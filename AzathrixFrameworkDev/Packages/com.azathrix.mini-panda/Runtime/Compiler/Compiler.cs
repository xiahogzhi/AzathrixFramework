using System;
using System.Collections.Generic;
using Azathrix.MiniPanda.Lexer;
using Azathrix.MiniPanda.Parser;

namespace Azathrix.MiniPanda.Compiler
{
    public class Compiler
    {
        private Bytecode _bytecode;
        private readonly List<Local> _locals = new List<Local>();
        private int _scopeDepth;
        private readonly List<LoopInfo> _loops = new List<LoopInfo>();
        private FunctionType _functionType;
        private Compiler _enclosing;
        private readonly List<Upvalue> _upvalues = new List<Upvalue>();
        private string _sourceFile;

        private struct Local
        {
            public string Name;
            public int Depth;
            public bool IsCaptured;
        }

        private struct Upvalue
        {
            public int Index;
            public bool IsLocal;
        }

        private struct CompiledFunction
        {
            public FunctionPrototype Prototype;
            public List<Upvalue> Upvalues;
        }

        private struct LoopInfo
        {
            public int Start;
            public int LocalCount;
            public int IterLocalCount;
            public bool IsFor;
            public List<int> Breaks;
        }

        public enum FunctionType { Script, Function, Method, Initializer }

        /// <summary>
        /// Source file path for debug info.
        /// </summary>
        public string SourceFile
        {
            get => _sourceFile ?? _enclosing?.SourceFile;
            set => _sourceFile = value;
        }

        public Compiler(FunctionType type = FunctionType.Script, Compiler enclosing = null)
        {
            _functionType = type;
            _enclosing = enclosing;
            _bytecode = new Bytecode();

            // Reserve slot 0: 'this' for methods/initializers, empty for scripts/functions
            var slot0Name = (type == FunctionType.Method || type == FunctionType.Initializer) ? "this" : "";
            _locals.Add(new Local { Name = slot0Name, Depth = 0 });
        }

        public Bytecode Compile(List<Stmt> statements)
        {
            foreach (var stmt in statements)
            {
                CompileStmt(stmt);
            }
            Emit(Opcode.Null, 0);
            Emit(Opcode.Return, 0);
            _bytecode.SourceFile = SourceFile;
            return _bytecode;
        }

        private CompiledFunction CompileFunction(string name, List<string> parameters, List<Stmt> body, FunctionType type = FunctionType.Function, string className = null)
        {
            var compiler = new Compiler(type, this);

            foreach (var param in parameters)
            {
                compiler.AddLocal(param);
            }

            foreach (var stmt in body)
            {
                compiler.CompileStmt(stmt);
            }

            // Implicit return null (or this for initializers)
            if (type == FunctionType.Initializer)
            {
                compiler.Emit(Opcode.GetLocal, 0);
                compiler.EmitByte(0, 0); // slot 0 = this
            }
            else
            {
                compiler.Emit(Opcode.Null, 0);
            }
            compiler.Emit(Opcode.Return, 0);

            // Propagate source file to nested function
            compiler._bytecode.SourceFile = SourceFile;

            var prototype = new FunctionPrototype
            {
                Name = name,
                ClassName = className,
                Arity = parameters.Count,
                Code = compiler._bytecode,
                UpvalueCount = compiler._upvalues.Count
            };

            return new CompiledFunction
            {
                Prototype = prototype,
                Upvalues = compiler._upvalues
            };
        }

        private void CompileStmt(Stmt stmt)
        {
            switch (stmt)
            {
                case ExpressionStmt s: CompileExpressionStmt(s); break;
                case VarDecl s: CompileVarDecl(s); break;
                case FuncDecl s: CompileFuncDecl(s); break;
                case ClassDecl s: CompileClassDecl(s); break;
                case IfStmt s: CompileIfStmt(s); break;
                case WhileStmt s: CompileWhileStmt(s); break;
                case ForStmt s: CompileForStmt(s); break;
                case ReturnStmt s: CompileReturnStmt(s); break;
                case BreakStmt s: CompileBreakStmt(s); break;
                case ContinueStmt s: CompileContinueStmt(s); break;
                case BlockStmt s: CompileBlockStmt(s); break;
                case ImportStmt s: CompileImportStmt(s); break;
                default: throw new CompilerException($"Unknown statement type: {stmt.GetType().Name}");
            }
        }

        private void CompileExpressionStmt(ExpressionStmt stmt)
        {
            CompileExpr(stmt.Expression);
            Emit(Opcode.Pop, stmt.Line);
        }

        private void CompileVarDecl(VarDecl stmt)
        {
            if (stmt.IsGlobal)
            {
                // Root global variable (global var x = ...)
                var nameIndex = _bytecode.AddConstant(stmt.Name);
                if (stmt.Initializer != null)
                {
                    CompileExpr(stmt.Initializer);
                }
                else
                {
                    Emit(Opcode.Null, stmt.Line);
                }
                Emit(Opcode.DefineRootGlobal, stmt.Line);
                EmitShort((ushort)nameIndex, stmt.Line);
            }
            else if (_scopeDepth > 0)
            {
                // Local variable
                AddLocal(stmt.Name);
                if (stmt.Initializer != null)
                {
                    CompileExpr(stmt.Initializer);
                }
                else
                {
                    Emit(Opcode.Null, stmt.Line);
                }
            }
            else
            {
                // Global variable
                var nameIndex = _bytecode.AddConstant(stmt.Name);
                if (stmt.Initializer != null)
                {
                    CompileExpr(stmt.Initializer);
                }
                else
                {
                    Emit(Opcode.Null, stmt.Line);
                }
                Emit(Opcode.DefineGlobal, stmt.Line);
                EmitShort((ushort)nameIndex, stmt.Line);
            }
        }

        private void CompileFuncDecl(FuncDecl stmt)
        {
            var compiled = CompileFunction(stmt.Name, stmt.Parameters, stmt.Body);
            var index = _bytecode.AddConstant(compiled.Prototype);

            Emit(Opcode.Closure, stmt.Line);
            EmitShort((ushort)index, stmt.Line);
            EmitUpvalueInfo(compiled.Upvalues, stmt.Line);

            if (stmt.IsGlobal)
            {
                var nameIndex = _bytecode.AddConstant(stmt.Name);
                Emit(Opcode.DefineRootGlobal, stmt.Line);
                EmitShort((ushort)nameIndex, stmt.Line);
            }
            else if (_scopeDepth > 0)
            {
                AddLocal(stmt.Name);
            }
            else
            {
                var nameIndex = _bytecode.AddConstant(stmt.Name);
                Emit(Opcode.DefineGlobal, stmt.Line);
                EmitShort((ushort)nameIndex, stmt.Line);
            }
        }

        private void CompileClassDecl(ClassDecl stmt)
        {
            var nameIndex = _bytecode.AddConstant(stmt.Name);
            Emit(Opcode.Class, stmt.Line);
            EmitShort((ushort)nameIndex, stmt.Line);

            if (stmt.IsGlobal)
            {
                Emit(Opcode.DefineRootGlobal, stmt.Line);
                EmitShort((ushort)nameIndex, stmt.Line);
            }
            else if (_scopeDepth > 0)
            {
                AddLocal(stmt.Name);
            }
            else
            {
                Emit(Opcode.DefineGlobal, stmt.Line);
                EmitShort((ushort)nameIndex, stmt.Line);
            }

            // Handle inheritance
            if (stmt.SuperClass != null)
            {
                var superIndex = ResolveLocal(stmt.SuperClass);
                if (superIndex != -1)
                {
                    Emit(Opcode.GetLocal, stmt.Line);
                    EmitByte((byte)superIndex, stmt.Line);
                }
                else
                {
                    var superNameIndex = _bytecode.AddConstant(stmt.SuperClass);
                    Emit(Opcode.GetGlobal, stmt.Line);
                    EmitShort((ushort)superNameIndex, stmt.Line);
                }

                // Get the class we just defined
                if (stmt.IsGlobal || _scopeDepth == 0)
                {
                    Emit(Opcode.GetGlobal, stmt.Line);
                    EmitShort((ushort)nameIndex, stmt.Line);
                }
                else
                {
                    var classIndex = ResolveLocal(stmt.Name);
                    Emit(Opcode.GetLocal, stmt.Line);
                    EmitByte((byte)classIndex, stmt.Line);
                }

                Emit(Opcode.Inherit, stmt.Line);
            }

            // Inject field initializers into constructor
            var methods = stmt.Methods;
            var constructorName = stmt.Name; // Constructor uses class name
            if (stmt.Fields.Count > 0)
            {
                methods = new List<FuncDecl>(stmt.Methods);
                var initMethod = methods.Find(m => m.Name == constructorName);
                var fieldInits = new List<Stmt>();
                foreach (var field in stmt.Fields)
                {
                    fieldInits.Add(new ExpressionStmt
                    {
                        Expression = new SetExpr
                        {
                            Object = new ThisExpr { Line = field.Line },
                            Name = field.Name,
                            Value = field.Initializer ?? new LiteralExpr { Value = null, Line = field.Line },
                            Line = field.Line
                        },
                        Line = field.Line
                    });
                }
                if (initMethod != null)
                {
                    methods.Remove(initMethod);
                    // Check if first statement is super.ClassName() call
                    var body = initMethod.Body;
                    var newBody = new List<Stmt>();
                    int startIndex = 0;

                    // If first statement is super.ClassName(), preserve it at the beginning
                    if (body.Count > 0 && IsSuperConstructorCall(body[0], stmt.SuperClass))
                    {
                        newBody.Add(body[0]);
                        startIndex = 1;
                    }

                    // Add field initializers
                    newBody.AddRange(fieldInits);

                    // Add remaining constructor body
                    for (int i = startIndex; i < body.Count; i++)
                    {
                        newBody.Add(body[i]);
                    }

                    methods.Add(new FuncDecl { Name = constructorName, Parameters = initMethod.Parameters, Body = newBody, Line = initMethod.Line });
                }
                else
                {
                    // If subclass has no explicit constructor, call super.SuperClassName() first
                    if (stmt.SuperClass != null)
                    {
                        fieldInits.Insert(0, new ExpressionStmt
                        {
                            Expression = new CallExpr
                            {
                                Callee = new SuperExpr { Method = stmt.SuperClass, Line = stmt.Line },
                                Arguments = new List<Expr>(),
                                Line = stmt.Line
                            },
                            Line = stmt.Line
                        });
                    }
                    methods.Add(new FuncDecl { Name = constructorName, Parameters = new List<string>(), Body = fieldInits, Line = stmt.Line });
                }
            }

            // Compile methods
            foreach (var method in methods)
            {
                var methodType = method.Name == constructorName ? FunctionType.Initializer : FunctionType.Method;
                var compiled = CompileFunction(method.Name, method.Parameters, method.Body, methodType, stmt.Name);
                var methodIndex = _bytecode.AddConstant(compiled.Prototype);
                var methodNameIndex = _bytecode.AddConstant(method.Name);

                // Get the class
                if (stmt.IsGlobal || _scopeDepth == 0)
                {
                    Emit(Opcode.GetGlobal, stmt.Line);
                    EmitShort((ushort)nameIndex, stmt.Line);
                }
                else
                {
                    var classIndex = ResolveLocal(stmt.Name);
                    Emit(Opcode.GetLocal, stmt.Line);
                    EmitByte((byte)classIndex, stmt.Line);
                }

                Emit(Opcode.Closure, stmt.Line);
                EmitShort((ushort)methodIndex, stmt.Line);
                EmitUpvalueInfo(compiled.Upvalues, stmt.Line);

                Emit(Opcode.Method, stmt.Line);
                EmitShort((ushort)methodNameIndex, stmt.Line);
            }
        }

        private void CompileIfStmt(IfStmt stmt)
        {
            CompileExpr(stmt.Condition);
            var thenJump = EmitJump(Opcode.JumpIfFalse, stmt.Line);
            Emit(Opcode.Pop, stmt.Line);

            foreach (var s in stmt.ThenBranch)
            {
                CompileStmt(s);
            }

            var elseJump = EmitJump(Opcode.Jump, stmt.Line);
            PatchJump(thenJump);
            Emit(Opcode.Pop, stmt.Line);

            if (stmt.ElseBranch != null)
            {
                foreach (var s in stmt.ElseBranch)
                {
                    CompileStmt(s);
                }
            }

            PatchJump(elseJump);
        }

        private void CompileWhileStmt(WhileStmt stmt)
        {
            var loopStart = _bytecode.Code.Count;
            _loops.Add(new LoopInfo
            {
                Start = loopStart,
                LocalCount = _locals.Count,
                IterLocalCount = _locals.Count,
                IsFor = false,
                Breaks = new List<int>()
            });

            CompileExpr(stmt.Condition);
            var exitJump = EmitJump(Opcode.JumpIfFalse, stmt.Line);
            Emit(Opcode.Pop, stmt.Line);

            foreach (var s in stmt.Body)
            {
                CompileStmt(s);
            }

            EmitLoop(loopStart, stmt.Line);
            PatchJump(exitJump);
            Emit(Opcode.Pop, stmt.Line);

            var loop = _loops[_loops.Count - 1];
            foreach (var breakJump in loop.Breaks)
            {
                PatchJump(breakJump);
            }
            _loops.RemoveAt(_loops.Count - 1);
        }

        private void CompileForStmt(ForStmt stmt)
        {
            BeginScope();

            var loopBase = _locals.Count;

            // Stack: [..., iterable] -> GetIter -> [..., iterator]
            CompileExpr(stmt.Iterable);
            Emit(Opcode.GetIter, stmt.Line);
            // Reserve a local slot for the iterator so it survives across iterations.
            AddLocal("$iter");
            var iterLocalCount = _locals.Count;

            var loopStart = _bytecode.Code.Count;
            _loops.Add(new LoopInfo
            {
                Start = loopStart,
                LocalCount = loopBase,
                IterLocalCount = iterLocalCount,
                IsFor = true,
                Breaks = new List<int>()
            });

            // ForIter peeks at TOS (iterator) and pushes the next value if available.
            var exitJump = EmitJump(Opcode.ForIter, stmt.Line);

            // Bind loop variable to the value just pushed by ForIter.
            AddLocal(stmt.Variable);

            foreach (var s in stmt.Body)
            {
                CompileStmt(s);
            }

            // Pop loop variable, iterator is back at TOS
            if (_locals[_locals.Count - 1].IsCaptured)
            {
                Emit(Opcode.CloseUpvalue, stmt.Line);
            }
            else
            {
                Emit(Opcode.Pop, stmt.Line);
            }

            EmitLoop(loopStart, stmt.Line);
            PatchJump(exitJump);

            var loop = _loops[_loops.Count - 1];
            foreach (var breakJump in loop.Breaks)
            {
                PatchJump(breakJump);
            }
            _loops.RemoveAt(_loops.Count - 1);

            // Remove $iter and loop variable from locals - they were handled by ForIter
            _locals.RemoveAt(_locals.Count - 1); // loop variable
            _locals.RemoveAt(_locals.Count - 1); // $iter

            EndScope(stmt.Line);
        }

        private void CompileReturnStmt(ReturnStmt stmt)
        {
            if (stmt.Value != null)
            {
                CompileExpr(stmt.Value);
            }
            else
            {
                Emit(Opcode.Null, stmt.Line);
            }
            Emit(Opcode.Return, stmt.Line);
        }

        private void CompileBreakStmt(BreakStmt stmt)
        {
            if (_loops.Count == 0)
                throw new CompilerException("'break' outside of loop");

            var loop = _loops[_loops.Count - 1];
            EmitPopLocals(loop.LocalCount, stmt.Line);
            var jump = EmitJump(Opcode.Jump, stmt.Line);
            loop.Breaks.Add(jump);
        }

        private void CompileContinueStmt(ContinueStmt stmt)
        {
            if (_loops.Count == 0)
                throw new CompilerException("'continue' outside of loop");

            var loop = _loops[_loops.Count - 1];
            var targetLocals = loop.IsFor ? loop.IterLocalCount : loop.LocalCount;
            EmitPopLocals(targetLocals, stmt.Line);
            EmitLoop(loop.Start, stmt.Line);
        }

        private void CompileBlockStmt(BlockStmt stmt)
        {
            BeginScope();
            foreach (var s in stmt.Statements)
            {
                CompileStmt(s);
            }
            EndScope(stmt.Line);
        }

        private void CompileImportStmt(ImportStmt stmt)
        {
            var pathIndex = _bytecode.AddConstant(stmt.Path);
            var aliasIndex = _bytecode.AddConstant(stmt.Alias ?? "");
            Emit(Opcode.Import, stmt.Line);
            EmitShort((ushort)pathIndex, stmt.Line);
            EmitShort((ushort)aliasIndex, stmt.Line);
            EmitByte((byte)(stmt.IsGlobal ? 1 : 0), stmt.Line);

            // global import: VM handles binding to globals, no compiler action needed
            // local import: VM pushes module, compiler binds to local/global
            if (!stmt.IsGlobal)
            {
                var bindName = !string.IsNullOrEmpty(stmt.Alias) ? stmt.Alias : GetModuleName(stmt.Path);
                if (_scopeDepth > 0)
                {
                    AddLocal(bindName);
                }
                else
                {
                    var nameIndex = _bytecode.AddConstant(bindName);
                    Emit(Opcode.DefineGlobal, stmt.Line);
                    EmitShort((ushort)nameIndex, stmt.Line);
                }
            }
        }

        private static string GetModuleName(string path)
        {
            var lastDot = path.LastIndexOf('.');
            return lastDot >= 0 ? path.Substring(lastDot + 1) : path;
        }

        private void CompileExpr(Expr expr)
        {
            switch (expr)
            {
                case LiteralExpr e: CompileLiteral(e); break;
                case StringExpr e: CompileStringExpr(e); break;
                case IdentifierExpr e: CompileIdentifier(e); break;
                case AssignExpr e: CompileAssign(e); break;
                case BinaryExpr e: CompileBinary(e); break;
                case UnaryExpr e: CompileUnary(e); break;
                case CallExpr e: CompileCall(e); break;
                case GetExpr e: CompileGet(e); break;
                case SetExpr e: CompileSet(e); break;
                case IndexGetExpr e: CompileIndexGet(e); break;
                case IndexSetExpr e: CompileIndexSet(e); break;
                case ArrayExpr e: CompileArray(e); break;
                case ObjectExpr e: CompileObject(e); break;
                case LambdaExpr e: CompileLambda(e); break;
                case ThisExpr e: CompileThis(e); break;
                case SuperExpr e: CompileSuper(e); break;
                case TernaryExpr e: CompileTernary(e); break;
                case CompoundAssignExpr e: CompileCompoundAssign(e); break;
                case UpdateExpr e: CompileUpdate(e); break;
                default: throw new CompilerException($"Unknown expression type: {expr.GetType().Name}");
            }
        }

        private void CompileTernary(TernaryExpr expr)
        {
            CompileExpr(expr.Condition);
            var elseJump = EmitJump(Opcode.JumpIfFalse, expr.Line);
            Emit(Opcode.Pop, expr.Line);
            CompileExpr(expr.ThenExpr);
            var endJump = EmitJump(Opcode.Jump, expr.Line);
            PatchJump(elseJump);
            Emit(Opcode.Pop, expr.Line);
            CompileExpr(expr.ElseExpr);
            PatchJump(endJump);
        }

        private void EmitBinaryOp(TokenType op, int line)
        {
            switch (op)
            {
                case TokenType.Plus: Emit(Opcode.Add, line); break;
                case TokenType.Minus: Emit(Opcode.Sub, line); break;
                case TokenType.Star: Emit(Opcode.Mul, line); break;
                case TokenType.Slash: Emit(Opcode.Div, line); break;
                default: throw new CompilerException($"Unsupported operator: {op}");
            }
        }

        private void EmitNumberConst(double value, int line)
        {
            var index = _bytecode.AddConstant(value);
            Emit(Opcode.Const, line);
            EmitShort((ushort)index, line);
        }

        private void CompileCompoundAssign(CompoundAssignExpr expr)
        {
            switch (expr.Target)
            {
                case IdentifierExpr:
                    CompileExpr(expr.Target);
                    CompileExpr(expr.Value);
                    EmitBinaryOp(expr.Operator, expr.Line);
                    EmitAssignment(expr.Target, expr.Line);
                    break;
                case GetExpr get:
                    CompileExpr(get.Object);
                    Emit(Opcode.Dup, expr.Line);
                    var nameIndex = _bytecode.AddConstant(get.Name);
                    Emit(Opcode.GetProperty, expr.Line);
                    EmitShort((ushort)nameIndex, expr.Line);
                    CompileExpr(expr.Value);
                    EmitBinaryOp(expr.Operator, expr.Line);
                    Emit(Opcode.SetProperty, expr.Line);
                    EmitShort((ushort)nameIndex, expr.Line);
                    break;
                case IndexGetExpr idx:
                    CompileExpr(idx.Object);
                    CompileExpr(idx.Index);
                    Emit(Opcode.Dup2, expr.Line);
                    Emit(Opcode.GetIndex, expr.Line);
                    CompileExpr(expr.Value);
                    EmitBinaryOp(expr.Operator, expr.Line);
                    Emit(Opcode.SetIndex, expr.Line);
                    break;
                default:
                    throw new CompilerException("Invalid compound assignment target");
            }
        }

        private void CompileUpdate(UpdateExpr expr)
        {
            // ++x / x++ / --x / x--
            var delta = expr.Operator == TokenType.PlusPlus ? 1.0 : -1.0;

            switch (expr.Target)
            {
                case IdentifierExpr:
                    if (expr.IsPrefix)
                    {
                        CompileExpr(expr.Target);
                        EmitNumberConst(delta, expr.Line);
                        Emit(Opcode.Add, expr.Line);
                        EmitAssignment(expr.Target, expr.Line);
                    }
                    else
                    {
                        CompileExpr(expr.Target);
                        Emit(Opcode.Dup, expr.Line);
                        EmitNumberConst(delta, expr.Line);
                        Emit(Opcode.Add, expr.Line);
                        EmitAssignment(expr.Target, expr.Line);
                        Emit(Opcode.Pop, expr.Line);
                    }
                    break;
                case GetExpr get:
                    {
                        var nameIndex = _bytecode.AddConstant(get.Name);
                        if (expr.IsPrefix)
                        {
                            CompileExpr(get.Object);
                            Emit(Opcode.Dup, expr.Line);
                            Emit(Opcode.GetProperty, expr.Line);
                            EmitShort((ushort)nameIndex, expr.Line);
                            EmitNumberConst(delta, expr.Line);
                            Emit(Opcode.Add, expr.Line);
                            Emit(Opcode.SetProperty, expr.Line);
                            EmitShort((ushort)nameIndex, expr.Line);
                        }
                        else
                        {
                            CompileExpr(get.Object);
                            Emit(Opcode.Dup, expr.Line);
                            Emit(Opcode.GetProperty, expr.Line);
                            EmitShort((ushort)nameIndex, expr.Line);
                            Emit(Opcode.Dup, expr.Line);
                            EmitNumberConst(delta, expr.Line);
                            Emit(Opcode.Add, expr.Line);
                            Emit(Opcode.SwapUnder, expr.Line);
                            Emit(Opcode.SetProperty, expr.Line);
                            EmitShort((ushort)nameIndex, expr.Line);
                            Emit(Opcode.Pop, expr.Line);
                        }
                        break;
                    }
                case IndexGetExpr idx:
                    if (expr.IsPrefix)
                    {
                        CompileExpr(idx.Object);
                        CompileExpr(idx.Index);
                        Emit(Opcode.Dup2, expr.Line);
                        Emit(Opcode.GetIndex, expr.Line);
                        EmitNumberConst(delta, expr.Line);
                        Emit(Opcode.Add, expr.Line);
                        Emit(Opcode.SetIndex, expr.Line);
                    }
                    else
                    {
                        CompileExpr(idx.Object);
                        CompileExpr(idx.Index);
                        Emit(Opcode.Dup2, expr.Line);
                        Emit(Opcode.GetIndex, expr.Line);
                        Emit(Opcode.Dup, expr.Line);
                        EmitNumberConst(delta, expr.Line);
                        Emit(Opcode.Add, expr.Line);
                        Emit(Opcode.Rot3Under, expr.Line);
                        Emit(Opcode.SetIndex, expr.Line);
                        Emit(Opcode.Pop, expr.Line);
                    }
                    break;
                default:
                    throw new CompilerException("Invalid update target");
            }
        }

        private void EmitAssignment(Expr target, int line)
        {
            if (target is IdentifierExpr id)
            {
                var local = ResolveLocal(id.Name);
                if (local != -1)
                {
                    Emit(Opcode.SetLocal, line);
                    EmitByte((byte)local, line);
                }
                else
                {
                    var upvalue = ResolveUpvalue(id.Name);
                    if (upvalue != -1)
                    {
                        Emit(Opcode.SetUpvalue, line);
                        EmitByte((byte)upvalue, line);
                    }
                    else
                    {
                        var index = _bytecode.AddConstant(id.Name);
                        Emit(Opcode.SetGlobal, line);
                        EmitShort((ushort)index, line);
                    }
                }
            }
                        else if (target is GetExpr get)
            {
                CompileExpr(get.Object);
                Emit(Opcode.Swap, line);
                var index = _bytecode.AddConstant(get.Name);
                Emit(Opcode.SetProperty, line);
                EmitShort((ushort)index, line);
            }
            else if (target is IndexGetExpr idx)
            {
                CompileExpr(idx.Object);
                CompileExpr(idx.Index);
                Emit(Opcode.SwapUnder, line);
                Emit(Opcode.Swap, line);
                Emit(Opcode.SetIndex, line);
            }
        }

        private void CompileLiteral(LiteralExpr expr)
        {
            switch (expr.Value)
            {
                case null: Emit(Opcode.Null, expr.Line); break;
                case true: Emit(Opcode.True, expr.Line); break;
                case false: Emit(Opcode.False, expr.Line); break;
                default:
                    var index = _bytecode.AddConstant(expr.Value);
                    Emit(Opcode.Const, expr.Line);
                    EmitShort((ushort)index, expr.Line);
                    break;
            }
        }

        private void CompileStringExpr(StringExpr expr)
        {
            int partCount = 0;
            foreach (var part in expr.Parts)
            {
                if (part is string s)
                {
                    var index = _bytecode.AddConstant(s);
                    Emit(Opcode.Const, expr.Line);
                    EmitShort((ushort)index, expr.Line);
                    partCount++;
                }
                else if (part is StringInterpolation interp)
                {
                    // Parse and compile the interpolated expression
                    var lexer = new Lexer.Lexer(interp.Expression);
                    var tokens = lexer.Tokenize();
                    var parser = new Parser.Parser(tokens);
                    var stmts = parser.Parse();
                    if (stmts.Count == 1 && stmts[0] is ExpressionStmt exprStmt)
                    {
                        CompileExpr(exprStmt.Expression);
                        partCount++;
                    }
                    else
                    {
                        throw new CompilerException($"String interpolation must contain a single expression");
                    }
                }
            }
            Emit(Opcode.BuildString, expr.Line);
            EmitByte((byte)partCount, expr.Line);
        }

        private void CompileIdentifier(IdentifierExpr expr)
        {
            var local = ResolveLocal(expr.Name);
            if (local != -1)
            {
                Emit(Opcode.GetLocal, expr.Line);
                EmitByte((byte)local, expr.Line);
            }
            else
            {
                var upvalue = ResolveUpvalue(expr.Name);
                if (upvalue != -1)
                {
                    Emit(Opcode.GetUpvalue, expr.Line);
                    EmitByte((byte)upvalue, expr.Line);
                }
                else
                {
                    var index = _bytecode.AddConstant(expr.Name);
                    Emit(Opcode.GetGlobal, expr.Line);
                    EmitShort((ushort)index, expr.Line);
                }
            }
        }

        private void CompileAssign(AssignExpr expr)
        {
            CompileExpr(expr.Value);

            if (expr.Target is IdentifierExpr id)
            {
                var local = ResolveLocal(id.Name);
                if (local != -1)
                {
                    Emit(Opcode.SetLocal, expr.Line);
                    EmitByte((byte)local, expr.Line);
                }
                else
                {
                    var upvalue = ResolveUpvalue(id.Name);
                    if (upvalue != -1)
                    {
                        Emit(Opcode.SetUpvalue, expr.Line);
                        EmitByte((byte)upvalue, expr.Line);
                    }
                    else
                    {
                        var index = _bytecode.AddConstant(id.Name);
                        Emit(Opcode.SetGlobal, expr.Line);
                        EmitShort((ushort)index, expr.Line);
                    }
                }
            }
        }

        private void CompileBinary(BinaryExpr expr)
        {
            // Short-circuit for && and ||
            if (expr.Operator == TokenType.And)
            {
                CompileExpr(expr.Left);
                var endJump = EmitJump(Opcode.JumpIfFalse, expr.Line);
                Emit(Opcode.Pop, expr.Line);
                CompileExpr(expr.Right);
                PatchJump(endJump);
                return;
            }

            if (expr.Operator == TokenType.Or)
            {
                CompileExpr(expr.Left);
                var elseJump = EmitJump(Opcode.JumpIfFalse, expr.Line);
                var endJump = EmitJump(Opcode.Jump, expr.Line);
                PatchJump(elseJump);
                Emit(Opcode.Pop, expr.Line);
                CompileExpr(expr.Right);
                PatchJump(endJump);
                return;
            }

            CompileExpr(expr.Left);
            CompileExpr(expr.Right);

            switch (expr.Operator)
            {
                case TokenType.Plus: Emit(Opcode.Add, expr.Line); break;
                case TokenType.Minus: Emit(Opcode.Sub, expr.Line); break;
                case TokenType.Star: Emit(Opcode.Mul, expr.Line); break;
                case TokenType.Slash: Emit(Opcode.Div, expr.Line); break;
                case TokenType.Percent: Emit(Opcode.Mod, expr.Line); break;
                case TokenType.EqualEqual: Emit(Opcode.Eq, expr.Line); break;
                case TokenType.BangEqual: Emit(Opcode.Ne, expr.Line); break;
                case TokenType.Less: Emit(Opcode.Lt, expr.Line); break;
                case TokenType.LessEqual: Emit(Opcode.Le, expr.Line); break;
                case TokenType.Greater: Emit(Opcode.Gt, expr.Line); break;
                case TokenType.GreaterEqual: Emit(Opcode.Ge, expr.Line); break;
            }
        }

        private void CompileUnary(UnaryExpr expr)
        {
            CompileExpr(expr.Operand);
            switch (expr.Operator)
            {
                case TokenType.Minus: Emit(Opcode.Neg, expr.Line); break;
                case TokenType.Bang: Emit(Opcode.Not, expr.Line); break;
            }
        }

        private void CompileCall(CallExpr expr)
        {
            // Optimize method calls
            if (expr.Callee is GetExpr get)
            {
                CompileExpr(get.Object);
                foreach (var arg in expr.Arguments)
                {
                    CompileExpr(arg);
                }
                var nameIndex = _bytecode.AddConstant(get.Name);
                Emit(Opcode.Invoke, expr.Line);
                EmitShort((ushort)nameIndex, expr.Line);
                EmitByte((byte)expr.Arguments.Count, expr.Line);
                return;
            }

            CompileExpr(expr.Callee);
            foreach (var arg in expr.Arguments)
            {
                CompileExpr(arg);
            }
            Emit(Opcode.Call, expr.Line);
            EmitByte((byte)expr.Arguments.Count, expr.Line);
        }

        private void CompileGet(GetExpr expr)
        {
            CompileExpr(expr.Object);
            var index = _bytecode.AddConstant(expr.Name);
            Emit(Opcode.GetProperty, expr.Line);
            EmitShort((ushort)index, expr.Line);
        }

        private void CompileSet(SetExpr expr)
        {
            CompileExpr(expr.Object);
            CompileExpr(expr.Value);
            var index = _bytecode.AddConstant(expr.Name);
            Emit(Opcode.SetProperty, expr.Line);
            EmitShort((ushort)index, expr.Line);
        }

        private void CompileIndexGet(IndexGetExpr expr)
        {
            CompileExpr(expr.Object);
            CompileExpr(expr.Index);
            Emit(Opcode.GetIndex, expr.Line);
        }

        private void CompileIndexSet(IndexSetExpr expr)
        {
            CompileExpr(expr.Object);
            CompileExpr(expr.Index);
            CompileExpr(expr.Value);
            Emit(Opcode.SetIndex, expr.Line);
        }

        private void CompileArray(ArrayExpr expr)
        {
            foreach (var element in expr.Elements)
            {
                CompileExpr(element);
            }
            Emit(Opcode.NewArray, expr.Line);
            EmitShort((ushort)expr.Elements.Count, expr.Line);
        }

        private void CompileObject(ObjectExpr expr)
        {
            Emit(Opcode.NewObject, expr.Line);
            foreach (var (key, value) in expr.Properties)
            {
                Emit(Opcode.Dup, expr.Line);
                CompileExpr(value);
                var index = _bytecode.AddConstant(key);
                Emit(Opcode.SetField, expr.Line);
                EmitShort((ushort)index, expr.Line);
                Emit(Opcode.Pop, expr.Line);
            }
        }

        private void CompileLambda(LambdaExpr expr)
        {
            List<Stmt> body;
            if (expr.Body != null)
            {
                body = new List<Stmt> { new ReturnStmt { Value = expr.Body, Line = expr.Line } };
            }
            else
            {
                body = expr.Block;
            }

            var compiled = CompileFunction("<lambda>", expr.Parameters, body);
            var index = _bytecode.AddConstant(compiled.Prototype);
            Emit(Opcode.Closure, expr.Line);
            EmitShort((ushort)index, expr.Line);
            EmitUpvalueInfo(compiled.Upvalues, expr.Line);
        }

        private void CompileThis(ThisExpr expr)
        {
            Emit(Opcode.This, expr.Line);
        }

        private void CompileSuper(SuperExpr expr)
        {
            var nameIndex = _bytecode.AddConstant(expr.Method);
            Emit(Opcode.GetSuper, expr.Line);
            EmitShort((ushort)nameIndex, expr.Line);
        }

        private void EmitUpvalueInfo(List<Upvalue> upvalues, int line)
        {
            foreach (var upvalue in upvalues)
            {
                EmitByte((byte)(upvalue.IsLocal ? 1 : 0), line);
                EmitByte((byte)upvalue.Index, line);
            }
        }

        // Scope management
        private void EmitPopLocals(int targetLocalCount, int line)
        {
            for (int i = _locals.Count - 1; i >= targetLocalCount; i--)
            {
                if (_locals[i].IsCaptured)
                {
                    Emit(Opcode.CloseUpvalue, line);
                }
                else
                {
                    Emit(Opcode.Pop, line);
                }
            }
        }
        private void BeginScope() => _scopeDepth++;

        private void EndScope(int line)
        {
            _scopeDepth--;
            while (_locals.Count > 0 && _locals[_locals.Count - 1].Depth > _scopeDepth)
            {
                if (_locals[_locals.Count - 1].IsCaptured)
                {
                    Emit(Opcode.CloseUpvalue, line);
                }
                else
                {
                    Emit(Opcode.Pop, line);
                }
                _locals.RemoveAt(_locals.Count - 1);
            }
        }

        private void AddLocal(string name)
        {
            _locals.Add(new Local { Name = name, Depth = _scopeDepth });
        }

        private int ResolveLocal(string name)
        {
            for (int i = _locals.Count - 1; i >= 0; i--)
            {
                if (_locals[i].Name == name) return i;
            }
            return -1;
        }

        private int ResolveUpvalue(string name)
        {
            if (_enclosing == null) return -1;

            var local = _enclosing.ResolveLocal(name);
            if (local != -1)
            {
                _enclosing._locals[local] = new Local
                {
                    Name = _enclosing._locals[local].Name,
                    Depth = _enclosing._locals[local].Depth,
                    IsCaptured = true
                };
                return AddUpvalue(local, true);
            }

            var upvalue = _enclosing.ResolveUpvalue(name);
            if (upvalue != -1)
            {
                return AddUpvalue(upvalue, false);
            }

            return -1;
        }

        private int AddUpvalue(int index, bool isLocal)
        {
            for (int i = 0; i < _upvalues.Count; i++)
            {
                if (_upvalues[i].Index == index && _upvalues[i].IsLocal == isLocal)
                    return i;
            }
            _upvalues.Add(new Upvalue { Index = index, IsLocal = isLocal });
            return _upvalues.Count - 1;
        }

        // Emit helpers
        private void Emit(Opcode op, int line) => _bytecode.Emit(op, line);
        private void EmitByte(byte b, int line) => _bytecode.EmitByte(b, line);
        private void EmitShort(ushort value, int line) => _bytecode.EmitShort(value, line);
        private int EmitJump(Opcode op, int line) => _bytecode.EmitJump(op, line);
        private void PatchJump(int offset) => _bytecode.PatchJump(offset);
        private void EmitLoop(int loopStart, int line) => _bytecode.EmitLoop(loopStart, line);

        private static bool IsSuperConstructorCall(Stmt stmt, string superClassName)
        {
            if (superClassName == null) return false;
            return stmt is ExpressionStmt es &&
                   es.Expression is CallExpr call &&
                   call.Callee is SuperExpr super &&
                   super.Method == superClassName;
        }
    }

    public class CompilerException : Exception
    {
        public CompilerException(string message) : base(message) { }
    }
}
