using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Azathrix.MiniPanda.Compiler;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.Debug;
using Azathrix.MiniPanda.Exceptions;
using Environment = Azathrix.MiniPanda.Core.Environment;

namespace Azathrix.MiniPanda.VM
{
    /// <summary>
    /// 文件加载器委托，用于自定义脚本文件的加载方式
    /// </summary>
    /// <param name="path">脚本路径</param>
    /// <returns>文件数据和完整路径的元组</returns>
    public delegate (byte[] data, string fullPath) FileLoader(string path);

    /// <summary>
    /// MiniPanda 虚拟机 - 字节码执行引擎
    /// <para>
    /// 这是 MiniPanda 脚本语言的核心执行引擎，负责：
    /// <list type="bullet">
    /// <item>执行编译后的字节码指令</item>
    /// <item>管理运行时栈和调用帧</item>
    /// <item>处理变量作用域和闭包</item>
    /// <item>模块导入和缓存管理</item>
    /// <item>异常处理机制</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <remarks>
    /// 使用示例：
    /// <code>
    /// var vm = new VirtualMachine();
    /// vm.RegisterBuiltins();
    /// var result = vm.Run("return 1 + 2");
    /// </code>
    /// </remarks>
    public class VirtualMachine
    {
        // ==================== 常量定义 ====================

        /// <summary>操作数栈最大深度</summary>
        private const int StackMax = 256;
        /// <summary>调用帧最大深度（递归深度限制）</summary>
        private const int FramesMax = 64;
        /// <summary>异常处理器最大嵌套深度</summary>
        private const int HandlersMax = 32;

        // ==================== 运行时栈 ====================

        /// <summary>操作数栈，存储运算中间值</summary>
        private readonly Value[] _stack = new Value[StackMax];
        /// <summary>栈顶指针，指向下一个空闲位置</summary>
        private int _stackTop;

        // ==================== 调用帧管理 ====================

        /// <summary>调用帧栈，每次函数调用创建一个新帧</summary>
        private readonly CallFrame[] _frames = new CallFrame[FramesMax];
        /// <summary>当前调用帧数量</summary>
        private int _frameCount;
        /// <summary>开放上值链表头，用于闭包变量捕获</summary>
        private Upvalue _openUpvalues;

        // ==================== 异常处理 ====================

        /// <summary>异常处理器栈，支持 try/catch/finally</summary>
        private readonly ExceptionHandler[] _handlers = new ExceptionHandler[HandlersMax];
        /// <summary>当前异常处理器数量</summary>
        private int _handlerCount;
        /// <summary>待处理的异常（finally 块执行后需要重新抛出）</summary>
        private Value _pendingException;
        /// <summary>是否有待处理的异常</summary>
        private bool _hasPendingException;

        // ==================== 作用域管理 ====================

        /// <summary>全局作用域，存储全局变量和内置函数</summary>
        private readonly Environment _globalScope;
        /// <summary>命名作用域缓存，用于隔离不同脚本的执行环境</summary>
        private readonly Dictionary<string, Environment> _scopeCache = new Dictionary<string, Environment>();
        /// <summary>正在加载的模块集合，用于检测循环依赖</summary>
        private readonly HashSet<string> _loadingModules = new HashSet<string>();

        // ==================== 编译缓存 ====================

        /// <summary>脚本编译缓存，避免重复编译相同代码</summary>
        private readonly Dictionary<string, CompiledScript> _scriptCache = new Dictionary<string, CompiledScript>();
        /// <summary>表达式求值缓存</summary>
        private readonly Dictionary<string, CompiledScript> _evalCache = new Dictionary<string, CompiledScript>();
        /// <summary>模块脚本缓存</summary>
        private readonly Dictionary<string, CompiledScript> _moduleScriptCache =
            new Dictionary<string, CompiledScript>();
        /// <summary>已加载模块缓存</summary>
        private readonly Dictionary<string, MiniPandaModule> _moduleCache = new Dictionary<string, MiniPandaModule>();

        // ==================== 配置选项 ====================

        /// <summary>是否启用编译缓存（默认启用）</summary>
        public bool CacheEnabled { get; set; } = true;
        /// <summary>自定义文件加载器，用于 Unity 等特殊环境</summary>
        public FileLoader CustomLoader { get; set; }
        /// <summary>获取全局作用域</summary>
        public Environment GlobalScope => _globalScope;
        /// <summary>调试器实例（可选）</summary>
        public Debugger Debugger { get; set; }
        /// <summary>当前调用帧深度（用于调试）</summary>
        public int FrameDepth => _frameCount;

        /// <summary>
        /// 调用帧结构，保存函数调用的执行上下文
        /// </summary>
        private struct CallFrame
        {
            /// <summary>当前执行的函数</summary>
            public MiniPandaFunction Function;
            /// <summary>函数的字节码</summary>
            public Bytecode Bytecode;
            /// <summary>指令指针（Instruction Pointer）</summary>
            public int IP;
            /// <summary>栈基址，局部变量从此位置开始</summary>
            public int StackBase;
        }

        /// <summary>
        /// 异常处理器结构，保存 try/catch/finally 块的信息
        /// </summary>
        private struct ExceptionHandler
        {
            /// <summary>catch 块的字节码地址</summary>
            public int CatchAddress;
            /// <summary>finally 块的字节码地址</summary>
            public int FinallyAddress;
            /// <summary>catch 变量的局部变量槽位（-1 表示无变量）</summary>
            public int CatchVarSlot;
            /// <summary>进入 try 块时的栈深度</summary>
            public int StackBase;
            /// <summary>进入 try 块时的调用帧数量</summary>
            public int FrameCount;
        }

        /// <summary>
        /// 创建新的虚拟机实例
        /// </summary>
        public VirtualMachine()
        {
            _globalScope = new Environment();
        }

        /// <summary>
        /// 注册内置函数到全局作用域
        /// </summary>
        /// <remarks>
        /// 注册的内置函数包括：print、type、len、math、string、array 等
        /// 同时注册 _G 全局表，允许脚本动态访问全局变量
        /// </remarks>
        public void RegisterBuiltins()
        {
            Builtins.Register(_globalScope);
            // 注册 _G 全局表，提供对全局作用域的动态访问
            _globalScope.Define("_G", Value.FromObject(new MiniPandaGlobalTable(_globalScope)));
        }

        /// <summary>
        /// 重置虚拟机状态
        /// </summary>
        /// <remarks>
        /// 清除所有缓存、重置栈和调用帧，但保留全局作用域中的定义
        /// </remarks>
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
            _handlerCount = 0;
            _hasPendingException = false;
            _openUpvalues = null;
        }

        #region 作用域管理

        /// <summary>
        /// 获取或创建命名作用域
        /// </summary>
        /// <param name="name">作用域名称</param>
        /// <returns>作用域环境，继承自全局作用域</returns>
        /// <remarks>
        /// 作用域会被缓存，相同名称返回相同的作用域实例
        /// 常用于隔离不同脚本的执行环境
        /// </remarks>
        public Environment GetScope(string name)
        {
            if (_scopeCache.TryGetValue(name, out var scope))
                return scope;
            scope = _globalScope.CreateChild();
            _scopeCache[name] = scope;
            return scope;
        }

        /// <summary>
        /// 清除指定作用域中的所有变量
        /// </summary>
        /// <param name="name">作用域名称</param>
        public void ClearScope(string name)
        {
            if (_scopeCache.TryGetValue(name, out var scope))
                scope.Clear();
        }

        #endregion

        #region 全局变量操作

        /// <summary>设置全局变量</summary>
        public void SetGlobal(string name, Value value) => _globalScope.Define(name, value);
        /// <summary>设置数值类型全局变量</summary>
        public void SetGlobal(string name, double value) => SetGlobal(name, Value.FromNumber(value));
        /// <summary>设置布尔类型全局变量</summary>
        public void SetGlobal(string name, bool value) => SetGlobal(name, Value.FromBool(value));
        /// <summary>设置字符串类型全局变量</summary>
        public void SetGlobal(string name, string value) => SetGlobal(name, (Value) value);
        /// <summary>设置原生函数类型全局变量</summary>
        public void SetGlobal(string name, NativeFunction func) => SetGlobal(name, Value.FromObject(func));

        /// <summary>
        /// 获取全局变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>变量值</returns>
        /// <exception cref="MiniPandaRuntimeException">变量未定义时抛出</exception>
        public Value GetGlobal(string name)
        {
            if (!_globalScope.Contains(name))
                throw new MiniPandaRuntimeException($"Undefined global variable '{name}'");
            return _globalScope.Get(name);
        }

        #endregion

        #region 调试支持

        /// <summary>
        /// 获取当前调用栈信息
        /// </summary>
        public StackFrameInfo[] GetStackTrace()
        {
            var frames = new List<StackFrameInfo>();
            if (_frameCount == 0) return frames.ToArray();

            // 调试器暂停时，IP 还没有递增，直接使用 IP；否则使用 IP - 1
            var isPaused = Debugger?.IsPaused == true;
            for (int i = _frameCount - 1; i >= 0; i--)
            {
                var frame = _frames[i];
                var name = frame.Function?.Prototype?.FullName ?? "<main>";
                var file = frame.Bytecode?.SourceFile ?? "<script>";
                // UnityEngine.Debug.Log($"[GetStackTrace] Frame {i}: name={name}, file={file}, Bytecode={frame.Bytecode != null}, SourceFile={frame.Bytecode?.SourceFile}");
                var line = 1;
                var ip = isPaused ? frame.IP : frame.IP - 1;
                if (ip >= 0 && ip < frame.Bytecode?.Lines.Count)
                {
                    line = frame.Bytecode.Lines[ip];
                }

                frames.Add(new StackFrameInfo
                {
                    Id = i,
                    Name = name,
                    File = file,
                    Line = line,
                    Column = 1
                });
            }
            return frames.ToArray();
        }

        /// <summary>
        /// 获取指定帧的局部变量
        /// </summary>
        public Dictionary<string, Value> GetLocalVariables(int frameId)
        {
            var result = new Dictionary<string, Value>();
            if (frameId < 0 || frameId >= _frameCount) return result;

            var frame = _frames[frameId];
            var func = frame.Function;
            if (func?.Prototype == null) return result;

            // 获取局部变量名（需要编译器提供调试信息）
            // 目前简化实现，返回栈上的值
            var stackBase = frame.StackBase;
            for (int i = 0; i < func.Prototype.Arity && stackBase + i < _stackTop; i++)
            {
                result[$"arg{i}"] = _stack[stackBase + i];
            }

            return result;
        }

        #endregion

        #region 高级 API

        /// <summary>
        /// 执行脚本代码
        /// </summary>
        /// <param name="code">脚本源代码</param>
        /// <param name="scopeName">作用域名称，默认 "main"</param>
        /// <param name="clearScope">是否在执行前清除作用域，默认 true</param>
        /// <returns>脚本返回值</returns>
        public Value Run(string code, string scopeName = "main", bool clearScope = true)
        {
            var compiled = Compile(code);
            var scope = GetScope(scopeName);
            if (clearScope) scope.Clear();
            return RunBytecode(compiled.Bytecode, scope);
        }

        /// <summary>
        /// 执行字节码或源代码数据
        /// </summary>
        /// <param name="data">字节码或 UTF-8 编码的源代码</param>
        /// <param name="scopeName">作用域名称</param>
        /// <param name="clearScope">是否清除作用域</param>
        /// <returns>执行结果</returns>
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

        /// <summary>
        /// 执行脚本并转换返回值类型
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        public T Run<T>(string code, string scopeName = "main", bool clearScope = true)
        {
            return Run(code, scopeName, clearScope).To<T>(this);
        }

        /// <summary>
        /// 求值表达式
        /// </summary>
        /// <param name="expression">表达式字符串</param>
        /// <param name="env">环境变量（可选）</param>
        /// <param name="scopeName">作用域名称</param>
        /// <param name="clearScope">是否清除作用域</param>
        /// <returns>表达式结果</returns>
        /// <remarks>
        /// 表达式会被包装为 "return {expression}" 执行
        /// env 参数可以是 Environment、Dictionary 或普通对象
        /// </remarks>
        public Value Eval(string expression, object env = null, string scopeName = "main", bool clearScope = true)
        {
            var code = $"return {expression}";

            CompiledScript compiled;
            if (CacheEnabled && _evalCache.TryGetValue(expression, out compiled))
            {
                // 使用缓存的编译结果
            }
            else
            {
                compiled = CompileCode(code);
                if (CacheEnabled) _evalCache[expression] = compiled;
            }

            var scope = GetScope(scopeName);
            if (clearScope) scope.Clear();

            // 将环境变量注入作用域
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

        /// <summary>
        /// 求值表达式并转换返回值类型
        /// </summary>
        public T Eval<T>(string expression, object env = null, string scopeName = "main", bool clearScope = true)
        {
            return Eval(expression, env, scopeName, clearScope).To<T>(this);
        }

        /// <summary>
        /// 调用全局函数
        /// </summary>
        /// <param name="funcName">函数名</param>
        /// <param name="args">参数列表</param>
        /// <returns>函数返回值</returns>
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

        /// <summary>
        /// 在指定作用域中调用全局函数
        /// </summary>
        /// <param name="scope">作用域对象（Environment、Dictionary 或普通对象）</param>
        /// <param name="funcName">函数名</param>
        /// <param name="args">参数列表</param>
        /// <returns>函数返回值</returns>
        /// <remarks>
        /// 创建临时作用域环境，将 scope 中的变量注入后执行函数
        /// 适用于需要向函数传递上下文的场景
        /// </remarks>
        public Value Call(object scope, string funcName, params object[] args)
        {
            var func = _globalScope.Get(funcName);
            if (func.As<MiniPandaFunction>() is { } function)
            {
                // 创建带有临时环境的作用域函数
                var scopedEnv = function.Closure.CreateChild();
                if (scope is Environment e)
                {
                    foreach (var kvp in e.GetAll())
                        scopedEnv.Define(kvp.Key, kvp.Value);
                }
                else if (scope is Dictionary<string, object> dict)
                {
                    scopedEnv.With(dict);
                }
                else if (scope != null)
                {
                    scopedEnv.With(scope);
                }

                var scopedFunc = new MiniPandaFunction(function.Prototype, scopedEnv);
                var values = new Value[args.Length];
                for (int i = 0; i < args.Length; i++)
                    values[i] = ConvertToValue(args[i]);
                return CallFunction(scopedFunc, values);
            }

            throw new MiniPandaRuntimeException($"'{funcName}' is not a function");
        }

        /// <summary>
        /// 检查数据是否为编译后的字节码
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>如果是字节码返回 true</returns>
        /// <remarks>
        /// 字节码以魔数 "MPBC" (MiniPanda ByteCode) 开头
        /// </remarks>
        public static bool IsBytecode(byte[] data)
        {
            return data != null && data.Length >= 4 &&
                   data[0] == 'M' && data[1] == 'P' && data[2] == 'B' && data[3] == 'C';
        }

        #endregion

        #region 编译

        /// <summary>
        /// 编译脚本代码
        /// </summary>
        /// <param name="code">源代码</param>
        /// <returns>编译后的脚本对象</returns>
        /// <remarks>
        /// 启用缓存时，相同代码只编译一次
        /// 使用 FNV-1a 哈希算法计算代码指纹
        /// </remarks>
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

        /// <summary>
        /// 编译源代码为字节码
        /// </summary>
        private CompiledScript CompileCode(string code, string sourcePath = null, string sourceHash = null)
        {
            // 词法分析：源代码 -> Token 流
            var lexer = new Lexer.Lexer(code);
            var tokens = lexer.Tokenize();
            // 语法分析：Token 流 -> AST
            var parser = new Parser.Parser(tokens);
            var ast = parser.Parse();
            // 代码生成：AST -> 字节码
            var compiler = new Compiler.Compiler();
            compiler.SourceFile = sourcePath;
            var bytecode = compiler.Compile(ast);
            return new CompiledScript(bytecode, sourceHash ?? ComputeHash(code));
        }

        /// <summary>
        /// 编译字节数据（自动检测格式）
        /// </summary>
        private CompiledScript CompileData(byte[] data, string sourcePath)
        {
            // 如果已经是字节码，直接反序列化
            if (IsBytecode(data))
            {
                var bytecode = Bytecode.Deserialize(data);
                bytecode.SourceFile = sourcePath;
                return new CompiledScript(bytecode);
            }

            // 否则作为源代码编译
            var code = Encoding.UTF8.GetString(data);
            return CompileCode(code, sourcePath);
        }

        /// <summary>
        /// 计算字符串的 FNV-1a 哈希值
        /// </summary>
        /// <remarks>
        /// 用于脚本缓存的键，快速且分布均匀
        /// </remarks>
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

        #region 模块管理

        /// <summary>
        /// 预加载模块到缓存
        /// </summary>
        /// <param name="data">模块数据（字节码或源代码）</param>
        /// <param name="moduleName">模块名称</param>
        /// <param name="sourcePath">源文件路径（可选）</param>
        public void LoadModule(byte[] data, string moduleName, string sourcePath = null)
        {
            var compiled = CompileData(data, sourcePath ?? moduleName);
            _moduleScriptCache[moduleName] = compiled;
        }

        /// <summary>
        /// 执行脚本文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>执行结果</returns>
        public Value RunFile(string path, string scopeName = "main", bool clearScope = true)
        {
            var (data, fullPath) = LoadFile(path);
            if (data == null)
                throw new MiniPandaRuntimeException($"Cannot load file: {path}");

            var compiled = CompileData(data, fullPath ?? path);
            var scope = GetScope(scopeName);
            if (clearScope) scope.Clear();
            return RunBytecode(compiled.Bytecode, scope);
        }

        /// <summary>
        /// 加载文件数据
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>文件数据和完整路径</returns>
        public (byte[] data, string fullPath) LoadFile(string path)
        {
            var convertedPath = ConvertPath(path);
            if (CustomLoader != null)
                return CustomLoader(convertedPath);
            return DefaultLoadFile(convertedPath);
        }

        /// <summary>
        /// 获取或创建模块
        /// </summary>
        /// <param name="path">模块路径</param>
        /// <returns>模块对象</returns>
        /// <remarks>
        /// 模块会被缓存，相同路径只加载一次
        /// 支持循环依赖检测
        /// </remarks>
        internal MiniPandaModule GetOrCreateModule(string path)
        {
            // 检查缓存
            if (_moduleCache.TryGetValue(path, out var cached))
                return cached;

            // 循环依赖检测
            if (_loadingModules.Contains(path))
                throw new MiniPandaRuntimeException($"Circular dependency detected: {path}");

            _loadingModules.Add(path);
            try
            {
                // 加载并编译模块脚本
                var script = GetOrLoadModuleScript(path);

                // 创建模块作用域
                var moduleEnv = GetScope($"module:{path}");

                // 创建模块对象（传入导出列表）
                var module = new MiniPandaModule(path, moduleEnv, script.Bytecode.Exports);
                _moduleCache[path] = module;

                // 执行模块脚本
                RunNested(script.Bytecode, moduleEnv);

                return module;
            }
            finally
            {
                _loadingModules.Remove(path);
            }
        }

        /// <summary>
        /// 获取或加载模块脚本
        /// </summary>
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

        /// <summary>
        /// 转换模块路径（点号转斜杠）
        /// </summary>
        /// <remarks>
        /// 例如：utils.math -> utils/math
        /// </remarks>
        public static string ConvertPath(string path)
        {
            return path.Replace('.', '/');
        }

        /// <summary>
        /// 默认文件加载器
        /// </summary>
        /// <remarks>
        /// 按顺序尝试 .mpbc（字节码）和 .panda（源代码）扩展名
        /// </remarks>
        private (byte[] data, string fullPath) DefaultLoadFile(string path)
        {
            // 安全检查：防止路径遍历攻击
            if (path.Contains("..") || Path.IsPathRooted(path))
                return (null, null);

            foreach (var ext in new[] {".mpbc", ".panda"})
            {
                var fullPath = path + ext;
                if (File.Exists(fullPath))
                    return (File.ReadAllBytes(fullPath), fullPath);
            }

            if (File.Exists(path))
                return (File.ReadAllBytes(path), path);
            return (null, null);
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearCache()
        {
            _scriptCache.Clear();
            _evalCache.Clear();
            _moduleScriptCache.Clear();
            _moduleCache.Clear();
        }

        #endregion

        #region 底层执行

        /// <summary>
        /// 执行字节码
        /// </summary>
        /// <param name="bytecode">字节码对象</param>
        /// <param name="scope">执行作用域</param>
        /// <returns>执行结果</returns>
        /// <remarks>
        /// 这是主要的执行入口，会重置 VM 状态后执行
        /// </remarks>
        public Value RunBytecode(Bytecode bytecode, Environment scope)
        {
            // 调试日志
            // UnityEngine.Debug.Log($"[MiniPanda VM] RunBytecode: Debugger={Debugger != null}, Enabled={Debugger?.Enabled}, SourceFile={bytecode.SourceFile}");

            // 重置 VM 状态
            _stackTop = 0;
            _frameCount = 0;
            _openUpvalues = null;

            var runScope = scope ?? _globalScope;

            // 创建主函数并执行
            var mainFunc = new MiniPandaFunction(
                new FunctionPrototype {Name = "<main>", Arity = 0, Code = bytecode},
                runScope
            );

            Push(Value.FromObject(mainFunc));
            CallValue(Value.FromObject(mainFunc), 0);

            return Execute();
        }

        /// <summary>
        /// 嵌套执行字节码（不重置 VM 状态）
        /// </summary>
        /// <param name="bytecode">字节码对象</param>
        /// <param name="scope">执行作用域</param>
        /// <returns>执行结果</returns>
        /// <remarks>
        /// 用于模块导入等需要保持当前执行状态的场景
        /// 执行完成后会恢复之前的 VM 状态
        /// </remarks>
        public Value RunNested(Bytecode bytecode, Environment scope)
        {
            // 保存当前状态
            var savedStackTop = _stackTop;
            var savedFrameCount = _frameCount;
            var savedUpvalues = _openUpvalues;

            // 保存可能被覆盖的调用帧
            var savedFrames = new CallFrame[_frameCount];
            for (int i = 0; i < _frameCount; i++)
                savedFrames[i] = _frames[i];

            // 保存可能被覆盖的栈数据
            var savedStack = new Value[_stackTop];
            for (int i = 0; i < _stackTop; i++)
                savedStack[i] = _stack[i];

            try
            {
                // 重置状态进行嵌套执行
                _stackTop = 0;
                _frameCount = 0;
                _openUpvalues = null;

                var mainFunc = new MiniPandaFunction(
                    new FunctionPrototype {Name = "<module>", Arity = 0, Code = bytecode},
                    scope
                );

                Push(Value.FromObject(mainFunc));
                CallValue(Value.FromObject(mainFunc), 0);

                return Execute();
            }
            finally
            {
                // 恢复状态
                _stackTop = savedStackTop;
                _frameCount = savedFrameCount;
                _openUpvalues = savedUpvalues;

                for (int i = 0; i < savedFrameCount; i++)
                    _frames[i] = savedFrames[i];

                for (int i = 0; i < savedStackTop; i++)
                    _stack[i] = savedStack[i];
            }
        }

        /// <summary>
        /// 调用 MiniPanda 函数
        /// </summary>
        /// <param name="function">函数对象</param>
        /// <param name="args">参数数组</param>
        /// <returns>函数返回值</returns>
        public Value CallFunction(MiniPandaFunction function, Value[] args)
        {
            var hasRestParam = function.Prototype.RestParam != null;
            // 无可变参数：参数数量必须匹配
            // 有可变参数：允许任意数量 >= Arity
            if (!hasRestParam && args.Length != function.Arity)
                throw new MiniPandaRuntimeException($"Expected {function.Arity} arguments but got {args.Length}");
            if (hasRestParam && args.Length < function.Arity)
                throw new MiniPandaRuntimeException($"Expected at least {function.Arity} arguments but got {args.Length}");
            if (_frameCount >= FramesMax)
                throw new MiniPandaRuntimeException("Stack overflow");

            // 压入函数（槽位 0）或绑定的实例
            if (function.BoundInstance != null)
            {
                Push(Value.FromObject(function.BoundInstance));
            }
            else
            {
                Push(Value.FromObject(function));
            }

            var stackBase = _stackTop - 1;

            // 处理可变参数
            if (hasRestParam)
            {
                // 压入普通参数
                for (int i = 0; i < function.Arity; i++)
                {
                    Push(i < args.Length ? args[i] : Value.Null);
                }
                // 收集剩余参数到数组
                var restArray = new MiniPandaArray();
                for (int i = function.Arity; i < args.Length; i++)
                {
                    restArray.Elements.Add(args[i]);
                }
                Push(Value.FromObject(restArray));
            }
            else
            {
                // 压入参数
                foreach (var arg in args)
                {
                    Push(arg);
                }
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

        /// <summary>
        /// 调用实例方法
        /// </summary>
        public Value CallMethod(MiniPandaInstance instance, MiniPandaFunction method, Value[] args)
        {
            var boundMethod = method.Bind(instance);
            return CallFunction(boundMethod, args);
        }

        /// <summary>
        /// 执行当前调用帧
        /// </summary>
        private Value Execute()
        {
            ref var frame = ref _frames[_frameCount - 1];

            try
            {
                return ExecuteInternal(ref frame);
            }
            catch (MiniPandaRuntimeException ex) when (ex.PandaStackTrace.Count == 0)
            {
                // 添加栈跟踪信息
                throw new MiniPandaRuntimeException(ex.Message, GetPandaStackTrace());
            }
            catch (MiniPandaRuntimeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 包装其他异常
                throw new MiniPandaRuntimeException(ex.Message, GetPandaStackTrace());
            }
        }

        /// <summary>
        /// 获取当前执行位置
        /// </summary>
        /// <returns>格式化的位置字符串</returns>
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

        /// <summary>
        /// 构建 Panda 栈跟踪
        /// </summary>
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

        /// <summary>
        /// 字节码执行主循环
        /// </summary>
        /// <param name="frame">当前调用帧</param>
        /// <returns>执行结果值</returns>
        /// <remarks>
        /// 这是 VM 的核心执行循环，负责：
        /// 1. 读取并解码指令
        /// 2. 执行对应的操作
        /// 3. 管理栈和调用帧
        /// </remarks>
        private Value ExecuteInternal(ref CallFrame frame)
        {
            // 上一次检查的行号（避免同一行重复触发断点）
            int lastDebugLine = -1;

            // 主执行循环
            while (true)
            {
                // 调试钩子：检查断点和单步执行
                if (Debugger != null && Debugger.Enabled)
                {
                    var ip = frame.IP;
                    if (ip < frame.Bytecode.Lines.Count)
                    {
                        var line = frame.Bytecode.Lines[ip];
                        if (line != lastDebugLine)
                        {
                            lastDebugLine = line;
                            var file = frame.Bytecode.SourceFile ?? "<script>";
                            if (Debugger.ShouldStop(file, line, _frameCount, out var reason))
                            {
                                // UnityEngine.Debug.Log($"[MiniPanda VM] Stopping at {file}:{line}, reason={reason}");
                                Debugger.OnStopped(reason, file, line);
                                // 等待调试器继续
                                while (Debugger.IsPaused)
                                {
                                    System.Threading.Thread.Sleep(10);
                                }
                            }
                        }
                    }
                }

                // 读取下一条指令
                var op = (Opcode) frame.Bytecode.Code[frame.IP++];

                // 根据操作码分发执行
                switch (op)
                {
                    // ==================== 常量和字面量 ====================

                    case Opcode.Const:
                    {
                        // 从常量池加载常量并压栈
                        var index = ReadShort(ref frame);
                        Push(ToValue(frame.Bytecode.Constants[index]));
                        break;
                    }

                    case Opcode.Null: Push(Value.Null); break;
                    case Opcode.True: Push(Value.True); break;
                    case Opcode.False: Push(Value.False); break;

                    // ==================== 栈操作 ====================

                    case Opcode.Pop: Pop(); break;
                    case Opcode.Dup: Push(Peek(0)); break;

                    case Opcode.Swap:
                    {
                        // 交换栈顶两个元素
                        var a = Pop();
                        var b = Pop();
                        Push(a);
                        Push(b);
                        break;
                    }

                    case Opcode.Dup2:
                    {
                        // 复制栈顶两个元素
                        var a = Peek(1);
                        var b = Peek(0);
                        Push(a);
                        Push(b);
                        break;
                    }

                    case Opcode.SwapUnder:
                    {
                        // 交换栈中较深位置的两个元素
                        var top = _stackTop - 1;
                        var temp = _stack[top - 2];
                        _stack[top - 2] = _stack[top - 3];
                        _stack[top - 3] = temp;
                        break;
                    }

                    case Opcode.Rot3Under:
                    {
                        // 三元素旋转
                        var top = _stackTop - 1;
                        var a = _stack[top - 3];
                        var b = _stack[top - 2];
                        var c = _stack[top - 1];
                        _stack[top - 3] = c;
                        _stack[top - 2] = a;
                        _stack[top - 1] = b;
                        break;
                    }

                    // ==================== 局部变量 ====================

                    case Opcode.GetLocal:
                    {
                        // 读取局部变量
                        var slot = frame.Bytecode.Code[frame.IP++];
                        Push(_stack[frame.StackBase + slot]);
                        break;
                    }

                    case Opcode.SetLocal:
                    {
                        // 设置局部变量（不弹出栈顶值）
                        var slot = frame.Bytecode.Code[frame.IP++];
                        _stack[frame.StackBase + slot] = Peek(0);
                        break;
                    }

                    // ==================== 上值（闭包变量） ====================

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
                        // 关闭上值（将栈上的值复制到堆上）
                        CloseUpvalues(_stackTop - 1);
                        Pop();
                        break;
                    }

                    // ==================== 全局变量 ====================

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
                        // 在当前作用域定义全局变量
                        var index = ReadShort(ref frame);
                        var name = frame.Bytecode.Constants[index] as string;
                        var globals = ResolveGlobals(ref frame);
                        globals.Define(name, Pop());
                        break;
                    }

                    case Opcode.DefineRootGlobal:
                    {
                        // 在根全局作用域定义变量（用于 global 关键字）
                        var index = ReadShort(ref frame);
                        var name = frame.Bytecode.Constants[index] as string;
                        _globalScope.Define(name, Pop());
                        break;
                    }

                    // ==================== 算术运算 ====================

                    case Opcode.Add: BinaryOp((a, b) => a + b, (a, b) => a + b); break;
                    case Opcode.Sub: BinaryOp((a, b) => a - b); break;
                    case Opcode.Mul: BinaryOp((a, b) => a * b); break;
                    case Opcode.Div: BinaryOp((a, b) => a / b); break;
                    case Opcode.Mod: BinaryOp((a, b) => a % b); break;

                    // ==================== 位运算 ====================

                    case Opcode.BitAnd: BitwiseOp((a, b) => a & b); break;
                    case Opcode.BitOr: BitwiseOp((a, b) => a | b); break;
                    case Opcode.BitXor: BitwiseOp((a, b) => a ^ b); break;
                    case Opcode.Shl: BitwiseOp((a, b) => a << (int)b); break;
                    case Opcode.Shr: BitwiseOp((a, b) => a >> (int)b); break;
                    case Opcode.BitNot:
                    {
                        var val = Pop();
                        Push(Value.FromNumber(~(long)val.AsNumber()));
                        break;
                    }

                    // ==================== 一元运算 ====================

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

                    // ==================== 比较运算 ====================

                    case Opcode.Eq:
                    {
                        var b = Pop();
                        var a = Pop();
                        Push(Value.FromBool(a == b));
                        break;
                    }
                    case Opcode.Ne:
                    {
                        var b = Pop();
                        var a = Pop();
                        Push(Value.FromBool(a != b));
                        break;
                    }
                    case Opcode.Lt: CompareOp((a, b) => a < b); break;
                    case Opcode.Le: CompareOp((a, b) => a <= b); break;
                    case Opcode.Gt: CompareOp((a, b) => a > b); break;
                    case Opcode.Ge: CompareOp((a, b) => a >= b); break;

                    // ==================== 跳转指令 ====================

                    case Opcode.Jump:
                    {
                        // 无条件跳转
                        var offset = ReadShort(ref frame);
                        frame.IP += offset;
                        break;
                    }

                    case Opcode.JumpIfFalse:
                    {
                        // 条件为假时跳转
                        var offset = ReadShort(ref frame);
                        if (!Peek(0).AsBool()) frame.IP += offset;
                        break;
                    }

                    case Opcode.JumpIfTrue:
                    {
                        // 条件为真时跳转
                        var offset = ReadShort(ref frame);
                        if (Peek(0).AsBool()) frame.IP += offset;
                        break;
                    }

                    case Opcode.JumpIfNotNull:
                    {
                        // 非空时跳转（用于空值合并运算符）
                        var offset = ReadShort(ref frame);
                        if (!Peek(0).IsNull) frame.IP += offset;
                        break;
                    }

                    case Opcode.Loop:
                    {
                        // 向后跳转（循环）
                        var offset = ReadShort(ref frame);
                        frame.IP -= offset;
                        break;
                    }

                    // ==================== 函数调用 ====================

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

                        // 更新帧引用（可能已切换到新帧）
                        frame = ref _frames[_frameCount - 1];
                        break;
                    }

                    case Opcode.Return:
                    {
                        var result = Pop();
                        // 关闭当前帧的所有上值
                        CloseUpvalues(frame.StackBase);
                        _frameCount--;

                        if (_frameCount == 0)
                        {
                            // 主函数返回
                            _stackTop = 0;
                            return result;
                        }

                        // 恢复调用者的栈状态
                        _stackTop = frame.StackBase;
                        Push(result);
                        frame = ref _frames[_frameCount - 1];
                        break;
                    }

                    // ==================== 闭包和对象 ====================

                    case Opcode.Closure:
                    {
                        // 创建闭包函数
                        var index = ReadShort(ref frame);
                        var prototype = frame.Bytecode.Constants[index] as FunctionPrototype;
                        var function = new MiniPandaFunction(prototype, frame.Function?.Closure ?? _globalScope);
                        // 捕获上值
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
                        // 创建数组
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
                        // 创建空对象
                        Push(Value.FromObject(new MiniPandaObject()));
                        break;
                    }

                    // ==================== 字段访问 ====================

                    case Opcode.GetField:
                    {
                        // 获取对象字段（用于对象字面量）
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
                        // 设置对象字段
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

                    // ==================== 索引访问 ====================

                    case Opcode.GetIndex:
                    {
                        // 获取数组/对象索引
                        var index = Pop();
                        var obj = Pop();
                        if (obj.As<MiniPandaArray>() is { } array)
                        {
                            Push(array.Get((int) index.AsNumber()));
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
                        // 设置数组/对象索引
                        var value = Pop();
                        var index = Pop();
                        var obj = Pop();
                        if (obj.As<MiniPandaArray>() is { } array)
                        {
                            array.Set((int) index.AsNumber(), value);
                        }
                        else if (obj.As<MiniPandaObject>() is { } dict)
                        {
                            dict.Set(index.AsString(), value);
                        }

                        Push(value);
                        break;
                    }

                    // ==================== 属性访问 ====================

                    case Opcode.GetProperty:
                    {
                        // 获取实例属性
                        var index = ReadShort(ref frame);
                        var name = frame.Bytecode.Constants[index] as string;
                        var obj = Pop();

                        if (obj.As<MiniPandaInstance>() is { } instance)
                        {
                            Push(instance.Get(name));
                        }
                        else if (obj.As<MiniPandaClass>() is { } klass)
                        {
                            // 访问类的静态成员
                            Push(klass.GetStatic(name));
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
                        // 设置实例属性
                        var index = ReadShort(ref frame);
                        var name = frame.Bytecode.Constants[index] as string;
                        var value = Pop();
                        var obj = Pop();

                        if (obj.As<MiniPandaInstance>() is { } instance)
                        {
                            instance.Set(name, value);
                        }
                        else if (obj.As<MiniPandaClass>() is { } klass)
                        {
                            // 设置类的静态字段
                            klass.SetStatic(name, value);
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

                    // ==================== 类和继承 ====================

                    case Opcode.Class:
                    {
                        // 创建类
                        var index = ReadShort(ref frame);
                        var name = frame.Bytecode.Constants[index] as string;
                        Push(Value.FromObject(new MiniPandaClass(name)));
                        break;
                    }

                    case Opcode.Inherit:
                    {
                        // 继承父类
                        var subclass = Pop().As<MiniPandaClass>();
                        var superclass = Pop().As<MiniPandaClass>();
                        if (superclass != null && subclass != null)
                        {
                            subclass.SuperClass = superclass;
                            // 复制父类方法到子类
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
                        // 定义类方法
                        var index = ReadShort(ref frame);
                        var name = frame.Bytecode.Constants[index] as string;
                        var method = Pop().As<MiniPandaFunction>();
                        var klass = Peek(0).As<MiniPandaClass>();
                        if (klass != null && method != null)
                        {
                            // 构造函数使用类名
                            method.IsInitializer = name == klass.Name;
                            klass.Methods[name] = method;
                        }

                        break;
                    }

                    case Opcode.StaticMethod:
                    {
                        // 定义静态方法
                        var index = ReadShort(ref frame);
                        var name = frame.Bytecode.Constants[index] as string;
                        var method = Pop().As<MiniPandaFunction>();
                        var klass = Pop().As<MiniPandaClass>();
                        if (klass != null && method != null)
                        {
                            klass.StaticMethods[name] = method;
                        }
                        break;
                    }

                    case Opcode.StaticField:
                    {
                        // 定义静态字段
                        var index = ReadShort(ref frame);
                        var name = frame.Bytecode.Constants[index] as string;
                        var value = Pop();
                        var klass = Pop().As<MiniPandaClass>();
                        if (klass != null)
                        {
                            klass.StaticFields[name] = value;
                        }
                        break;
                    }

                    // ==================== 方法调用 ====================

                    case Opcode.Invoke:
                    {
                        // 优化的方法调用（合并 GetProperty + Call）
                        var index = ReadShort(ref frame);
                        var name = frame.Bytecode.Constants[index] as string;
                        var argCount = frame.Bytecode.Code[frame.IP++];
                        var receiver = Peek(argCount);

                        if (receiver.As<MiniPandaInstance>() is { } instance)
                        {
                            // 实例方法调用
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
                                throw new MiniPandaRuntimeException(
                                    $"'{name}' is not callable on instance of '{instance.Class.Name}'");
                            }
                        }
                        else if (receiver.As<MiniPandaModule>() is { } module)
                        {
                            // 模块函数调用
                            var member = module.GetMember(name);
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
                                Pop(); // 弹出模块
                                var result = native.Call(this, args);
                                Push(result);
                            }
                            else if (member.As<MiniPandaClass>() is { } klass)
                            {
                                // 模块中的类实例化
                                _stack[_stackTop - argCount - 1] = Value.FromObject(klass);
                                CallValue(member, argCount);
                                frame = ref _frames[_frameCount - 1];
                            }
                            else
                            {
                                throw new MiniPandaRuntimeException(
                                    $"'{name}' is not a function in module '{module.Path}'");
                            }
                        }
                        else if (receiver.As<MiniPandaClass>() is { } klass)
                        {
                            // 类静态方法调用
                            var member = klass.GetStatic(name);
                            if (member.As<MiniPandaFunction>() is { } func)
                            {
                                _stack[_stackTop - argCount - 1] = Value.FromObject(func);
                                Call(func, argCount);
                                frame = ref _frames[_frameCount - 1];
                            }
                            else
                            {
                                throw new MiniPandaRuntimeException(
                                    $"'{name}' is not a static method on class '{klass.Name}'");
                            }
                        }
                        else if (receiver.As<MiniPandaGlobalTable>() is { } globalTable)
                        {
                            // 全局表函数调用
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
                                Pop();
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
                        else if (receiver.As<MiniPandaObject>() is { } obj)
                        {
                            // 对象方法调用
                            var member = obj.Get(name);
                            if (member.As<NativeFunction>() is { } native)
                            {
                                var args = new Value[argCount];
                                for (int i = argCount - 1; i >= 0; i--)
                                    args[i] = Pop();
                                Pop();
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
                                throw new MiniPandaRuntimeException($"'{name}' is not callable on object");
                            }
                        }
                        else
                        {
                            var typeName = receiver.AsObject()?.GetType().Name ?? receiver.Type.ToString();
                            throw new MiniPandaRuntimeException(
                                $"Cannot invoke '{name}' on value of type '{typeName}'");
                        }

                        break;
                    }

                    // ==================== this 和 super ====================

                    case Opcode.This:
                    {
                        // 获取当前实例（槽位 0）
                        Push(_stack[frame.StackBase]);
                        break;
                    }

                    case Opcode.GetSuper:
                    {
                        // 获取父类方法
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

                    // ==================== 字符串插值 ====================

                    case Opcode.BuildString:
                    {
                        // 构建插值字符串
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

                    // ==================== 迭代器 ====================

                    case Opcode.GetIter:
                    {
                        // 获取迭代器
                        var iterable = Pop();
                        if (iterable.As<MiniPandaArray>() is { } array)
                        {
                            Push(Value.FromObject(new ArrayIterator(array)));
                        }
                        else if (iterable.As<MiniPandaObject>() is { } obj)
                        {
                            Push(Value.FromObject(new ObjectIterator(obj)));
                        }
                        else if (iterable.As<MiniPandaString>() is { } str)
                        {
                            Push(Value.FromObject(new StringIterator(str.Value)));
                        }
                        else
                        {
                            throw new MiniPandaRuntimeException("Object is not iterable");
                        }

                        break;
                    }

                    case Opcode.ForIter:
                    {
                        // for 循环迭代（单值）
                        var offset = ReadShort(ref frame);
                        var iterValue = Peek(0);
                        var iter = iterValue.AsObject() as IIterator;

                        if (iter != null && iter.HasNext())
                        {
                            Push(iter.Next());
                        }
                        else
                        {
                            if (iter == null)
                                throw new MiniPandaRuntimeException("Object is not iterable");
                            Pop(); // 移除迭代器
                            frame.IP += offset;
                        }

                        break;
                    }

                    case Opcode.ForIterKV:
                    {
                        // for 循环迭代（键值对）
                        var offset = ReadShort(ref frame);
                        var iterValue = Peek(0);
                        var iter = iterValue.AsObject() as IIterator;

                        if (iter != null && iter.HasNext())
                        {
                            var (key, val) = iter.NextKV();
                            Push(key);
                            Push(val);
                        }
                        else
                        {
                            if (iter == null)
                                throw new MiniPandaRuntimeException("Object is not iterable");
                            Pop(); // 移除迭代器
                            frame.IP += offset;
                        }

                        break;
                    }

                    // ==================== 模块导入 ====================

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
                            // 全局导入
                            var bindName = string.IsNullOrEmpty(alias) ? GetModuleName(path) : alias;
                            _globalScope.Define(bindName, Value.FromObject(module));
                        }
                        else
                        {
                            // 局部导入
                            Push(Value.FromObject(module));
                        }

                        break;
                    }

                    // ==================== 异常处理 ====================

                    case Opcode.SetupTry:
                    {
                        // 设置 try 块
                        var catchOffset = ReadShort(ref frame);
                        var finallyOffset = ReadShort(ref frame);
                        var catchVarSlot = ReadByte(ref frame);

                        if (_handlerCount >= HandlersMax)
                            throw new MiniPandaRuntimeException("Too many nested try blocks");

                        _handlers[_handlerCount++] = new ExceptionHandler
                        {
                            CatchAddress = catchOffset,
                            FinallyAddress = finallyOffset,
                            CatchVarSlot = catchVarSlot == 255 ? -1 : catchVarSlot,
                            StackBase = _stackTop,
                            FrameCount = _frameCount
                        };
                        break;
                    }

                    case Opcode.EndTry:
                    {
                        // 结束 try 块
                        if (_handlerCount > 0)
                            _handlerCount--;
                        break;
                    }

                    case Opcode.Throw:
                    {
                        // 抛出异常
                        var exception = Pop();
                        if (!ThrowException(exception, ref frame))
                        {
                            // 没有找到处理器，传播为 C# 异常
                            var msg = exception.AsString();
                            throw new MiniPandaRuntimeException(msg);
                        }
                        break;
                    }

                    case Opcode.EndFinally:
                    {
                        // 结束 finally 块
                        if (_hasPendingException)
                        {
                            // 重新抛出待处理的异常
                            _hasPendingException = false;
                            if (!ThrowException(_pendingException, ref frame))
                            {
                                var msg = _pendingException.AsString();
                                throw new MiniPandaRuntimeException(msg);
                            }
                        }
                        break;
                    }

                    default:
                        throw new MiniPandaRuntimeException($"Unknown opcode: {op}");
                }
            }
        }

        /// <summary>
        /// 调用值（函数、类、原生函数等）
        /// </summary>
        /// <param name="callee">被调用的值</param>
        /// <param name="argCount">参数数量</param>
        /// <returns>调用是否成功</returns>
        private bool CallValue(Value callee, int argCount)
        {
            if (callee.As<MiniPandaFunction>() is { } function)
            {
                // 处理绑定方法（如 super.method）
                if (function.BoundInstance != null)
                {
                    _stack[_stackTop - argCount - 1] = Value.FromObject(function.BoundInstance);
                }

                return Call(function, argCount);
            }

            if (callee.As<MiniPandaClass>() is { } klass)
            {
                // 类实例化
                var instance = new MiniPandaInstance(klass);
                _stack[_stackTop - argCount - 1] = Value.FromObject(instance);

                // 调用构造函数（使用类名）
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
                // 原生函数调用
                var args = new Value[argCount];
                for (int i = argCount - 1; i >= 0; i--)
                {
                    args[i] = Pop();
                }

                Pop(); // 弹出函数本身
                var result = native.Call(this, args);
                Push(result);
                return true;
            }

            if (callee.As<MiniPandaBoundMethod>() is { } bound)
            {
                // 绑定方法调用
                _stack[_stackTop - argCount - 1] = Value.FromObject(bound.Instance);
                return Call(bound.Method, argCount);
            }

            return false;
        }

        /// <summary>
        /// 调用 MiniPanda 函数（内部）
        /// </summary>
        /// <param name="function">函数对象</param>
        /// <param name="argCount">参数数量</param>
        /// <returns>是否成功</returns>
        private bool Call(MiniPandaFunction function, int argCount)
        {
            var hasRestParam = function.Prototype.RestParam != null;

            // 参数数量检查
            // 无可变参数：允许更少的参数（用于默认值），但不能更多
            // 有可变参数：允许任意数量 >= Arity
            if (!hasRestParam && argCount > function.Arity)
            {
                throw new MiniPandaRuntimeException($"Expected at most {function.Arity} arguments but got {argCount}");
            }

            // 填充缺失的参数为 null（用于默认参数处理）
            if (argCount < function.Arity)
            {
                while (argCount < function.Arity)
                {
                    Push(Value.Null);
                    argCount++;
                }
            }

            // 处理可变参数：将多余的参数收集到数组中
            int finalArgCount = function.Arity;
            if (hasRestParam)
            {
                int extraArgs = argCount - function.Arity;
                var restArray = new MiniPandaArray();
                // 逆序弹出并插入到数组开头以保持顺序
                for (int i = extraArgs - 1; i >= 0; i--)
                {
                    restArray.Elements.Insert(0, Pop());
                }

                Push(Value.FromObject(restArray));
                finalArgCount = function.Arity + 1; // 常规参数 + rest 数组
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
                StackBase = _stackTop - finalArgCount - 1
            };

            return true;
        }

        /// <summary>
        /// 调用实例方法（内部）
        /// </summary>
        private void CallMethod(MiniPandaInstance instance, MiniPandaFunction method, int argCount)
        {
            _stack[_stackTop - argCount - 1] = Value.FromObject(instance);
            Call(method.Bind(instance), argCount);
        }

        /// <summary>
        /// 二元运算辅助方法
        /// </summary>
        /// <param name="numOp">数值运算</param>
        /// <param name="strOp">字符串运算（可选，用于 + 运算符）</param>
        private void BinaryOp(Func<double, double, double> numOp, Func<string, string, string> strOp = null)
        {
            var b = Pop();
            var a = Pop();

            // 如果任一操作数是字符串且提供了字符串运算，则进行字符串运算
            if (strOp != null && (a.IsString || b.IsString))
            {
                Push(Value.FromObject(new MiniPandaString(strOp(a.AsString(), b.AsString()))));
            }
            else
            {
                Push(Value.FromNumber(numOp(a.AsNumber(), b.AsNumber())));
            }
        }

        /// <summary>
        /// 位运算辅助方法
        /// </summary>
        private void BitwiseOp(Func<long, long, long> op)
        {
            var b = (long)Pop().AsNumber();
            var a = (long)Pop().AsNumber();
            Push(Value.FromNumber(op(a, b)));
        }

        /// <summary>
        /// 比较运算辅助方法
        /// </summary>
        private void CompareOp(Func<double, double, bool> op)
        {
            var b = Pop();
            var a = Pop();
            Push(Value.FromBool(op(a.AsNumber(), b.AsNumber())));
        }

        /// <summary>
        /// 抛出异常并查找处理器
        /// </summary>
        /// <param name="exception">异常值</param>
        /// <param name="frame">当前调用帧</param>
        /// <returns>是否找到处理器</returns>
        private bool ThrowException(Value exception, ref CallFrame frame)
        {
            // 遍历异常处理器栈
            while (_handlerCount > 0)
            {
                var handler = _handlers[--_handlerCount];

                // 展开栈到处理器的栈基址
                _stackTop = handler.StackBase;

                // 展开调用帧
                while (_frameCount > handler.FrameCount)
                {
                    CloseUpvalues(_frames[_frameCount - 1].StackBase);
                    _frameCount--;
                }

                if (_frameCount == 0)
                    return false;

                frame = ref _frames[_frameCount - 1];

                // 如果有 catch 块
                if (handler.CatchAddress != handler.FinallyAddress)
                {
                    frame.IP = handler.CatchAddress;
                    // 压入异常供 catch 变量使用
                    Push(exception);
                    return true;
                }

                // 只有 finally 块 - 保存异常并执行 finally
                if (handler.FinallyAddress > 0)
                {
                    _pendingException = exception;
                    _hasPendingException = true;
                    frame.IP = handler.FinallyAddress;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 解析全局作用域
        /// </summary>
        /// <remarks>
        /// 优先使用函数的闭包作用域，实现模块/全局隔离
        /// </remarks>
        private Environment ResolveGlobals(ref CallFrame frame)
        {
            return frame.Function?.Closure ?? _globalScope;
        }

        /// <summary>读取 16 位无符号整数</summary>
        private ushort ReadShort(ref CallFrame frame)
        {
            var high = frame.Bytecode.Code[frame.IP++];
            var low = frame.Bytecode.Code[frame.IP++];
            return (ushort) ((high << 8) | low);
        }

        /// <summary>读取单字节</summary>
        private byte ReadByte(ref CallFrame frame)
        {
            return frame.Bytecode.Code[frame.IP++];
        }

        /// <summary>从路径中提取模块名</summary>
        private static string GetModuleName(string path)
        {
            var lastDot = path.LastIndexOf('.');
            return lastDot >= 0 ? path.Substring(lastDot + 1) : path;
        }

        /// <summary>
        /// 捕获上值
        /// </summary>
        /// <param name="index">栈索引</param>
        /// <returns>上值对象</returns>
        /// <remarks>
        /// 上值链表按索引降序排列，便于快速查找和关闭
        /// </remarks>
        private Upvalue CaptureUpvalue(int index)
        {
            Upvalue previous = null;
            var upvalue = _openUpvalues;
            // 查找已存在的上值或插入位置
            while (upvalue != null && upvalue.Index > index)
            {
                previous = upvalue;
                upvalue = upvalue.Next;
            }

            // 如果已存在，直接返回
            if (upvalue != null && upvalue.Index == index)
            {
                return upvalue;
            }

            // 创建新上值并插入链表
            var created = new Upvalue {Index = index, Next = upvalue};
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

        /// <summary>
        /// 关闭指定位置及以上的所有上值
        /// </summary>
        /// <param name="last">最低栈位置</param>
        /// <remarks>
        /// 将栈上的值复制到堆上，使闭包在函数返回后仍能访问
        /// </remarks>
        private void CloseUpvalues(int last)
        {
            while (_openUpvalues != null && _openUpvalues.Index >= last)
            {
                var upvalue = _openUpvalues;
                upvalue.Close(_stack);
                _openUpvalues = upvalue.Next;
            }
        }

        // ==================== 栈操作 ====================

        /// <summary>压栈</summary>
        private void Push(Value value)
        {
            if (_stackTop >= StackMax)
                throw new MiniPandaRuntimeException("Stack overflow");
            _stack[_stackTop++] = value;
        }

        /// <summary>弹栈</summary>
        private Value Pop() => _stack[--_stackTop];

        /// <summary>查看栈顶元素（不弹出）</summary>
        private Value Peek(int distance) => _stack[_stackTop - 1 - distance];

        /// <summary>
        /// 将常量池对象转换为 Value
        /// </summary>
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

        /// <summary>
        /// 将 C# 对象转换为 Value
        /// </summary>
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

    /// <summary>
    /// 上值（Upvalue）- 用于实现闭包
    /// </summary>
    /// <remarks>
    /// 上值是闭包捕获的外部变量的引用。
    /// 当变量还在栈上时，上值指向栈位置；
    /// 当变量离开作用域后，上值会"关闭"，将值复制到自身。
    /// </remarks>
    public sealed class Upvalue
    {
        /// <summary>栈索引（未关闭时有效）</summary>
        public int Index;
        /// <summary>关闭后的值</summary>
        public Value Closed;
        /// <summary>是否已关闭</summary>
        public bool IsClosed;
        /// <summary>链表中的下一个上值</summary>
        public Upvalue Next;

        /// <summary>获取上值的当前值</summary>
        public Value Get(Value[] stack) => IsClosed ? Closed : stack[Index];

        /// <summary>设置上值的值</summary>
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

        /// <summary>关闭上值（将栈上的值复制到堆上）</summary>
        public void Close(Value[] stack)
        {
            if (IsClosed) return;
            Closed = stack[Index];
            IsClosed = true;
        }
    }

    /// <summary>
    /// 迭代器接口
    /// </summary>
    internal interface IIterator
    {
        /// <summary>是否还有下一个元素</summary>
        bool HasNext();
        /// <summary>获取下一个值</summary>
        Value Next();
        /// <summary>获取下一个键值对</summary>
        (Value Key, Value Val) NextKV();
    }

    /// <summary>
    /// 数组迭代器
    /// </summary>
    internal class ArrayIterator : MiniPandaHeapObject, IIterator
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

        public (Value Key, Value Val) NextKV()
        {
            var key = Value.FromNumber(_index);
            var val = _array.Get(_index++);
            return (key, val);
        }
    }

    /// <summary>
    /// 对象/字典迭代器
    /// </summary>
    internal class ObjectIterator : MiniPandaHeapObject, IIterator
    {
        private readonly List<string> _keys;
        private readonly MiniPandaObject _obj;
        private int _index;

        public ObjectIterator(MiniPandaObject obj)
        {
            _obj = obj;
            _keys = new List<string>(obj.Fields.Keys);
            _index = 0;
        }

        public bool HasNext() => _index < _keys.Count;
        public Value Next() => Value.FromObject(new MiniPandaString(_keys[_index++]));

        public (Value Key, Value Val) NextKV()
        {
            var key = _keys[_index++];
            return (Value.FromObject(new MiniPandaString(key)), _obj.Fields[key]);
        }
    }

    /// <summary>
    /// 字符串迭代器
    /// </summary>
    internal class StringIterator : MiniPandaHeapObject, IIterator
    {
        private readonly string _str;
        private int _index;

        public StringIterator(string str)
        {
            _str = str;
            _index = 0;
        }

        public bool HasNext() => _index < _str.Length;
        public Value Next() => Value.FromObject(new MiniPandaString(_str[_index++].ToString()));

        public (Value Key, Value Val) NextKV()
        {
            var key = Value.FromNumber(_index);
            var val = Value.FromObject(new MiniPandaString(_str[_index++].ToString()));
            return (key, val);
        }
    }
}