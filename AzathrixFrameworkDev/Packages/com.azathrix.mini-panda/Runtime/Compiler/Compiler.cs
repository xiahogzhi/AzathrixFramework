using System;
using System.Collections.Generic;
using Azathrix.MiniPanda.Lexer;
using Azathrix.MiniPanda.Parser;

namespace Azathrix.MiniPanda.Compiler
{
    /// <summary>
    /// MiniPanda 字节码编译器
    /// <para>
    /// 将 AST（抽象语法树）编译为字节码，主要功能：
    /// <list type="bullet">
    /// <item>遍历 AST 节点生成对应的字节码指令</item>
    /// <item>管理局部变量和作用域</item>
    /// <item>处理闭包和上值捕获</item>
    /// <item>编译函数、类、模块等结构</item>
    /// </list>
    /// </para>
    /// </summary>
    public class Compiler
    {
        // ==================== 编译状态 ====================

        /// <summary>当前正在生成的字节码</summary>
        private Bytecode _bytecode;
        /// <summary>局部变量列表</summary>
        private readonly List<Local> _locals = new List<Local>();
        /// <summary>当前作用域深度</summary>
        private int _scopeDepth;
        /// <summary>循环信息栈（用于 break/continue）</summary>
        private readonly List<LoopInfo> _loops = new List<LoopInfo>();
        /// <summary>当前编译的函数类型</summary>
        private FunctionType _functionType;
        /// <summary>外层编译器（用于嵌套函数）</summary>
        private Compiler _enclosing;
        /// <summary>上值列表（闭包捕获的变量）</summary>
        private readonly List<Upvalue> _upvalues = new List<Upvalue>();
        /// <summary>源文件路径</summary>
        private string _sourceFile;

        /// <summary>
        /// 局部变量信息
        /// </summary>
        private struct Local
        {
            /// <summary>变量名</summary>
            public string Name;
            /// <summary>声明时的作用域深度</summary>
            public int Depth;
            /// <summary>是否被闭包捕获</summary>
            public bool IsCaptured;
        }

        /// <summary>
        /// 上值信息（闭包捕获的变量）
        /// </summary>
        private struct Upvalue
        {
            /// <summary>在外层函数中的索引</summary>
            public int Index;
            /// <summary>是否是外层函数的局部变量（否则是外层的上值）</summary>
            public bool IsLocal;
        }

        /// <summary>
        /// 编译后的函数信息
        /// </summary>
        private struct CompiledFunction
        {
            /// <summary>函数原型</summary>
            public FunctionPrototype Prototype;
            /// <summary>上值列表</summary>
            public List<Upvalue> Upvalues;
        }

        /// <summary>
        /// 循环信息（用于 break/continue 跳转）
        /// </summary>
        private struct LoopInfo
        {
            /// <summary>循环开始位置</summary>
            public int Start;
            /// <summary>进入循环时的局部变量数量</summary>
            public int LocalCount;
            /// <summary>迭代器局部变量数量（for 循环）</summary>
            public int IterLocalCount;
            /// <summary>是否是 for 循环</summary>
            public bool IsFor;
            /// <summary>break 跳转位置列表</summary>
            public List<int> Breaks;
        }

        /// <summary>
        /// 函数类型枚举
        /// </summary>
        public enum FunctionType
        {
            /// <summary>脚本（顶层代码）</summary>
            Script,
            /// <summary>普通函数</summary>
            Function,
            /// <summary>类方法</summary>
            Method,
            /// <summary>构造函数</summary>
            Initializer
        }

        /// <summary>
        /// 源文件路径（用于调试信息）
        /// </summary>
        public string SourceFile
        {
            get => _sourceFile ?? _enclosing?.SourceFile;
            set => _sourceFile = value;
        }

        /// <summary>
        /// 创建编译器实例
        /// </summary>
        /// <param name="type">函数类型</param>
        /// <param name="enclosing">外层编译器（嵌套函数时使用）</param>
        public Compiler(FunctionType type = FunctionType.Script, Compiler enclosing = null)
        {
            _functionType = type;
            _enclosing = enclosing;
            _bytecode = new Bytecode();

            // 预留槽位 0：方法/构造函数用于 this，脚本/函数为空
            var slot0Name = (type == FunctionType.Method || type == FunctionType.Initializer) ? "this" : "";
            _locals.Add(new Local { Name = slot0Name, Depth = 0 });
        }

        /// <summary>
        /// 编译语句列表为字节码
        /// </summary>
        /// <param name="statements">AST 语句列表</param>
        /// <returns>编译后的字节码</returns>
        public Bytecode Compile(List<Stmt> statements)
        {
            foreach (var stmt in statements)
            {
                CompileStmt(stmt);
            }
            // 隐式返回 null
            Emit(Opcode.Null, 0);
            Emit(Opcode.Return, 0);
            _bytecode.SourceFile = SourceFile;
            _bytecode.LocalNames = _locals.ConvertAll(l => l.Name);
            return _bytecode;
        }

        /// <summary>
        /// 编译函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="parameters">参数列表</param>
        /// <param name="defaults">默认参数值列表</param>
        /// <param name="body">函数体语句</param>
        /// <param name="type">函数类型</param>
        /// <param name="className">所属类名（方法时使用）</param>
        /// <param name="restParam">可变参数名（如 ...args）</param>
        /// <returns>编译后的函数信息</returns>
        private CompiledFunction CompileFunction(string name, List<string> parameters, List<Expr> defaults, List<Stmt> body, FunctionType type = FunctionType.Function, string className = null, string restParam = null)
        {
            // 创建子编译器处理函数体
            var compiler = new Compiler(type, this);

            // 添加参数为局部变量
            foreach (var param in parameters)
            {
                compiler.AddLocal(param);
            }

            // 生成默认参数值检查代码
            if (defaults != null)
            {
                for (int i = 0; i < defaults.Count; i++)
                {
                    if (defaults[i] != null)
                    {
                        // 如果参数为 null，则使用默认值
                        // 类似空值合并：param ?? default
                        compiler.Emit(Opcode.GetLocal, 0);
                        compiler.EmitByte((byte)(i + 1), 0); // +1 因为槽位 0 被保留
                        compiler.Emit(Opcode.Dup, 0);
                        var skipJump = compiler.EmitJump(Opcode.JumpIfNotNull, 0);
                        compiler.Emit(Opcode.Pop, 0); // 弹出 null
                        compiler.CompileExpr(defaults[i]);
                        compiler.PatchJump(skipJump);
                        compiler.Emit(Opcode.SetLocal, 0);
                        compiler.EmitByte((byte)(i + 1), 0);
                        compiler.Emit(Opcode.Pop, 0); // 弹出值
                    }
                }
            }

            // 添加可变参数为局部变量
            if (restParam != null)
            {
                compiler.AddLocal(restParam);
            }

            // 编译函数体
            foreach (var stmt in body)
            {
                compiler.CompileStmt(stmt);
            }

            // 隐式返回（构造函数返回 this，其他返回 null）
            if (type == FunctionType.Initializer)
            {
                compiler.Emit(Opcode.GetLocal, 0);
                compiler.EmitByte(0, 0); // 槽位 0 = this
            }
            else
            {
                compiler.Emit(Opcode.Null, 0);
            }
            compiler.Emit(Opcode.Return, 0);

            // 传播源文件路径到嵌套函数
            compiler._bytecode.SourceFile = SourceFile;

            var prototype = new FunctionPrototype
            {
                Name = name,
                ClassName = className,
                Arity = parameters.Count,
                RestParam = restParam,
                Code = compiler._bytecode,
                UpvalueCount = compiler._upvalues.Count,
                LocalNames = compiler._locals.ConvertAll(l => l.Name)
            };

            return new CompiledFunction
            {
                Prototype = prototype,
                Upvalues = compiler._upvalues
            };
        }

        // ==================== 语句编译 ====================

        /// <summary>
        /// 编译语句（分发到具体类型）
        /// </summary>
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
                case TryStmt s: CompileTryStmt(s); break;
                case ThrowStmt s: CompileThrowStmt(s); break;
                case EnumDecl s: CompileEnumDecl(s); break;
                default: throw new CompilerException($"Unknown statement type: {stmt.GetType().Name}");
            }
        }

        /// <summary>编译表达式语句</summary>
        private void CompileExpressionStmt(ExpressionStmt stmt)
        {
            CompileExpr(stmt.Expression);
            Emit(Opcode.Pop, stmt.Line); // 丢弃表达式结果
        }

        /// <summary>编译变量声明</summary>
        private void CompileVarDecl(VarDecl stmt)
        {
            // 记录导出
            if (stmt.IsExport && _scopeDepth == 0)
            {
                _bytecode.Exports.Add(stmt.Name);
            }

            if (stmt.IsGlobal)
            {
                // 根全局变量（global var x = ...）
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
                // 局部变量
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
                // 模块级全局变量
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

        /// <summary>编译函数声明</summary>
        private void CompileFuncDecl(FuncDecl stmt)
        {
            // 记录导出
            if (stmt.IsExport && _scopeDepth == 0)
            {
                _bytecode.Exports.Add(stmt.Name);
            }

            var compiled = CompileFunction(stmt.Name, stmt.Parameters, stmt.Defaults, stmt.Body, FunctionType.Function, null, stmt.RestParam);
            var index = _bytecode.AddConstant(compiled.Prototype);

            // 生成闭包指令
            Emit(Opcode.Closure, stmt.Line);
            EmitShort((ushort)index, stmt.Line);
            EmitUpvalueInfo(compiled.Upvalues, stmt.Line);

            // 绑定函数名
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

        /// <summary>
        /// 编译类声明
        /// </summary>
        /// <remarks>
        /// 处理类定义、继承、字段初始化和方法编译
        /// </remarks>
        private void CompileClassDecl(ClassDecl stmt)
        {
            // 记录导出
            if (stmt.IsExport && _scopeDepth == 0)
            {
                _bytecode.Exports.Add(stmt.Name);
            }

            // 创建类对象
            var nameIndex = _bytecode.AddConstant(stmt.Name);
            Emit(Opcode.Class, stmt.Line);
            EmitShort((ushort)nameIndex, stmt.Line);

            // 绑定类名
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

            // 处理继承
            if (stmt.SuperClass != null)
            {
                // 加载父类
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

                // 加载刚定义的类
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

                // 执行继承
                Emit(Opcode.Inherit, stmt.Line);
            }

            // 将字段初始化注入构造函数
            var methods = stmt.Methods;
            var constructorName = stmt.Name; // 构造函数使用类名
            if (stmt.Fields.Count > 0)
            {
                methods = new List<FuncDecl>(stmt.Methods);
                var initMethod = methods.Find(m => m.Name == constructorName);
                var fieldInits = new List<Stmt>();

                // 生成字段初始化语句
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
                    var body = initMethod.Body;
                    var newBody = new List<Stmt>();
                    int startIndex = 0;

                    // 如果第一条语句是 super.ClassName()，保留在开头
                    if (body.Count > 0 && IsSuperConstructorCall(body[0], stmt.SuperClass))
                    {
                        newBody.Add(body[0]);
                        startIndex = 1;
                    }

                    // 添加字段初始化
                    newBody.AddRange(fieldInits);

                    // 添加剩余的构造函数体
                    for (int i = startIndex; i < body.Count; i++)
                    {
                        newBody.Add(body[i]);
                    }

                    methods.Add(new FuncDecl { Name = constructorName, Parameters = initMethod.Parameters, Body = newBody, Line = initMethod.Line });
                }
                else
                {
                    // 如果子类没有显式构造函数，先调用 super.SuperClassName()
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
                    methods.Add(new FuncDecl { Name = constructorName, Parameters = new List<string>(), Defaults = new List<Expr>(), Body = fieldInits, Line = stmt.Line });
                }
            }

            // 编译方法
            foreach (var method in methods)
            {
                var methodType = method.Name == constructorName ? FunctionType.Initializer : FunctionType.Method;
                var compiled = CompileFunction(method.Name, method.Parameters, method.Defaults, method.Body, methodType, stmt.Name, method.RestParam);
                var methodIndex = _bytecode.AddConstant(compiled.Prototype);
                var methodNameIndex = _bytecode.AddConstant(method.Name);

                // 加载类
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

                // 生成闭包并添加为方法
                Emit(Opcode.Closure, stmt.Line);
                EmitShort((ushort)methodIndex, stmt.Line);
                EmitUpvalueInfo(compiled.Upvalues, stmt.Line);

                Emit(Opcode.Method, stmt.Line);
                EmitShort((ushort)methodNameIndex, stmt.Line);
            }

            // 编译静态字段
            foreach (var field in stmt.StaticFields)
            {
                // 加载类
                if (stmt.IsGlobal || _scopeDepth == 0)
                {
                    Emit(Opcode.GetGlobal, field.Line);
                    EmitShort((ushort)nameIndex, field.Line);
                }
                else
                {
                    var classIndex = ResolveLocal(stmt.Name);
                    Emit(Opcode.GetLocal, field.Line);
                    EmitByte((byte)classIndex, field.Line);
                }

                // 编译初始值
                if (field.Initializer != null)
                    CompileExpr(field.Initializer);
                else
                    Emit(Opcode.Null, field.Line);

                // 设置静态字段
                var fieldNameIndex = _bytecode.AddConstant(field.Name);
                Emit(Opcode.StaticField, field.Line);
                EmitShort((ushort)fieldNameIndex, field.Line);
            }

            // 编译静态方法
            foreach (var method in stmt.StaticMethods)
            {
                var compiled = CompileFunction(method.Name, method.Parameters, method.Defaults, method.Body, FunctionType.Function, null, method.RestParam);
                var methodIndex = _bytecode.AddConstant(compiled.Prototype);
                var methodNameIndex = _bytecode.AddConstant(method.Name);

                // 加载类
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

                // 生成闭包并添加为静态方法
                Emit(Opcode.Closure, stmt.Line);
                EmitShort((ushort)methodIndex, stmt.Line);
                EmitUpvalueInfo(compiled.Upvalues, stmt.Line);

                Emit(Opcode.StaticMethod, stmt.Line);
                EmitShort((ushort)methodNameIndex, stmt.Line);
            }
        }

        /// <summary>
        /// 编译 if 语句
        /// </summary>
        /// <remarks>
        /// 生成条件跳转代码：
        /// 1. 计算条件表达式
        /// 2. 条件为假时跳转到 else 分支
        /// 3. 执行 then 分支后跳过 else 分支
        /// </remarks>
        private void CompileIfStmt(IfStmt stmt)
        {
            // 编译条件表达式
            CompileExpr(stmt.Condition);
            // 条件为假时跳转到 else 分支
            var thenJump = EmitJump(Opcode.JumpIfFalse, stmt.Line);
            Emit(Opcode.Pop, stmt.Line); // 弹出条件值

            // 编译 then 分支
            foreach (var s in stmt.ThenBranch)
            {
                CompileStmt(s);
            }

            // then 分支结束后跳过 else 分支
            var elseJump = EmitJump(Opcode.Jump, stmt.Line);
            PatchJump(thenJump);
            Emit(Opcode.Pop, stmt.Line); // 弹出条件值

            // 编译 else 分支（如果存在）
            if (stmt.ElseBranch != null)
            {
                foreach (var s in stmt.ElseBranch)
                {
                    CompileStmt(s);
                }
            }

            PatchJump(elseJump);
        }

        /// <summary>
        /// 编译 while 循环语句
        /// </summary>
        /// <remarks>
        /// 生成循环代码：
        /// 1. 记录循环起始位置
        /// 2. 计算条件，为假时跳出
        /// 3. 执行循环体
        /// 4. 跳回循环起始
        /// </remarks>
        private void CompileWhileStmt(WhileStmt stmt)
        {
            // 记录循环起始位置
            var loopStart = _bytecode.Code.Count;
            // 压入循环信息（用于 break/continue）
            _loops.Add(new LoopInfo
            {
                Start = loopStart,
                LocalCount = _locals.Count,
                IterLocalCount = _locals.Count,
                IsFor = false,
                Breaks = new List<int>()
            });

            // 编译条件表达式
            CompileExpr(stmt.Condition);
            // 条件为假时跳出循环
            var exitJump = EmitJump(Opcode.JumpIfFalse, stmt.Line);
            Emit(Opcode.Pop, stmt.Line); // 弹出条件值

            // 编译循环体
            foreach (var s in stmt.Body)
            {
                CompileStmt(s);
            }

            // 跳回循环起始
            EmitLoop(loopStart, stmt.Line);
            PatchJump(exitJump);
            Emit(Opcode.Pop, stmt.Line); // 弹出条件值

            // 修补所有 break 跳转
            var loop = _loops[_loops.Count - 1];
            foreach (var breakJump in loop.Breaks)
            {
                PatchJump(breakJump);
            }
            _loops.RemoveAt(_loops.Count - 1);
        }

        /// <summary>
        /// 编译 for-in 循环语句
        /// </summary>
        /// <remarks>
        /// 支持两种形式：
        /// - for v in iterable { }     // 只迭代值
        /// - for k, v in iterable { }  // 迭代键值对
        ///
        /// 生成代码流程：
        /// 1. 获取迭代器
        /// 2. 调用 ForIter/ForIterKV 获取下一个元素
        /// 3. 绑定循环变量
        /// 4. 执行循环体
        /// 5. 弹出循环变量，跳回步骤 2
        /// </remarks>
        private void CompileForStmt(ForStmt stmt)
        {
            BeginScope();

            var loopBase = _locals.Count;
            var hasKeyVar = stmt.KeyVariable != null;

            // 编译可迭代对象并获取迭代器
            // 栈: [..., iterable] -> GetIter -> [..., iterator]
            CompileExpr(stmt.Iterable);
            Emit(Opcode.GetIter, stmt.Line);
            // 为迭代器预留局部变量槽位，确保跨迭代存活
            AddLocal("$iter");
            var iterLocalCount = _locals.Count;

            // 记录循环起始位置
            var loopStart = _bytecode.Code.Count;
            _loops.Add(new LoopInfo
            {
                Start = loopStart,
                LocalCount = loopBase,
                IterLocalCount = iterLocalCount,
                IsFor = true,
                Breaks = new List<int>()
            });

            // ForIter/ForIterKV 检查迭代器，有元素则压栈，否则跳出
            var exitJump = EmitJump(hasKeyVar ? Opcode.ForIterKV : Opcode.ForIter, stmt.Line);

            // 绑定循环变量
            if (hasKeyVar)
            {
                AddLocal(stmt.KeyVariable);   // 键变量
                AddLocal(stmt.Variable);      // 值变量
            }
            else
            {
                AddLocal(stmt.Variable);
            }

            // 编译循环体
            foreach (var s in stmt.Body)
            {
                CompileStmt(s);
            }

            // 弹出循环变量，迭代器回到栈顶
            if (hasKeyVar)
            {
                // 弹出值变量
                if (_locals[_locals.Count - 1].IsCaptured)
                    Emit(Opcode.CloseUpvalue, stmt.Line);
                else
                    Emit(Opcode.Pop, stmt.Line);
                // 弹出键变量
                if (_locals[_locals.Count - 2].IsCaptured)
                    Emit(Opcode.CloseUpvalue, stmt.Line);
                else
                    Emit(Opcode.Pop, stmt.Line);
            }
            else
            {
                if (_locals[_locals.Count - 1].IsCaptured)
                    Emit(Opcode.CloseUpvalue, stmt.Line);
                else
                    Emit(Opcode.Pop, stmt.Line);
            }

            // 跳回循环起始
            EmitLoop(loopStart, stmt.Line);
            PatchJump(exitJump);

            // 修补所有 break 跳转
            var loop = _loops[_loops.Count - 1];
            foreach (var breakJump in loop.Breaks)
            {
                PatchJump(breakJump);
            }
            _loops.RemoveAt(_loops.Count - 1);

            // 从局部变量列表移除 $iter 和循环变量
            if (hasKeyVar)
            {
                _locals.RemoveAt(_locals.Count - 1); // 值变量
                _locals.RemoveAt(_locals.Count - 1); // 键变量
            }
            else
            {
                _locals.RemoveAt(_locals.Count - 1); // 循环变量
            }
            _locals.RemoveAt(_locals.Count - 1); // $iter

            EndScope(stmt.Line);
        }

        /// <summary>
        /// 编译 return 语句
        /// </summary>
        private void CompileReturnStmt(ReturnStmt stmt)
        {
            if (stmt.Value != null)
            {
                CompileExpr(stmt.Value); // 编译返回值
            }
            else
            {
                Emit(Opcode.Null, stmt.Line); // 无返回值时返回 null
            }
            Emit(Opcode.Return, stmt.Line);
        }

        /// <summary>
        /// 编译 break 语句
        /// </summary>
        /// <remarks>
        /// 弹出循环内的局部变量后跳出循环
        /// </remarks>
        private void CompileBreakStmt(BreakStmt stmt)
        {
            if (_loops.Count == 0)
                throw new CompilerException("'break' outside of loop");

            var loop = _loops[_loops.Count - 1];
            // 弹出循环内声明的局部变量
            EmitPopLocals(loop.LocalCount, stmt.Line);
            // 记录跳转位置，稍后修补
            var jump = EmitJump(Opcode.Jump, stmt.Line);
            loop.Breaks.Add(jump);
        }

        /// <summary>
        /// 编译 continue 语句
        /// </summary>
        /// <remarks>
        /// 弹出循环体内的局部变量后跳回循环起始
        /// </remarks>
        private void CompileContinueStmt(ContinueStmt stmt)
        {
            if (_loops.Count == 0)
                throw new CompilerException("'continue' outside of loop");

            var loop = _loops[_loops.Count - 1];
            // for 循环需要保留迭代器，while 循环弹出所有局部变量
            var targetLocals = loop.IsFor ? loop.IterLocalCount : loop.LocalCount;
            EmitPopLocals(targetLocals, stmt.Line);
            // 跳回循环起始
            EmitLoop(loop.Start, stmt.Line);
        }

        /// <summary>
        /// 编译块语句
        /// </summary>
        private void CompileBlockStmt(BlockStmt stmt)
        {
            BeginScope();
            foreach (var s in stmt.Statements)
            {
                CompileStmt(s);
            }
            EndScope(stmt.Line);
        }

        /// <summary>
        /// 编译 import 语句
        /// </summary>
        /// <remarks>
        /// 支持两种形式：
        /// - import "path"           // 导入为模块名
        /// - import "path" as alias  // 导入为别名
        /// - global import "path"    // 导入到根全局作用域
        /// </remarks>
        private void CompileImportStmt(ImportStmt stmt)
        {
            // 生成 Import 指令
            var pathIndex = _bytecode.AddConstant(stmt.Path);
            var aliasIndex = _bytecode.AddConstant(stmt.Alias ?? "");
            Emit(Opcode.Import, stmt.Line);
            EmitShort((ushort)pathIndex, stmt.Line);
            EmitShort((ushort)aliasIndex, stmt.Line);
            EmitByte((byte)(stmt.IsGlobal ? 1 : 0), stmt.Line);

            // global import: VM 处理绑定到全局，编译器无需操作
            // 普通 import: VM 压入模块，编译器绑定到局部/全局变量
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

        /// <summary>
        /// 编译 try/catch/finally 语句
        /// </summary>
        /// <remarks>
        /// 生成异常处理代码：
        /// 1. SetupTry 设置异常处理器
        /// 2. 执行 try 块
        /// 3. 异常时跳转到 catch 块
        /// 4. finally 块始终执行
        /// </remarks>
        private void CompileTryStmt(TryStmt stmt)
        {
            // SetupTry 指令格式: catchOffset(2), finallyOffset(2), catchVarSlot(1)
            Emit(Opcode.SetupTry, stmt.Line);
            var catchOffsetPos = _bytecode.Code.Count;
            EmitShort(0, stmt.Line);  // catch 偏移占位符
            var finallyOffsetPos = _bytecode.Code.Count;
            EmitShort(0, stmt.Line);  // finally 偏移占位符

            // catch 变量槽位（-1 表示无 catch 变量）
            int catchVarSlot = -1;
            if (stmt.CatchBlock != null && stmt.CatchVar != null)
            {
                BeginScope();
                AddLocal(stmt.CatchVar);
                catchVarSlot = _locals.Count - 1;
            }
            EmitByte((byte)(catchVarSlot == -1 ? 255 : catchVarSlot), stmt.Line);

            // 编译 try 块
            foreach (var s in stmt.TryBlock)
            {
                CompileStmt(s);
            }
            Emit(Opcode.EndTry, stmt.Line);  // try 正常退出

            // 跳过 catch/finally（正常流程）
            var endJump = EmitJump(Opcode.Jump, stmt.Line);

            // 修补 catch 偏移
            var catchStart = _bytecode.Code.Count;
            PatchShort(catchOffsetPos, (ushort)catchStart);

            // 编译 catch 块
            if (stmt.CatchBlock != null)
            {
                // 异常值在栈上，存入 catch 变量（如果有）
                if (stmt.CatchVar != null)
                {
                    // 异常已由 VM 压栈，成为局部变量
                }
                else
                {
                    Emit(Opcode.Pop, stmt.Line);  // 无变量时丢弃异常
                }

                foreach (var s in stmt.CatchBlock)
                {
                    CompileStmt(s);
                }

                if (stmt.CatchVar != null)
                {
                    EndScope(stmt.Line);
                }
                Emit(Opcode.EndTry, stmt.Line);  // catch 结束
            }

            // 修补 finally 偏移
            var finallyStart = _bytecode.Code.Count;
            PatchShort(finallyOffsetPos, (ushort)finallyStart);

            // 编译 finally 块
            if (stmt.FinallyBlock != null)
            {
                foreach (var s in stmt.FinallyBlock)
                {
                    CompileStmt(s);
                }
                Emit(Opcode.EndFinally, stmt.Line);
            }

            PatchJump(endJump);
        }

        /// <summary>
        /// 编译 throw 语句
        /// </summary>
        private void CompileThrowStmt(ThrowStmt stmt)
        {
            CompileExpr(stmt.Value); // 编译异常值
            Emit(Opcode.Throw, stmt.Line);
        }

        /// <summary>
        /// 修补短整数（2字节）
        /// </summary>
        private void PatchShort(int offset, ushort value)
        {
            _bytecode.Code[offset] = (byte)(value >> 8);
            _bytecode.Code[offset + 1] = (byte)(value & 0xff);
        }

        /// <summary>
        /// 编译枚举声明
        /// </summary>
        /// <remarks>
        /// 枚举编译为包含成员的对象
        /// </remarks>
        private void CompileEnumDecl(EnumDecl stmt)
        {
            // 创建包含枚举成员的对象
            Emit(Opcode.NewObject, stmt.Line);

            // 添加每个枚举成员
            foreach (var (memberName, memberValue) in stmt.Members)
            {
                Emit(Opcode.Dup, stmt.Line);
                CompileExpr(memberValue);
                var nameIndex = _bytecode.AddConstant(memberName);
                Emit(Opcode.SetField, stmt.Line);
                EmitShort((ushort)nameIndex, stmt.Line);
                Emit(Opcode.Pop, stmt.Line);
            }

            // 将枚举定义为变量
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

        /// <summary>
        /// 从模块路径提取模块名
        /// </summary>
        /// <example>
        /// "utils.math" -> "math"
        /// "config" -> "config"
        /// </example>
        private static string GetModuleName(string path)
        {
            var lastDot = path.LastIndexOf('.');
            return lastDot >= 0 ? path.Substring(lastDot + 1) : path;
        }

        // ==================== 表达式编译 ====================

        /// <summary>
        /// 编译表达式（分发到具体类型）
        /// </summary>
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
                case NullCoalesceExpr e: CompileNullCoalesce(e); break;
                case CompoundAssignExpr e: CompileCompoundAssign(e); break;
                case UpdateExpr e: CompileUpdate(e); break;
                default: throw new CompilerException($"Unknown expression type: {expr.GetType().Name}");
            }
        }

        /// <summary>
        /// 编译三元运算符表达式
        /// </summary>
        /// <remarks>
        /// condition ? thenExpr : elseExpr
        /// </remarks>
        private void CompileTernary(TernaryExpr expr)
        {
            CompileExpr(expr.Condition);
            var elseJump = EmitJump(Opcode.JumpIfFalse, expr.Line);
            Emit(Opcode.Pop, expr.Line); // 弹出条件值
            CompileExpr(expr.ThenExpr);
            var endJump = EmitJump(Opcode.Jump, expr.Line);
            PatchJump(elseJump);
            Emit(Opcode.Pop, expr.Line); // 弹出条件值
            CompileExpr(expr.ElseExpr);
            PatchJump(endJump);
        }

        /// <summary>
        /// 编译空值合并表达式
        /// </summary>
        /// <remarks>
        /// left ?? right: 如果 left 不为 null 则使用 left，否则使用 right
        /// </remarks>
        private void CompileNullCoalesce(NullCoalesceExpr expr)
        {
            CompileExpr(expr.Left);
            Emit(Opcode.Dup, expr.Line);
            var endJump = EmitJump(Opcode.JumpIfNotNull, expr.Line);
            Emit(Opcode.Pop, expr.Line); // 弹出 null
            CompileExpr(expr.Right);
            PatchJump(endJump);
        }

        /// <summary>
        /// 生成二元运算操作码
        /// </summary>
        private void EmitBinaryOp(TokenType op, int line)
        {
            switch (op)
            {
                case TokenType.Plus: Emit(Opcode.Add, line); break;
                case TokenType.Minus: Emit(Opcode.Sub, line); break;
                case TokenType.Star: Emit(Opcode.Mul, line); break;
                case TokenType.Slash: Emit(Opcode.Div, line); break;
                case TokenType.Percent: Emit(Opcode.Mod, line); break;
                default: throw new CompilerException($"Unsupported operator: {op}");
            }
        }

        /// <summary>
        /// 生成数字常量指令
        /// </summary>
        private void EmitNumberConst(double value, int line)
        {
            var index = _bytecode.AddConstant(value);
            Emit(Opcode.Const, line);
            EmitShort((ushort)index, line);
        }

        /// <summary>
        /// 编译复合赋值表达式
        /// </summary>
        /// <remarks>
        /// 支持 +=, -=, *=, /=, %= 运算符
        /// </remarks>
        private void CompileCompoundAssign(CompoundAssignExpr expr)
        {
            switch (expr.Target)
            {
                case IdentifierExpr:
                    // 标识符复合赋值: x += 1
                    CompileExpr(expr.Target);
                    CompileExpr(expr.Value);
                    EmitBinaryOp(expr.Operator, expr.Line);
                    EmitAssignment(expr.Target, expr.Line);
                    break;
                case GetExpr get:
                    // 属性复合赋值: obj.prop += 1
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
                    // 索引复合赋值: arr[i] += 1
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

        /// <summary>
        /// 编译自增/自减表达式
        /// </summary>
        /// <remarks>
        /// 支持前缀和后缀形式：++x, x++, --x, x--
        /// </remarks>
        private void CompileUpdate(UpdateExpr expr)
        {
            // 计算增量：++ 为 1，-- 为 -1
            var delta = expr.Operator == TokenType.PlusPlus ? 1.0 : -1.0;

            switch (expr.Target)
            {
                case IdentifierExpr:
                    if (expr.IsPrefix)
                    {
                        // 前缀：++x，先加后返回新值
                        CompileExpr(expr.Target);
                        EmitNumberConst(delta, expr.Line);
                        Emit(Opcode.Add, expr.Line);
                        EmitAssignment(expr.Target, expr.Line);
                    }
                    else
                    {
                        // 后缀：x++，先返回旧值后加
                        CompileExpr(expr.Target);
                        Emit(Opcode.Dup, expr.Line);
                        EmitNumberConst(delta, expr.Line);
                        Emit(Opcode.Add, expr.Line);
                        EmitAssignment(expr.Target, expr.Line);
                        Emit(Opcode.Pop, expr.Line); // 弹出新值，保留旧值
                    }
                    break;
                case GetExpr get:
                    {
                        // 属性自增/自减
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
                    // 索引自增/自减
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

        /// <summary>
        /// 生成赋值指令
        /// </summary>
        /// <remarks>
        /// 根据目标类型生成对应的赋值操作码
        /// </remarks>
        private void EmitAssignment(Expr target, int line)
        {
            if (target is IdentifierExpr id)
            {
                // 标识符赋值：优先局部变量 -> 上值 -> 全局变量
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
                // 属性赋值
                CompileExpr(get.Object);
                Emit(Opcode.Swap, line);
                var index = _bytecode.AddConstant(get.Name);
                Emit(Opcode.SetProperty, line);
                EmitShort((ushort)index, line);
            }
            else if (target is IndexGetExpr idx)
            {
                // 索引赋值
                CompileExpr(idx.Object);
                CompileExpr(idx.Index);
                Emit(Opcode.SwapUnder, line);
                Emit(Opcode.Swap, line);
                Emit(Opcode.SetIndex, line);
            }
        }

        /// <summary>
        /// 编译字面量表达式
        /// </summary>
        private void CompileLiteral(LiteralExpr expr)
        {
            switch (expr.Value)
            {
                case null: Emit(Opcode.Null, expr.Line); break;   // null 字面量
                case true: Emit(Opcode.True, expr.Line); break;   // true 字面量
                case false: Emit(Opcode.False, expr.Line); break; // false 字面量
                default:
                    // 数字或字符串常量
                    var index = _bytecode.AddConstant(expr.Value);
                    Emit(Opcode.Const, expr.Line);
                    EmitShort((ushort)index, expr.Line);
                    break;
            }
        }

        /// <summary>
        /// 编译字符串表达式（支持插值）
        /// </summary>
        /// <remarks>
        /// 处理字符串插值，如 "Hello {name}!"
        /// 插值表达式已在解析阶段预解析为 Expr
        /// </remarks>
        private void CompileStringExpr(StringExpr expr)
        {
            int partCount = 0;
            foreach (var part in expr.Parts)
            {
                if (part is string s)
                {
                    // 字符串字面量部分
                    var index = _bytecode.AddConstant(s);
                    Emit(Opcode.Const, expr.Line);
                    EmitShort((ushort)index, expr.Line);
                    partCount++;
                }
                else if (part is Expr interpExpr)
                {
                    // 预解析的插值表达式，直接编译
                    CompileExpr(interpExpr);
                    partCount++;
                }
            }
            // 构建最终字符串
            Emit(Opcode.BuildString, expr.Line);
            EmitByte((byte)partCount, expr.Line);
        }

        /// <summary>
        /// 编译标识符表达式
        /// </summary>
        /// <remarks>
        /// 按优先级查找：局部变量 -> 上值 -> 全局变量
        /// </remarks>
        private void CompileIdentifier(IdentifierExpr expr)
        {
            // 优先查找局部变量
            var local = ResolveLocal(expr.Name);
            if (local != -1)
            {
                Emit(Opcode.GetLocal, expr.Line);
                EmitByte((byte)local, expr.Line);
            }
            else
            {
                // 查找上值（闭包捕获的变量）
                var upvalue = ResolveUpvalue(expr.Name);
                if (upvalue != -1)
                {
                    Emit(Opcode.GetUpvalue, expr.Line);
                    EmitByte((byte)upvalue, expr.Line);
                }
                else
                {
                    // 全局变量
                    var index = _bytecode.AddConstant(expr.Name);
                    Emit(Opcode.GetGlobal, expr.Line);
                    EmitShort((ushort)index, expr.Line);
                }
            }
        }

        /// <summary>
        /// 编译赋值表达式
        /// </summary>
        private void CompileAssign(AssignExpr expr)
        {
            CompileExpr(expr.Value); // 先编译右值

            if (expr.Target is IdentifierExpr id)
            {
                // 标识符赋值
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

        /// <summary>
        /// 编译二元运算表达式
        /// </summary>
        /// <remarks>
        /// 支持短路求值（&& 和 ||）和常量折叠优化
        /// </remarks>
        private void CompileBinary(BinaryExpr expr)
        {
            // 常量折叠：编译时计算常量表达式
            if (TryFoldBinary(expr, out var result))
            {
                CompileLiteral(new LiteralExpr { Value = result, Line = expr.Line });
                return;
            }

            // && 短路求值：左边为假则跳过右边
            if (expr.Operator == TokenType.And)
            {
                CompileExpr(expr.Left);
                var endJump = EmitJump(Opcode.JumpIfFalse, expr.Line);
                Emit(Opcode.Pop, expr.Line);
                CompileExpr(expr.Right);
                PatchJump(endJump);
                return;
            }

            // || 短路求值：左边为真则跳过右边
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

            // 普通二元运算：先编译两个操作数
            CompileExpr(expr.Left);
            CompileExpr(expr.Right);

            // 生成对应的运算操作码
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
                case TokenType.BitAnd: Emit(Opcode.BitAnd, expr.Line); break;
                case TokenType.BitOr: Emit(Opcode.BitOr, expr.Line); break;
                case TokenType.BitXor: Emit(Opcode.BitXor, expr.Line); break;
                case TokenType.LeftShift: Emit(Opcode.Shl, expr.Line); break;
                case TokenType.RightShift: Emit(Opcode.Shr, expr.Line); break;
            }
        }

        /// <summary>
        /// 编译一元运算表达式
        /// </summary>
        private void CompileUnary(UnaryExpr expr)
        {
            // 常量折叠：编译时计算常量表达式
            if (TryFoldUnary(expr, out var result))
            {
                CompileLiteral(new LiteralExpr { Value = result, Line = expr.Line });
                return;
            }

            CompileExpr(expr.Operand);
            switch (expr.Operator)
            {
                case TokenType.Minus: Emit(Opcode.Neg, expr.Line); break;   // 取负
                case TokenType.Bang: Emit(Opcode.Not, expr.Line); break;    // 逻辑非
                case TokenType.BitNot: Emit(Opcode.BitNot, expr.Line); break; // 按位取反
            }
        }

        /// <summary>
        /// 编译函数调用表达式
        /// </summary>
        /// <remarks>
        /// 优化方法调用：obj.method(args) 使用 Invoke 指令
        /// </remarks>
        private void CompileCall(CallExpr expr)
        {
            // 优化方法调用
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

            // 普通函数调用
            CompileExpr(expr.Callee);
            foreach (var arg in expr.Arguments)
            {
                CompileExpr(arg);
            }
            Emit(Opcode.Call, expr.Line);
            EmitByte((byte)expr.Arguments.Count, expr.Line);
        }

        /// <summary>
        /// 编译属性访问表达式
        /// </summary>
        /// <remarks>
        /// 支持可选链：obj?.prop
        /// </remarks>
        private void CompileGet(GetExpr expr)
        {
            CompileExpr(expr.Object);

            if (expr.IsOptional)
            {
                // 可选链：obj?.prop，如果 obj 为 null 则返回 null
                Emit(Opcode.Dup, expr.Line);
                var skipJump = EmitJump(Opcode.JumpIfNotNull, expr.Line);
                Emit(Opcode.Pop, expr.Line);  // 弹出复制的值，保留 null
                var endJump = EmitJump(Opcode.Jump, expr.Line);
                PatchJump(skipJump);
                Emit(Opcode.Pop, expr.Line);  // 弹出复制的值，保留 obj
                var index = _bytecode.AddConstant(expr.Name);
                Emit(Opcode.GetProperty, expr.Line);
                EmitShort((ushort)index, expr.Line);
                PatchJump(endJump);
            }
            else
            {
                // 普通属性访问
                var index = _bytecode.AddConstant(expr.Name);
                Emit(Opcode.GetProperty, expr.Line);
                EmitShort((ushort)index, expr.Line);
            }
        }

        /// <summary>
        /// 编译属性设置表达式
        /// </summary>
        private void CompileSet(SetExpr expr)
        {
            CompileExpr(expr.Object);
            CompileExpr(expr.Value);
            var index = _bytecode.AddConstant(expr.Name);
            Emit(Opcode.SetProperty, expr.Line);
            EmitShort((ushort)index, expr.Line);
        }

        /// <summary>
        /// 编译索引访问表达式
        /// </summary>
        /// <remarks>
        /// 支持可选链：obj?[index]
        /// </remarks>
        private void CompileIndexGet(IndexGetExpr expr)
        {
            CompileExpr(expr.Object);

            if (expr.IsOptional)
            {
                // 可选链：obj?[index]，如果 obj 为 null 则返回 null
                Emit(Opcode.Dup, expr.Line);
                var skipJump = EmitJump(Opcode.JumpIfNotNull, expr.Line);
                Emit(Opcode.Pop, expr.Line);  // 弹出复制的值，保留 null
                var endJump = EmitJump(Opcode.Jump, expr.Line);
                PatchJump(skipJump);
                Emit(Opcode.Pop, expr.Line);  // 弹出复制的值，保留 obj
                CompileExpr(expr.Index);
                Emit(Opcode.GetIndex, expr.Line);
                PatchJump(endJump);
            }
            else
            {
                // 普通索引访问
                CompileExpr(expr.Index);
                Emit(Opcode.GetIndex, expr.Line);
            }
        }

        /// <summary>
        /// 编译索引设置表达式
        /// </summary>
        private void CompileIndexSet(IndexSetExpr expr)
        {
            CompileExpr(expr.Object);
            CompileExpr(expr.Index);
            CompileExpr(expr.Value);
            Emit(Opcode.SetIndex, expr.Line);
        }

        /// <summary>
        /// 编译数组字面量表达式
        /// </summary>
        private void CompileArray(ArrayExpr expr)
        {
            // 先编译所有元素
            foreach (var element in expr.Elements)
            {
                CompileExpr(element);
            }
            // 创建数组
            Emit(Opcode.NewArray, expr.Line);
            EmitShort((ushort)expr.Elements.Count, expr.Line);
        }

        /// <summary>
        /// 编译对象字面量表达式
        /// </summary>
        private void CompileObject(ObjectExpr expr)
        {
            // 创建空对象
            Emit(Opcode.NewObject, expr.Line);
            // 设置每个属性
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

        /// <summary>
        /// 编译 Lambda 表达式
        /// </summary>
        /// <remarks>
        /// 支持两种形式：
        /// - 表达式体：(x) => x * 2
        /// - 块体：(x) => { return x * 2; }
        /// </remarks>
        private void CompileLambda(LambdaExpr expr)
        {
            List<Stmt> body;
            if (expr.Body != null)
            {
                // 表达式体：转换为 return 语句
                body = new List<Stmt> { new ReturnStmt { Value = expr.Body, Line = expr.Line } };
            }
            else
            {
                // 块体：直接使用
                body = expr.Block;
            }

            // 编译为闭包
            var compiled = CompileFunction("<lambda>", expr.Parameters, expr.Defaults, body, FunctionType.Function, null, expr.RestParam);
            var index = _bytecode.AddConstant(compiled.Prototype);
            Emit(Opcode.Closure, expr.Line);
            EmitShort((ushort)index, expr.Line);
            EmitUpvalueInfo(compiled.Upvalues, expr.Line);
        }

        /// <summary>
        /// 编译 this 表达式
        /// </summary>
        private void CompileThis(ThisExpr expr)
        {
            Emit(Opcode.This, expr.Line);
        }

        /// <summary>
        /// 编译 super 表达式
        /// </summary>
        private void CompileSuper(SuperExpr expr)
        {
            var nameIndex = _bytecode.AddConstant(expr.Method);
            Emit(Opcode.GetSuper, expr.Line);
            EmitShort((ushort)nameIndex, expr.Line);
        }

        /// <summary>
        /// 生成上值信息
        /// </summary>
        /// <remarks>
        /// 每个上值包含：是否是局部变量（1字节）+ 索引（1字节）
        /// </remarks>
        private void EmitUpvalueInfo(List<Upvalue> upvalues, int line)
        {
            foreach (var upvalue in upvalues)
            {
                EmitByte((byte)(upvalue.IsLocal ? 1 : 0), line);
                EmitByte((byte)upvalue.Index, line);
            }
        }

        // ==================== 作用域管理 ====================

        /// <summary>
        /// 弹出局部变量到指定数量
        /// </summary>
        /// <remarks>
        /// 用于 break/continue 时清理循环内的局部变量
        /// </remarks>
        private void EmitPopLocals(int targetLocalCount, int line)
        {
            for (int i = _locals.Count - 1; i >= targetLocalCount; i--)
            {
                if (_locals[i].IsCaptured)
                {
                    // 被闭包捕获的变量需要关闭上值
                    Emit(Opcode.CloseUpvalue, line);
                }
                else
                {
                    Emit(Opcode.Pop, line);
                }
            }
        }

        /// <summary>
        /// 进入新作用域
        /// </summary>
        private void BeginScope() => _scopeDepth++;

        /// <summary>
        /// 退出当前作用域
        /// </summary>
        /// <remarks>
        /// 弹出该作用域内声明的所有局部变量
        /// </remarks>
        private void EndScope(int line)
        {
            _scopeDepth--;
            // 弹出该作用域内的所有局部变量
            while (_locals.Count > 0 && _locals[_locals.Count - 1].Depth > _scopeDepth)
            {
                if (_locals[_locals.Count - 1].IsCaptured)
                {
                    // 被闭包捕获的变量需要关闭上值
                    Emit(Opcode.CloseUpvalue, line);
                }
                else
                {
                    Emit(Opcode.Pop, line);
                }
                _locals.RemoveAt(_locals.Count - 1);
            }
        }

        /// <summary>
        /// 添加局部变量
        /// </summary>
        /// <exception cref="CompilerException">局部变量超过 255 个时抛出</exception>
        private void AddLocal(string name)
        {
            if (_locals.Count >= 255)
            {
                throw new CompilerException($"Too many local variables in function (max 255). Cannot add '{name}'.");
            }
            _locals.Add(new Local { Name = name, Depth = _scopeDepth });
        }

        /// <summary>
        /// 解析局部变量
        /// </summary>
        /// <returns>局部变量索引，未找到返回 -1</returns>
        private int ResolveLocal(string name)
        {
            // 从后向前查找（内层作用域优先）
            for (int i = _locals.Count - 1; i >= 0; i--)
            {
                if (_locals[i].Name == name) return i;
            }
            return -1;
        }

        /// <summary>
        /// 解析上值（闭包捕获的变量）
        /// </summary>
        /// <returns>上值索引，未找到返回 -1</returns>
        private int ResolveUpvalue(string name)
        {
            if (_enclosing == null) return -1;

            // 先在外层函数的局部变量中查找
            var local = _enclosing.ResolveLocal(name);
            if (local != -1)
            {
                // 标记为被捕获
                _enclosing._locals[local] = new Local
                {
                    Name = _enclosing._locals[local].Name,
                    Depth = _enclosing._locals[local].Depth,
                    IsCaptured = true
                };
                return AddUpvalue(local, true);
            }

            // 递归在外层函数的上值中查找
            var upvalue = _enclosing.ResolveUpvalue(name);
            if (upvalue != -1)
            {
                return AddUpvalue(upvalue, false);
            }

            return -1;
        }

        /// <summary>
        /// 添加上值
        /// </summary>
        /// <param name="index">在外层函数中的索引</param>
        /// <param name="isLocal">是否是外层函数的局部变量</param>
        /// <returns>上值索引</returns>
        /// <exception cref="CompilerException">上值超过 255 个时抛出</exception>
        private int AddUpvalue(int index, bool isLocal)
        {
            // 检查是否已存在相同的上值
            for (int i = 0; i < _upvalues.Count; i++)
            {
                if (_upvalues[i].Index == index && _upvalues[i].IsLocal == isLocal)
                    return i;
            }
            if (_upvalues.Count >= 255)
            {
                throw new CompilerException("Too many closure variables (upvalues) in function (max 255).");
            }
            _upvalues.Add(new Upvalue { Index = index, IsLocal = isLocal });
            return _upvalues.Count - 1;
        }

        // ==================== 常量折叠优化 ====================

        /// <summary>
        /// 尝试在编译时计算二元表达式
        /// </summary>
        private bool TryFoldBinary(BinaryExpr expr, out object result)
        {
            result = null;
            if (!TryGetConstant(expr.Left, out var left) || !TryGetConstant(expr.Right, out var right))
                return false;

            // 数字运算
            if (left is double l && right is double r)
            {
                switch (expr.Operator)
                {
                    case TokenType.Plus: result = l + r; return true;
                    case TokenType.Minus: result = l - r; return true;
                    case TokenType.Star: result = l * r; return true;
                    case TokenType.Slash: result = l / r; return true;
                    case TokenType.Percent: result = l % r; return true;
                    case TokenType.Less: result = l < r; return true;
                    case TokenType.LessEqual: result = l <= r; return true;
                    case TokenType.Greater: result = l > r; return true;
                    case TokenType.GreaterEqual: result = l >= r; return true;
                    case TokenType.EqualEqual: result = l == r; return true;
                    case TokenType.BangEqual: result = l != r; return true;
                    case TokenType.BitAnd: result = (double)((long)l & (long)r); return true;
                    case TokenType.BitOr: result = (double)((long)l | (long)r); return true;
                    case TokenType.BitXor: result = (double)((long)l ^ (long)r); return true;
                    case TokenType.LeftShift: result = (double)((long)l << (int)r); return true;
                    case TokenType.RightShift: result = (double)((long)l >> (int)r); return true;
                }
            }

            // 字符串拼接
            if (left is string ls && right is string rs && expr.Operator == TokenType.Plus)
            {
                result = ls + rs;
                return true;
            }

            // 布尔运算
            if (left is bool lb && right is bool rb)
            {
                switch (expr.Operator)
                {
                    case TokenType.EqualEqual: result = lb == rb; return true;
                    case TokenType.BangEqual: result = lb != rb; return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试在编译时计算一元表达式
        /// </summary>
        private bool TryFoldUnary(UnaryExpr expr, out object result)
        {
            result = null;
            if (!TryGetConstant(expr.Operand, out var operand))
                return false;

            switch (expr.Operator)
            {
                case TokenType.Minus when operand is double d:
                    result = -d;
                    return true;
                case TokenType.Bang when operand is bool b:
                    result = !b;
                    return true;
                case TokenType.BitNot when operand is double d:
                    result = (double)(~(long)d);
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试从表达式获取常量值（递归支持嵌套常量表达式）
        /// </summary>
        private bool TryGetConstant(Expr expr, out object value)
        {
            switch (expr)
            {
                case LiteralExpr lit:
                    value = lit.Value;
                    return true;
                case UnaryExpr unary when TryFoldUnary(unary, out value):
                    return true;
                case BinaryExpr binary when TryFoldBinary(binary, out value):
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

        // ==================== 字节码生成辅助 ====================

        /// <summary>生成操作码</summary>
        private void Emit(Opcode op, int line) => _bytecode.Emit(op, line);
        /// <summary>生成单字节</summary>
        private void EmitByte(byte b, int line) => _bytecode.EmitByte(b, line);
        /// <summary>生成双字节（大端序）</summary>
        private void EmitShort(ushort value, int line) => _bytecode.EmitShort(value, line);
        /// <summary>生成跳转指令，返回跳转偏移位置</summary>
        private int EmitJump(Opcode op, int line) => _bytecode.EmitJump(op, line);
        /// <summary>修补跳转偏移</summary>
        private void PatchJump(int offset) => _bytecode.PatchJump(offset);
        /// <summary>生成循环跳转指令</summary>
        private void EmitLoop(int loopStart, int line) => _bytecode.EmitLoop(loopStart, line);

        /// <summary>
        /// 检查语句是否是父类构造函数调用
        /// </summary>
        private static bool IsSuperConstructorCall(Stmt stmt, string superClassName)
        {
            if (superClassName == null) return false;
            return stmt is ExpressionStmt es &&
                   es.Expression is CallExpr call &&
                   call.Callee is SuperExpr super &&
                   super.Method == superClassName;
        }
    }

    /// <summary>
    /// 编译器异常
    /// </summary>
    public class CompilerException : Exception
    {
        public CompilerException(string message) : base(message) { }
    }
}
