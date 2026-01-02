using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Azathrix.MiniPanda.Compiler;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.Exceptions;
using Environment = Azathrix.MiniPanda.Core.Environment;

namespace Azathrix.MiniPanda.VM
{
    public delegate (byte[] data, string fullPath) FileLoader(string path);

    public class VirtualMachine
    {
        private const int StackMax = 256;
        private const int FramesMax = 64;

        private readonly Value[] _stack = new Value[StackMax];
        private int _stackTop;

        private readonly CallFrame[] _frames = new CallFrame[FramesMax];
        private int _frameCount;
        private Upvalue _openUpvalues;

        // Scope management
        private readonly Environment _globalScope;
        private readonly Dictionary<string, Environment> _scopeCache = new Dictionary<string, Environment>();
        private readonly HashSet<string> _loadingModules = new HashSet<string>();

        // Caches
        private readonly Dictionary<string, CompiledScript> _scriptCache = new Dictionary<string, CompiledScript>();
        private readonly Dictionary<string, CompiledScript> _evalCache = new Dictionary<string, CompiledScript>();
        private readonly Dictionary<string, CompiledScript> _moduleScriptCache = new Dictionary<string, CompiledScript>();
        private readonly Dictionary<string, MiniPandaModule> _moduleCache = new Dictionary<string, MiniPandaModule>();

        // Configuration
        public bool CacheEnabled { get; set; } = true;
        public FileLoader CustomLoader { get; set; }

        public Environment GlobalScope => _globalScope;

        private struct CallFrame
        {
            public MiniPandaFunction Function;
            public Bytecode Bytecode;
            public int IP;
            public int StackBase;
        }

        public VirtualMachine()
        {
            _globalScope = new Environment();
        }

        public void RegisterBuiltins()
        {
            Builtins.Register(_globalScope);
            // Register _G global table
            _globalScope.Define("_G", Value.FromObject(new MiniPandaGlobalTable(_globalScope)));
        }

        public void Reset()
        {
            _scriptCache.Clear();
            _evalCache.Clear();
            _moduleScriptCache.Clear();
            _moduleCache.Clear();
            _scopeCache.Clear();
            _loadingModules.Clear();
            _stackTop = 0;
            _frameCount = 0;
            _openUpvalues = null;
        }

        #region Scope Management

        public Environment GetScope(string name)
        {
            if (_scopeCache.TryGetValue(name, out var scope))
                return scope;
            scope = _globalScope.CreateChild();
            _scopeCache[name] = scope;
            return scope;
        }

        public void ClearScope(string name)
        {
            if (_scopeCache.TryGetValue(name, out var scope))
                scope.Clear();
        }

        #endregion

        #region Global Variables

        public void SetGlobal(string name, Value value) => _globalScope.Define(name, value);
        public void SetGlobal(string name, double value) => SetGlobal(name, Value.FromNumber(value));
        public void SetGlobal(string name, bool value) => SetGlobal(name, Value.FromBool(value));
        public void SetGlobal(string name, string value) => SetGlobal(name, (Value)value);
        public void SetGlobal(string name, NativeFunction func) => SetGlobal(name, Value.FromObject(func));

        public Value GetGlobal(string name)
        {
            if (!_globalScope.Contains(name))
                throw new MiniPandaRuntimeException($"Undefined global variable '{name}'");
            return _globalScope.Get(name);
        }

        #endregion

        #region High-Level API

        public Value Run(string code, string scopeName = "main", bool clearScope = true)
        {
            var compiled = Compile(code);
            var scope = GetScope(scopeName);
            if (clearScope) scope.Clear();
            return RunBytecode(compiled.Bytecode, scope);
        }

        public Value Run(byte[] data, string scopeName = "main", bool clearScope = true)
        {
            if (IsBytecode(data))
            {
                var bytecode = Bytecode.Deserialize(data);
                var scope = GetScope(scopeName);
                if (clearScope) scope.Clear();
                return RunBytecode(bytecode, scope);
            }
            return Run(Encoding.UTF8.GetString(data), scopeName, clearScope);
        }

        public T Run<T>(string code, string scopeName = "main", bool clearScope = true)
        {
            return Run(code, scopeName, clearScope).To<T>(this);
        }

        public Value Eval(string expression, object env = null, string scopeName = "main", bool clearScope = true)
        {
            var code = $"return {expression}";

            CompiledScript compiled;
            if (CacheEnabled && _evalCache.TryGetValue(expression, out compiled))
            {
                // use cached
            }
            else
            {
                compiled = CompileCode(code);
                if (CacheEnabled) _evalCache[expression] = compiled;
            }

            var scope = GetScope(scopeName);
            if (clearScope) scope.Clear();

            if (env != null)
            {
                if (env is Environment e)
                {
                    foreach (var kvp in e.GetAll())
                        scope.Define(kvp.Key, kvp.Value);
                }
                else if (env is Dictionary<string, object> dict)
                {
                    scope.With(dict);
                }
                else
                {
                    scope.With(env);
                }
            }

            return RunBytecode(compiled.Bytecode, scope);
        }

        public T Eval<T>(string expression, object env = null, string scopeName = "main", bool clearScope = true)
        {
            return Eval(expression, env, scopeName, clearScope).To<T>(this);
        }

        public Value Call(string funcName, params object[] args)
        {
            var func = _globalScope.Get(funcName);
            if (func.AsCallable() is { } callable)
            {
                var values = new Value[args.Length];
                for (int i = 0; i < args.Length; i++)
                    values[i] = ConvertToValue(args[i]);
                return callable.Call(this, values);
            }
            throw new MiniPandaRuntimeException($"'{funcName}' is not a function");
        }

        public static bool IsBytecode(byte[] data)
        {
            return data != null && data.Length >= 4 &&
                   data[0] == 'M' && data[1] == 'P' && data[2] == 'B' && data[3] == 'C';
        }

        #endregion

        #region Compilation

        public CompiledScript Compile(string code)
        {
            if (CacheEnabled)
            {
                var hash = ComputeHash(code);
                if (_scriptCache.TryGetValue(hash, out var cached))
                    return cached;

                var compiled = CompileCode(code, null, hash);
                _scriptCache[hash] = compiled;
                return compiled;
            }
            return CompileCode(code);
        }

        private CompiledScript CompileCode(string code, string sourcePath = null, string sourceHash = null)
        {
            var lexer = new Lexer.Lexer(code);
            var tokens = lexer.Tokenize();
            var parser = new Parser.Parser(tokens);
            var ast = parser.Parse();
            var compiler = new Compiler.Compiler();
            compiler.SourceFile = sourcePath;
            var bytecode = compiler.Compile(ast);
            return new CompiledScript(bytecode, sourceHash ?? ComputeHash(code));
        }

        private CompiledScript CompileData(byte[] data, string sourcePath)
        {
            if (IsBytecode(data))
            {
                var bytecode = Bytecode.Deserialize(data);
                bytecode.SourceFile = sourcePath;
                return new CompiledScript(bytecode);
            }
            var code = Encoding.UTF8.GetString(data);
            return CompileCode(code, sourcePath);
        }

        private static string ComputeHash(string input)
        {
            unchecked
            {
                const ulong fnvPrime = 1099511628211;
                ulong hash = 14695981039346656037;
                foreach (char c in input)
                {
                    hash ^= c;
                    hash *= fnvPrime;
                }
                return hash.ToString("X16");
            }
        }

        #endregion

        #region Module Management

        public void LoadModule(byte[] data, string moduleName, string sourcePath = null)
        {
            var compiled = CompileData(data, sourcePath ?? moduleName);
            _moduleScriptCache[moduleName] = compiled;
        }

        public Value RunFile(string path)
        {
            var (data, fullPath) = LoadFile(path);
            if (data == null)
                throw new MiniPandaRuntimeException($"Cannot load file: {path}");
            return Run(data);
        }

        public (byte[] data, string fullPath) LoadFile(string path)
        {
            var convertedPath = ConvertPath(path);
            if (CustomLoader != null)
                return CustomLoader(convertedPath);
            return DefaultLoadFile(convertedPath);
        }

        internal MiniPandaModule GetOrCreateModule(string path)
        {
            if (_moduleCache.TryGetValue(path, out var cached))
                return cached;

            // Circular dependency detection
            if (_loadingModules.Contains(path))
                throw new MiniPandaRuntimeException($"Circular dependency detected: {path}");

            _loadingModules.Add(path);
            try
            {
                var moduleEnv = GetScope($"module:{path}");
                var module = new MiniPandaModule(path, moduleEnv);
                _moduleCache[path] = module;

                var script = GetOrLoadModuleScript(path);
                RunNested(script.Bytecode, moduleEnv);

                return module;
            }
            finally
            {
                _loadingModules.Remove(path);
            }
        }

        private CompiledScript GetOrLoadModuleScript(string path)
        {
            if (_moduleScriptCache.TryGetValue(path, out var cached))
                return cached;

            var (data, fullPath) = LoadFile(path);
            if (data == null)
                throw new MiniPandaRuntimeException($"Cannot load script: {path}");

            var compiled = CompileData(data, fullPath ?? path);
            _moduleScriptCache[path] = compiled;
            return compiled;
        }

        public static string ConvertPath(string path)
        {
            return path.Replace('.', '/');
        }

        private (byte[] data, string fullPath) DefaultLoadFile(string path)
        {
            foreach (var ext in new[] { ".mpbc", ".panda" })
            {
                var fullPath = path + ext;
                if (File.Exists(fullPath))
                    return (File.ReadAllBytes(fullPath), fullPath);
            }
            if (File.Exists(path))
                return (File.ReadAllBytes(path), path);
            return (null, null);
        }

        public void ClearCache()
        {
            _scriptCache.Clear();
            _evalCache.Clear();
            _moduleScriptCache.Clear();
            _moduleCache.Clear();
        }

        #endregion

        #region Low-Level Execution

        public Value RunBytecode(Bytecode bytecode, Environment scope)
        {
            // Reset VM state
            _stackTop = 0;
            _frameCount = 0;
            _openUpvalues = null;

            var runScope = scope ?? _globalScope;

            var mainFunc = new MiniPandaFunction(
                new FunctionPrototype { Name = "<main>", Arity = 0, Code = bytecode },
                runScope
            );

            Push(Value.FromObject(mainFunc));
            CallValue(Value.FromObject(mainFunc), 0);

            return Execute();
        }

        /// <summary>
        /// Run bytecode without resetting VM state (for nested execution like module import).
        /// </summary>
        public Value RunNested(Bytecode bytecode, Environment scope)
        {
            // Save current state
            var savedStackTop = _stackTop;
            var savedFrameCount = _frameCount;
            var savedUpvalues = _openUpvalues;

            // Save frames that might be overwritten
            var savedFrames = new CallFrame[_frameCount];
            for (int i = 0; i < _frameCount; i++)
                savedFrames[i] = _frames[i];

            // Save stack that might be overwritten
            var savedStack = new Value[_stackTop];
            for (int i = 0; i < _stackTop; i++)
                savedStack[i] = _stack[i];

            try
            {
                // Reset for nested execution
                _stackTop = 0;
                _frameCount = 0;
                _openUpvalues = null;

                var mainFunc = new MiniPandaFunction(
                    new FunctionPrototype { Name = "<module>", Arity = 0, Code = bytecode },
                    scope
                );

                Push(Value.FromObject(mainFunc));
                CallValue(Value.FromObject(mainFunc), 0);

                return Execute();
            }
            finally
            {
                // Restore state
                _stackTop = savedStackTop;
                _frameCount = savedFrameCount;
                _openUpvalues = savedUpvalues;

                // Restore frames
                for (int i = 0; i < savedFrameCount; i++)
                    _frames[i] = savedFrames[i];

                // Restore stack
                for (int i = 0; i < savedStackTop; i++)
                    _stack[i] = savedStack[i];
            }
        }

        public Value CallFunction(MiniPandaFunction function, Value[] args)
        {
            if (args.Length != function.Arity)
                throw new MiniPandaRuntimeException($"Expected {function.Arity} arguments but got {args.Length}");
            if (_frameCount >= FramesMax)
                throw new MiniPandaRuntimeException("Stack overflow");

            // Push function (slot 0) or bound instance
            if (function.BoundInstance != null)
            {
                Push(Value.FromObject(function.BoundInstance));
            }
            else
            {
                Push(Value.FromObject(function));
            }

            var stackBase = _stackTop - 1;

            // Push arguments
            foreach (var arg in args)
            {
                Push(arg);
            }

            _frames[_frameCount++] = new CallFrame
            {
                Function = function,
                Bytecode = function.Prototype.Code,
                IP = 0,
                StackBase = stackBase
            };

            var result = Execute();
            return result;
        }

        public Value CallMethod(MiniPandaInstance instance, MiniPandaFunction method, Value[] args)
        {
            var boundMethod = method.Bind(instance);
            return CallFunction(boundMethod, args);
        }

        private Value Execute()
        {
            ref var frame = ref _frames[_frameCount - 1];

            try
            {
                return ExecuteInternal(ref frame);
            }
            catch (MiniPandaRuntimeException ex) when (ex.PandaStackTrace.Count == 0)
            {
                throw new MiniPandaRuntimeException(ex.Message, GetPandaStackTrace());
            }
            catch (MiniPandaRuntimeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MiniPandaRuntimeException(ex.Message, GetPandaStackTrace());
            }

            return Value.Null;
        }

        public List<Exceptions.StackFrame> GetStackTrace() => GetPandaStackTrace();

        public string GetCurrentLocation()
        {
            if (_frameCount == 0) return "<unknown>";
            var f = _frames[_frameCount - 1];
            var source = f.Bytecode?.SourceFile ?? "<unknown>";
            var line = f.Bytecode?.Lines != null && f.IP > 0 && f.IP <= f.Bytecode.Lines.Count
                ? f.Bytecode.Lines[Math.Max(0, f.IP - 1)]
                : 0;
            var funcName = f.Function?.Prototype?.FullName;
            if (!string.IsNullOrEmpty(funcName) && funcName != "<main>")
                return $"{source}:{line} in {funcName}";
            return $"{source}:{line}";
        }

        private List<Exceptions.StackFrame> GetPandaStackTrace()
        {
            var frames = new List<Exceptions.StackFrame>();
            for (int i = _frameCount - 1; i >= 0; i--)
            {
                var f = _frames[i];
                var name = f.Function?.Prototype?.FullName ?? "<main>";
                var source = f.Bytecode?.SourceFile ?? "<unknown>";
                var line = f.Bytecode?.Lines != null && f.IP > 0 && f.IP <= f.Bytecode.Lines.Count
                    ? f.Bytecode.Lines[Math.Max(0, f.IP - 1)]
                    : 0;
                frames.Add(new Exceptions.StackFrame(name, source, line));
            }
            return frames;
        }

        private Value ExecuteInternal(ref CallFrame frame)
        {
            while (true)
            {
                var op = (Opcode)frame.Bytecode.Code[frame.IP++];

                switch (op)
                {
                    case Opcode.Const:
                        {
                            var index = ReadShort(ref frame);
                            Push(ToValue(frame.Bytecode.Constants[index]));
                            break;
                        }

                    case Opcode.Null: Push(Value.Null); break;
                    case Opcode.True: Push(Value.True); break;
                    case Opcode.False: Push(Value.False); break;

                                        case Opcode.Pop: Pop(); break;
                    case Opcode.Dup: Push(Peek(0)); break;

                    case Opcode.Swap:
                        {
                            var a = Pop();
                            var b = Pop();
                            Push(a);
                            Push(b);
                            break;
                        }

                    case Opcode.Dup2:
                        {
                            var a = Peek(1);
                            var b = Peek(0);
                            Push(a);
                            Push(b);
                            break;
                        }

                    case Opcode.SwapUnder:
                        {
                            var top = _stackTop - 1;
                            var temp = _stack[top - 2];
                            _stack[top - 2] = _stack[top - 3];
                            _stack[top - 3] = temp;
                            break;
                        }

                    case Opcode.Rot3Under:
                        {
                            var top = _stackTop - 1;
                            var a = _stack[top - 3];
                            var b = _stack[top - 2];
                            var c = _stack[top - 1];
                            _stack[top - 3] = c;
                            _stack[top - 2] = a;
                            _stack[top - 1] = b;
                            break;
                        }

                    case Opcode.GetLocal:
                        {
                            var slot = frame.Bytecode.Code[frame.IP++];
                            Push(_stack[frame.StackBase + slot]);
                            break;
                        }

                    case Opcode.SetLocal:
                        {
                            var slot = frame.Bytecode.Code[frame.IP++];
                            _stack[frame.StackBase + slot] = Peek(0);
                            break;
                        }

                    case Opcode.GetUpvalue:
                        {
                            var slot = frame.Bytecode.Code[frame.IP++];
                            var upvalue = frame.Function.Upvalues[slot];
                            Push(upvalue.Get(_stack));
                            break;
                        }

                    case Opcode.SetUpvalue:
                        {
                            var slot = frame.Bytecode.Code[frame.IP++];
                            var upvalue = frame.Function.Upvalues[slot];
                            upvalue.Set(_stack, Peek(0));
                            break;
                        }

                    case Opcode.CloseUpvalue:
                        {
                            CloseUpvalues(_stackTop - 1);
                            Pop();
                            break;
                        }

                    case Opcode.GetGlobal:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            var globals = ResolveGlobals(ref frame);
                            if (!globals.Contains(name))
                                throw new MiniPandaRuntimeException($"Undefined variable '{name}'");
                            Push(globals.Get(name));
                            break;
                        }

                    case Opcode.SetGlobal:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            var globals = ResolveGlobals(ref frame);
                            globals.Set(name, Peek(0));
                            break;
                        }

                    case Opcode.DefineGlobal:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            var globals = ResolveGlobals(ref frame);
                            globals.Define(name, Pop());
                            break;
                        }

                    case Opcode.DefineRootGlobal:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            _globalScope.Define(name, Pop());
                            break;
                        }

                    case Opcode.Add: BinaryOp((a, b) => a + b, (a, b) => a + b); break;
                    case Opcode.Sub: BinaryOp((a, b) => a - b); break;
                    case Opcode.Mul: BinaryOp((a, b) => a * b); break;
                    case Opcode.Div: BinaryOp((a, b) => a / b); break;
                    case Opcode.Mod: BinaryOp((a, b) => a % b); break;

                    case Opcode.Neg:
                        {
                            var val = Pop();
                            Push(Value.FromNumber(-val.AsNumber()));
                            break;
                        }

                    case Opcode.Not:
                        {
                            var val = Pop();
                            Push(Value.FromBool(!val.AsBool()));
                            break;
                        }

                    case Opcode.Eq: { var b = Pop(); var a = Pop(); Push(Value.FromBool(a == b)); break; }
                    case Opcode.Ne: { var b = Pop(); var a = Pop(); Push(Value.FromBool(a != b)); break; }
                    case Opcode.Lt: CompareOp((a, b) => a < b); break;
                    case Opcode.Le: CompareOp((a, b) => a <= b); break;
                    case Opcode.Gt: CompareOp((a, b) => a > b); break;
                    case Opcode.Ge: CompareOp((a, b) => a >= b); break;

                    case Opcode.Jump:
                        {
                            var offset = ReadShort(ref frame);
                            frame.IP += offset;
                            break;
                        }

                    case Opcode.JumpIfFalse:
                        {
                            var offset = ReadShort(ref frame);
                            if (!Peek(0).AsBool()) frame.IP += offset;
                            break;
                        }

                    case Opcode.JumpIfTrue:
                        {
                            var offset = ReadShort(ref frame);
                            if (Peek(0).AsBool()) frame.IP += offset;
                            break;
                        }

                    case Opcode.Loop:
                        {
                            var offset = ReadShort(ref frame);
                            frame.IP -= offset;
                            break;
                        }

                    case Opcode.Call:
                        {
                            var argCount = frame.Bytecode.Code[frame.IP++];
                            var callee = Peek(argCount);
                            if (callee.IsNull)
                            {
                                throw new MiniPandaRuntimeException($"Cannot call null value");
                            }
                            if (!CallValue(callee, argCount))
                            {
                                var typeName = callee.AsObject()?.GetType().Name ?? callee.Type.ToString();
                                throw new MiniPandaRuntimeException($"Cannot call value of type '{typeName}'");
                            }
                            frame = ref _frames[_frameCount - 1];
                            break;
                        }

                                        case Opcode.Return:
                        {
                            var result = Pop();
                            CloseUpvalues(frame.StackBase);
                            _frameCount--;

                            if (_frameCount == 0)
                            {
                                _stackTop = 0;
                                return result;
                            }

                            _stackTop = frame.StackBase;
                            Push(result);
                            frame = ref _frames[_frameCount - 1];
                            break;
                        }

                                        case Opcode.Closure:
                        {
                            var index = ReadShort(ref frame);
                            var prototype = frame.Bytecode.Constants[index] as FunctionPrototype;
                            var function = new MiniPandaFunction(prototype, frame.Function?.Closure ?? _globalScope);
                            for (int i = 0; i < prototype.UpvalueCount; i++)
                            {
                                var isLocal = frame.Bytecode.Code[frame.IP++] == 1;
                                var slot = frame.Bytecode.Code[frame.IP++];
                                function.Upvalues[i] = isLocal
                                    ? CaptureUpvalue(frame.StackBase + slot)
                                    : frame.Function.Upvalues[slot];
                            }
                            Push(Value.FromObject(function));
                            break;
                        }

                    case Opcode.NewArray:
                        {
                            var count = ReadShort(ref frame);
                            var array = new MiniPandaArray();
                            var startIndex = _stackTop - count;
                            for (int i = 0; i < count; i++)
                            {
                                array.Elements.Add(_stack[startIndex + i]);
                            }
                            _stackTop -= count;
                            Push(Value.FromObject(array));
                            break;
                        }

                    case Opcode.NewObject:
                        {
                            Push(Value.FromObject(new MiniPandaObject()));
                            break;
                        }

                    case Opcode.GetField:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            var obj = Pop();
                            if (obj.As<MiniPandaObject>() is { } dict)
                            {
                                Push(dict.Get(name));
                            }
                            else if (obj.As<MiniPandaModule>() is { } module)
                            {
                                Push(module.GetMember(name));
                            }
                            else
                            {
                                Push(Value.Null);
                            }
                            break;
                        }

                    case Opcode.SetField:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            var value = Pop();
                            var obj = Peek(0);
                            if (obj.As<MiniPandaObject>() is { } dict)
                            {
                                dict.Set(name, value);
                            }
                            break;
                        }

                    case Opcode.GetIndex:
                        {
                            var index = Pop();
                            var obj = Pop();
                            if (obj.As<MiniPandaArray>() is { } array)
                            {
                                Push(array.Get((int)index.AsNumber()));
                            }
                            else if (obj.As<MiniPandaObject>() is { } dict)
                            {
                                Push(dict.Get(index.AsString()));
                            }
                            else
                            {
                                Push(Value.Null);
                            }
                            break;
                        }

                    case Opcode.SetIndex:
                        {
                            var value = Pop();
                            var index = Pop();
                            var obj = Pop();
                            if (obj.As<MiniPandaArray>() is { } array)
                            {
                                array.Set((int)index.AsNumber(), value);
                            }
                            else if (obj.As<MiniPandaObject>() is { } dict)
                            {
                                dict.Set(index.AsString(), value);
                            }
                            Push(value);
                            break;
                        }

                    case Opcode.GetProperty:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            var obj = Pop();

                            if (obj.As<MiniPandaInstance>() is { } instance)
                            {
                                Push(instance.Get(name));
                            }
                            else if (obj.As<MiniPandaModule>() is { } module)
                            {
                                Push(module.GetMember(name));
                            }
                            else if (obj.As<MiniPandaGlobalTable>() is { } globalTable)
                            {
                                Push(globalTable.Get(name));
                            }
                            else if (obj.As<MiniPandaObject>() is { } dict)
                            {
                                Push(dict.Get(name));
                            }
                            else if (obj.As<MiniPandaArray>() is { } array && name == "length")
                            {
                                Push(Value.FromNumber(array.Length));
                            }
                            else if (obj.As<MiniPandaString>() is { } str && name == "length")
                            {
                                Push(Value.FromNumber(str.Value.Length));
                            }
                            else
                            {
                                Push(Value.Null);
                            }
                            break;
                        }

                    case Opcode.SetProperty:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            var value = Pop();
                            var obj = Pop();

                            if (obj.As<MiniPandaInstance>() is { } instance)
                            {
                                instance.Set(name, value);
                            }
                            else if (obj.As<MiniPandaGlobalTable>() is { } globalTable)
                            {
                                globalTable.Set(name, value);
                            }
                            else if (obj.As<MiniPandaObject>() is { } dict)
                            {
                                dict.Set(name, value);
                            }
                            Push(value);
                            break;
                        }

                    case Opcode.Class:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            Push(Value.FromObject(new MiniPandaClass(name)));
                            break;
                        }

                    case Opcode.Inherit:
                        {
                            var subclass = Pop().As<MiniPandaClass>();
                            var superclass = Pop().As<MiniPandaClass>();
                            if (superclass != null && subclass != null)
                            {
                                subclass.SuperClass = superclass;
                                foreach (var method in superclass.Methods)
                                {
                                    if (!subclass.Methods.ContainsKey(method.Key))
                                    {
                                        subclass.Methods[method.Key] = method.Value;
                                    }
                                }
                            }
                            break;
                        }

                    case Opcode.Method:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            var method = Pop().As<MiniPandaFunction>();
                            var klass = Peek(0).As<MiniPandaClass>();
                            if (klass != null && method != null)
                            {
                                // Constructor uses class name
                                method.IsInitializer = name == klass.Name;
                                klass.Methods[name] = method;
                            }
                            break;
                        }

                                        case Opcode.Invoke:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            var argCount = frame.Bytecode.Code[frame.IP++];
                            var receiver = Peek(argCount);

                            if (receiver.As<MiniPandaInstance>() is { } instance)
                            {
                                var member = instance.Get(name);
                                if (member.AsCallable() is { })
                                {
                                    _stack[_stackTop - argCount - 1] = member;
                                    if (!CallValue(member, argCount))
                                    {
                                        throw new MiniPandaRuntimeException($"Cannot call '{name}'");
                                    }
                                    frame = ref _frames[_frameCount - 1];
                                }
                                else
                                {
                                    throw new MiniPandaRuntimeException($"'{name}' is not callable on instance of '{instance.Class.Name}'");
                                }
                            }
                            else if (receiver.As<MiniPandaModule>() is { } module)
                            {
                                var member = module.GetMember(name);
                                if (member.As<MiniPandaFunction>() is { } func)
                                {
                                    // Replace module with function on stack
                                    _stack[_stackTop - argCount - 1] = Value.FromObject(func);
                                    Call(func, argCount);
                                    frame = ref _frames[_frameCount - 1];
                                }
                                else if (member.As<NativeFunction>() is { } native)
                                {
                                    // Pop args and module, call native, push result
                                    var args = new Value[argCount];
                                    for (int i = argCount - 1; i >= 0; i--)
                                        args[i] = Pop();
                                    Pop(); // Pop module
                                    var result = native.Call(this, args);
                                    Push(result);
                                }
                                else
                                {
                                    throw new MiniPandaRuntimeException($"'{name}' is not a function in module '{module.Path}'");
                                }
                            }
                            else if (receiver.As<MiniPandaGlobalTable>() is { } globalTable)
                            {
                                var member = globalTable.Get(name);
                                if (member.As<MiniPandaFunction>() is { } func)
                                {
                                    _stack[_stackTop - argCount - 1] = Value.FromObject(func);
                                    Call(func, argCount);
                                    frame = ref _frames[_frameCount - 1];
                                }
                                else if (member.As<NativeFunction>() is { } native)
                                {
                                    var args = new Value[argCount];
                                    for (int i = argCount - 1; i >= 0; i--)
                                        args[i] = Pop();
                                    Pop(); // Pop globalTable
                                    var result = native.Call(this, args);
                                    Push(result);
                                }
                                else if (member.AsCallable() is { } callable)
                                {
                                    _stack[_stackTop - argCount - 1] = member;
                                    if (!CallValue(member, argCount))
                                    {
                                        throw new MiniPandaRuntimeException($"Cannot call '{name}'");
                                    }
                                    frame = ref _frames[_frameCount - 1];
                                }
                                else
                                {
                                    throw new MiniPandaRuntimeException($"'{name}' is not callable in global scope");
                                }
                            }
                            else
                            {
                                var typeName = receiver.AsObject()?.GetType().Name ?? receiver.Type.ToString();
                                throw new MiniPandaRuntimeException($"Cannot invoke '{name}' on value of type '{typeName}'");
                            }
                            break;
                        }

                    case Opcode.This:
                        {
                            Push(_stack[frame.StackBase]);
                            break;
                        }

                    case Opcode.GetSuper:
                        {
                            var index = ReadShort(ref frame);
                            var name = frame.Bytecode.Constants[index] as string;
                            var instance = _stack[frame.StackBase].As<MiniPandaInstance>();
                            if (instance?.Class.SuperClass != null)
                            {
                                var method = instance.Class.SuperClass.FindMethod(name);
                                if (method != null)
                                {
                                    Push(Value.FromObject(method.Bind(instance)));
                                }
                                else
                                {
                                    Push(Value.Null);
                                }
                            }
                            else
                            {
                                Push(Value.Null);
                            }
                            break;
                        }

                    case Opcode.BuildString:
                        {
                            var count = frame.Bytecode.Code[frame.IP++];
                            var sb = new StringBuilder();
                            var parts = new Value[count];
                            for (int i = count - 1; i >= 0; i--)
                            {
                                parts[i] = Pop();
                            }
                            foreach (var part in parts)
                            {
                                sb.Append(part.AsString());
                            }
                            Push(Value.FromObject(new MiniPandaString(sb.ToString())));
                            break;
                        }

                    case Opcode.GetIter:
                        {
                            var iterable = Pop();
                            if (iterable.As<MiniPandaArray>() is { } array)
                            {
                                Push(Value.FromObject(new ArrayIterator(array)));
                            }
                            else
                            {
                                throw new MiniPandaRuntimeException("Object is not iterable");
                            }
                            break;
                        }

                    case Opcode.ForIter:
                        {
                            var offset = ReadShort(ref frame);
                            var iterValue = Peek(0);
                            var iter = iterValue.As<ArrayIterator>();
                            if (iter == null && iterValue.As<MiniPandaArray>() is { } array)
                            {
                                // Allow iterating raw arrays if GetIter was skipped.
                                iter = new ArrayIterator(array);
                                _stack[_stackTop - 1] = Value.FromObject(iter);
                            }

                            if (iter != null && iter.HasNext())
                            {
                                Push(iter.Next());
                            }
                            else
                            {
                                if (iter == null)
                                    throw new MiniPandaRuntimeException("Object is not iterable");
                                Pop(); // Remove iterator
                                frame.IP += offset;
                            }
                            break;
                        }
                    case Opcode.Import:
                        {
                            var pathIndex = ReadShort(ref frame);
                            var aliasIndex = ReadShort(ref frame);
                            var isGlobal = ReadByte(ref frame) == 1;
                            var path = frame.Bytecode.Constants[pathIndex] as string;
                            var alias = frame.Bytecode.Constants[aliasIndex] as string;

                            var module = GetOrCreateModule(path);

                            if (isGlobal)
                            {
                                var bindName = string.IsNullOrEmpty(alias) ? GetModuleName(path) : alias;
                                _globalScope.Define(bindName, Value.FromObject(module));
                            }
                            else
                            {
                                Push(Value.FromObject(module));
                            }
                            break;
                        }

                    default:
                        throw new MiniPandaRuntimeException($"Unknown opcode: {op}");
                }
            }
        }

        private bool CallValue(Value callee, int argCount)
        {
            if (callee.As<MiniPandaFunction>() is { } function)
            {
                // Handle bound functions (e.g., from super.method)
                if (function.BoundInstance != null)
                {
                    _stack[_stackTop - argCount - 1] = Value.FromObject(function.BoundInstance);
                }
                return Call(function, argCount);
            }

            if (callee.As<MiniPandaClass>() is { } klass)
            {
                var instance = new MiniPandaInstance(klass);
                _stack[_stackTop - argCount - 1] = Value.FromObject(instance);

                // Constructor uses class name
                var constructor = klass.FindMethod(klass.Name);
                if (constructor != null)
                {
                    return Call(constructor.Bind(instance), argCount);
                }
                else if (argCount != 0)
                {
                    throw new MiniPandaRuntimeException($"Expected 0 arguments but got {argCount}");
                }
                return true;
            }

            if (callee.As<NativeFunction>() is { } native)
            {
                var args = new Value[argCount];
                for (int i = argCount - 1; i >= 0; i--)
                {
                    args[i] = Pop();
                }
                Pop(); // Pop the function itself
                var result = native.Call(this, args);
                Push(result);
                return true;
            }

            if (callee.As<MiniPandaBoundMethod>() is { } bound)
            {
                _stack[_stackTop - argCount - 1] = Value.FromObject(bound.Instance);
                return Call(bound.Method, argCount);
            }

            return false;
        }

        private bool Call(MiniPandaFunction function, int argCount)
        {
            if (argCount != function.Arity)
            {
                throw new MiniPandaRuntimeException($"Expected {function.Arity} arguments but got {argCount}");
            }

            if (_frameCount == FramesMax)
            {
                throw new MiniPandaRuntimeException("Stack overflow");
            }

            _frames[_frameCount++] = new CallFrame
            {
                Function = function,
                Bytecode = function.Prototype.Code,
                IP = 0,
                StackBase = _stackTop - argCount - 1
            };

            return true;
        }

        private void CallMethod(MiniPandaInstance instance, MiniPandaFunction method, int argCount)
        {
            _stack[_stackTop - argCount - 1] = Value.FromObject(instance);
            Call(method.Bind(instance), argCount);
        }

        private void BinaryOp(Func<double, double, double> numOp, Func<string, string, string> strOp = null)
        {
            var b = Pop();
            var a = Pop();

            if (strOp != null && (a.IsString || b.IsString))
            {
                Push(Value.FromObject(new MiniPandaString(strOp(a.AsString(), b.AsString()))));
            }
            else
            {
                Push(Value.FromNumber(numOp(a.AsNumber(), b.AsNumber())));
            }
        }

        private void CompareOp(Func<double, double, bool> op)
        {
            var b = Pop();
            var a = Pop();
            Push(Value.FromBool(op(a.AsNumber(), b.AsNumber())));
        }

        private Environment ResolveGlobals(ref CallFrame frame)
        {
            // Prefer the function's closure for module/global isolation.
            return frame.Function?.Closure ?? _globalScope;
        }

        private ushort ReadShort(ref CallFrame frame)
        {
            var high = frame.Bytecode.Code[frame.IP++];
            var low = frame.Bytecode.Code[frame.IP++];
            return (ushort)((high << 8) | low);
        }

        private byte ReadByte(ref CallFrame frame)
        {
            return frame.Bytecode.Code[frame.IP++];
        }

        private static string GetModuleName(string path)
        {
            var lastDot = path.LastIndexOf('.');
            return lastDot >= 0 ? path.Substring(lastDot + 1) : path;
        }
        private Upvalue CaptureUpvalue(int index)
        {
            Upvalue previous = null;
            var upvalue = _openUpvalues;
            while (upvalue != null && upvalue.Index > index)
            {
                previous = upvalue;
                upvalue = upvalue.Next;
            }

            if (upvalue != null && upvalue.Index == index)
            {
                return upvalue;
            }

            var created = new Upvalue { Index = index, Next = upvalue };
            if (previous == null)
            {
                _openUpvalues = created;
            }
            else
            {
                previous.Next = created;
            }

            return created;
        }

        private void CloseUpvalues(int last)
        {
            while (_openUpvalues != null && _openUpvalues.Index >= last)
            {
                var upvalue = _openUpvalues;
                upvalue.Close(_stack);
                _openUpvalues = upvalue.Next;
            }
        }

        private void Push(Value value)
        {
            if (_stackTop >= StackMax)
                throw new MiniPandaRuntimeException("Stack overflow");
            _stack[_stackTop++] = value;
        }

        private Value Pop() => _stack[--_stackTop];
        private Value Peek(int distance) => _stack[_stackTop - 1 - distance];

        private Value ToValue(object obj)
        {
            return obj switch
            {
                null => Value.Null,
                bool b => Value.FromBool(b),
                double d => Value.FromNumber(d),
                string s => Value.FromObject(new MiniPandaString(s)),
                FunctionPrototype fp => Value.FromObject(new MiniPandaFunction(fp, _globalScope)),
                _ => Value.Null
            };
        }

        private static Value ConvertToValue(object obj)
        {
            return obj switch
            {
                null => Value.Null,
                bool b => Value.FromBool(b),
                int i => Value.FromNumber(i),
                long l => Value.FromNumber(l),
                float f => Value.FromNumber(f),
                double d => Value.FromNumber(d),
                string s => s,
                Value v => v,
                _ => Value.FromObject(new MiniPandaString(obj.ToString()))
            };
        }

        #endregion
    }

    public sealed class Upvalue
    {
        public int Index;
        public Value Closed;
        public bool IsClosed;
        public Upvalue Next;

        public Value Get(Value[] stack) => IsClosed ? Closed : stack[Index];

        public void Set(Value[] stack, Value value)
        {
            if (IsClosed)
            {
                Closed = value;
            }
            else
            {
                stack[Index] = value;
            }
        }

        public void Close(Value[] stack)
        {
            if (IsClosed) return;
            Closed = stack[Index];
            IsClosed = true;
        }
    }

    internal class ArrayIterator : GC.MiniPandaHeapObject
    {
        private readonly MiniPandaArray _array;
        private int _index;

        public ArrayIterator(MiniPandaArray array)
        {
            _array = array;
            _index = 0;
        }

        public bool HasNext() => _index < _array.Length;
        public Value Next() => _array.Get(_index++);
    }
}
