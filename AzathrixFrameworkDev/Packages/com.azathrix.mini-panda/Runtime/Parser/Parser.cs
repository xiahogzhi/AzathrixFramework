using System;
using System.Collections.Generic;
using Azathrix.MiniPanda.Lexer;

namespace Azathrix.MiniPanda.Parser
{
    /// <summary>
    /// MiniPanda 语法解析器
    /// <para>
    /// 将词法单元（Token）序列解析为抽象语法树（AST），主要功能：
    /// <list type="bullet">
    /// <item>递归下降解析</item>
    /// <item>运算符优先级处理</item>
    /// <item>语法错误检测和报告</item>
    /// </list>
    /// </para>
    /// </summary>
    public class Parser
    {
        /// <summary>词法单元列表</summary>
        private readonly List<Token> _tokens;
        /// <summary>当前解析位置</summary>
        private int _current;

        /// <summary>
        /// 创建解析器实例
        /// </summary>
        /// <param name="tokens">词法单元列表</param>
        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
        }

        /// <summary>
        /// 解析词法单元为语句列表
        /// </summary>
        /// <returns>AST 语句列表</returns>
        public List<Stmt> Parse()
        {
            var statements = new List<Stmt>();
            SkipNewlines();
            while (!IsAtEnd())
            {
                statements.Add(Declaration());
                SkipNewlines();
            }
            return statements;
        }

        // ==================== 声明解析 ====================

        /// <summary>
        /// 解析声明（变量、函数、类、导入、枚举）
        /// </summary>
        private Stmt Declaration()
        {
            if (Match(TokenType.Var)) return VarDeclaration(false, false);
            if (Match(TokenType.Func)) return FuncDeclaration(false, false);
            if (Match(TokenType.Class)) return ClassDeclaration(false, false);
            if (Match(TokenType.Import)) return ImportDeclaration(false);
            if (Match(TokenType.Enum)) return EnumDeclaration(false);
            // global 修饰符
            if (Match(TokenType.Global))
            {
                if (Match(TokenType.Import)) return ImportDeclaration(true);
                if (Match(TokenType.Var)) return VarDeclaration(true, false);
                if (Match(TokenType.Func)) return FuncDeclaration(true, false);
                if (Match(TokenType.Class)) return ClassDeclaration(true, false);
                if (Match(TokenType.Enum)) return EnumDeclaration(true);
                throw Error("Expected 'import', 'var', 'func', 'class', or 'enum' after 'global'");
            }
            // export 修饰符（模块导出）
            if (Match(TokenType.Export))
            {
                if (Match(TokenType.Var)) return VarDeclaration(false, true);
                if (Match(TokenType.Func)) return FuncDeclaration(false, true);
                if (Match(TokenType.Class)) return ClassDeclaration(false, true);
                throw Error("Expected 'var', 'func', or 'class' after 'export'");
            }
            return Statement();
        }

        /// <summary>
        /// 解析变量声明
        /// </summary>
        /// <param name="isGlobal">是否是全局变量</param>
        /// <param name="isExport">是否导出</param>
        private Stmt VarDeclaration(bool isGlobal, bool isExport = false)
        {
            var name = Consume(TokenType.Identifier, "Expected variable name").Lexeme;
            Expr initializer = null;
            if (Match(TokenType.Equal))
            {
                initializer = Expression();
            }
            ConsumeStatementEnd();
            return new VarDecl { Name = name, Initializer = initializer, IsGlobal = isGlobal, IsExport = isExport, Line = Previous().Line };
        }

        /// <summary>
        /// 解析函数声明
        /// </summary>
        /// <param name="isGlobal">是否是全局函数</param>
        /// <param name="isExport">是否导出</param>
        private FuncDecl FuncDeclaration(bool isGlobal, bool isExport = false)
        {
            var name = Consume(TokenType.Identifier, "Expected function name").Lexeme;
            Consume(TokenType.LeftParen, "Expected '(' after function name");
            var parameters = new List<string>();
            var defaults = new List<Expr>();
            string restParam = null;
            bool hadDefault = false;

            // 解析参数列表
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    // 可变参数 ...args
                    if (Match(TokenType.DotDotDot))
                    {
                        restParam = Consume(TokenType.Identifier, "Expected rest parameter name").Lexeme;
                        break; // 可变参数必须是最后一个
                    }
                    var paramName = Consume(TokenType.Identifier, "Expected parameter name").Lexeme;
                    parameters.Add(paramName);
                    // 默认参数值
                    if (Match(TokenType.Equal))
                    {
                        defaults.Add(Expression());
                        hadDefault = true;
                    }
                    else
                    {
                        if (hadDefault) throw Error("Non-default parameter cannot follow default parameter");
                        defaults.Add(null);
                    }
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightParen, "Expected ')' after parameters");

            // 解析函数体
            List<Stmt> body;
            if (Check(TokenType.LeftBrace))
            {
                body = Block();
            }
            else
            {
                // 单语句函数体
                var stmt = Statement();
                body = new List<Stmt> { stmt };
            }

            return new FuncDecl { Name = name, Parameters = parameters, Defaults = defaults, RestParam = restParam, Body = body, IsGlobal = isGlobal, IsExport = isExport, Line = Previous().Line };
        }

        /// <summary>
        /// 解析类声明
        /// </summary>
        /// <param name="isGlobal">是否是全局类</param>
        /// <param name="isExport">是否导出</param>
        private Stmt ClassDeclaration(bool isGlobal, bool isExport = false)
        {
            var name = Consume(TokenType.Identifier, "Expected class name").Lexeme;
            // 解析继承
            string superClass = null;
            if (Match(TokenType.Colon))
            {
                superClass = Consume(TokenType.Identifier, "Expected superclass name").Lexeme;
            }
            Consume(TokenType.LeftBrace, "Expected '{' before class body");
            SkipNewlines();

            var fields = new List<VarDecl>();
            var methods = new List<FuncDecl>();
            var staticFields = new List<VarDecl>();
            var staticMethods = new List<FuncDecl>();

            // 解析类成员
            while (!Check(TokenType.RightBrace) && !IsAtEnd())
            {
                SkipNewlines();
                if (Check(TokenType.RightBrace)) break;

                // 检查 static 修饰符
                bool isStatic = Match(TokenType.Static);

                if (Match(TokenType.Var))
                {
                    // 字段声明
                    var fieldToken = Consume(TokenType.Identifier, "Expected field name");
                    Expr initializer = null;
                    if (Match(TokenType.Equal))
                    {
                        initializer = Expression();
                    }
                    ConsumeStatementEnd();
                    var field = new VarDecl { Name = fieldToken.Lexeme, Initializer = initializer, IsStatic = isStatic, Line = fieldToken.Line, Column = fieldToken.Column };
                    if (isStatic)
                        staticFields.Add(field);
                    else
                        fields.Add(field);
                }
                else if (Match(TokenType.Func))
                {
                    // 方法声明
                    var method = FuncDeclaration(false);
                    method.IsStatic = isStatic;
                    if (isStatic)
                        staticMethods.Add(method);
                    else
                        methods.Add(method);
                }
                else if (!isStatic && Check(TokenType.Identifier) && Peek().Lexeme == name && CheckNext(TokenType.LeftParen))
                {
                    // 构造函数: ClassName(...) { ... }
                    Advance(); // 消费类名
                    methods.Add(ParseConstructor(name));
                }
                else
                {
                    throw new ParserException($"Unexpected token in class body: {Peek().Type} at line {Peek().Line}, column {Peek().Column}");
                }
                SkipNewlines();
            }

            Consume(TokenType.RightBrace, "Expected '}' after class body");
            return new ClassDecl { Name = name, SuperClass = superClass, Fields = fields, Methods = methods, StaticFields = staticFields, StaticMethods = staticMethods, IsGlobal = isGlobal, IsExport = isExport, Line = Previous().Line };
        }

        /// <summary>
        /// 解析构造函数
        /// </summary>
        private FuncDecl ParseConstructor(string className)
        {
            Consume(TokenType.LeftParen, "Expected '(' after constructor name");
            var parameters = new List<string>();
            var defaults = new List<Expr>();
            string restParam = null;
            bool hadDefault = false;
            // 解析参数列表
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    if (Match(TokenType.DotDotDot))
                    {
                        restParam = Consume(TokenType.Identifier, "Expected rest parameter name").Lexeme;
                        break;
                    }
                    var paramName = Consume(TokenType.Identifier, "Expected parameter name").Lexeme;
                    parameters.Add(paramName);
                    if (Match(TokenType.Equal))
                    {
                        defaults.Add(Expression());
                        hadDefault = true;
                    }
                    else
                    {
                        if (hadDefault) throw Error("Non-default parameter cannot follow default parameter");
                        defaults.Add(null);
                    }
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightParen, "Expected ')' after parameters");
            var body = Block();
            return new FuncDecl { Name = className, Parameters = parameters, Defaults = defaults, RestParam = restParam, Body = body, Line = Previous().Line };
        }

        /// <summary>检查下一个词法单元类型</summary>
        private bool CheckNext(TokenType type)
        {
            if (_current + 1 >= _tokens.Count) return false;
            return _tokens[_current + 1].Type == type;
        }

        /// <summary>
        /// 解析导入声明
        /// </summary>
        private Stmt ImportDeclaration(bool isGlobal)
        {
            var path = Consume(TokenType.String, "Expected import path").Literal as string;
            string alias = null;
            if (Match(TokenType.As))
            {
                alias = Consume(TokenType.Identifier, "Expected alias name").Lexeme;
            }
            ConsumeStatementEnd();
            return new ImportStmt { Path = path, Alias = alias, IsGlobal = isGlobal, Line = Previous().Line };
        }

        /// <summary>
        /// 解析枚举声明
        /// </summary>
        private Stmt EnumDeclaration(bool isGlobal)
        {
            var line = Previous().Line;
            var name = Consume(TokenType.Identifier, "Expected enum name").Lexeme;
            Consume(TokenType.LeftBrace, "Expected '{' after enum name");
            SkipNewlines();

            var members = new List<(string Name, Expr Value)>();
            int autoValue = 0;

            // 解析枚举成员
            while (!Check(TokenType.RightBrace) && !IsAtEnd())
            {
                var memberName = Consume(TokenType.Identifier, "Expected enum member name").Lexeme;
                Expr memberValue = null;

                if (Match(TokenType.Equal))
                {
                    // 显式指定值
                    memberValue = Expression();
                    // 尝试提取数值用于自动递增
                    if (memberValue is LiteralExpr lit && lit.Value is double d)
                    {
                        autoValue = (int)d + 1;
                    }
                }
                else
                {
                    // 自动递增值
                    memberValue = new LiteralExpr { Value = (double)autoValue++, Line = Previous().Line };
                }

                members.Add((memberName, memberValue));

                SkipNewlines();
                if (!Check(TokenType.RightBrace))
                {
                    if (!Match(TokenType.Comma))
                    {
                        SkipNewlines();
                    }
                    else
                    {
                        SkipNewlines();
                    }
                }
            }

            Consume(TokenType.RightBrace, "Expected '}' after enum members");
            return new EnumDecl { Name = name, Members = members, IsGlobal = isGlobal, Line = line };
        }

        // ==================== 语句解析 ====================

        /// <summary>
        /// 解析语句
        /// </summary>
        private Stmt Statement()
        {
            if (Match(TokenType.If)) return IfStatement();
            if (Match(TokenType.While)) return WhileStatement();
            if (Match(TokenType.For)) return ForStatement();
            if (Match(TokenType.Return)) return ReturnStatement();
            if (Match(TokenType.Break)) { ConsumeStatementEnd(); return new BreakStmt { Line = Previous().Line }; }
            if (Match(TokenType.Continue)) { ConsumeStatementEnd(); return new ContinueStmt { Line = Previous().Line }; }
            if (Match(TokenType.Try)) return TryStatement();
            if (Match(TokenType.Throw)) return ThrowStatement();
            if (Check(TokenType.LeftBrace)) return new BlockStmt { Statements = Block(), Line = Previous().Line };
            return ExpressionStatement();
        }

        /// <summary>解析 if 语句</summary>
        private Stmt IfStatement()
        {
            var condition = Expression();
            List<Stmt> thenBranch;
            List<Stmt> elseBranch = null;

            // 解析 then 分支
            if (Check(TokenType.LeftBrace))
            {
                thenBranch = Block();
            }
            else
            {
                thenBranch = new List<Stmt> { Statement() };
            }

            // 解析 else 分支
            SkipNewlines();
            if (Match(TokenType.Else))
            {
                if (Match(TokenType.If))
                {
                    // else if
                    elseBranch = new List<Stmt> { IfStatement() };
                }
                else if (Check(TokenType.LeftBrace))
                {
                    elseBranch = Block();
                }
                else
                {
                    elseBranch = new List<Stmt> { Statement() };
                }
            }

            return new IfStmt { Condition = condition, ThenBranch = thenBranch, ElseBranch = elseBranch, Line = Previous().Line };
        }

        /// <summary>解析 while 语句</summary>
        private Stmt WhileStatement()
        {
            var condition = Expression();
            List<Stmt> body;
            if (Check(TokenType.LeftBrace))
            {
                body = Block();
            }
            else
            {
                body = new List<Stmt> { Statement() };
            }
            return new WhileStmt { Condition = condition, Body = body, Line = Previous().Line };
        }

        /// <summary>
        /// 解析 for-in 语句
        /// </summary>
        /// <remarks>
        /// 支持两种形式：
        /// - for v in iterable { }
        /// - for k, v in iterable { }
        /// </remarks>
        private Stmt ForStatement()
        {
            var firstVar = Consume(TokenType.Identifier, "Expected variable name").Lexeme;
            string keyVar = null;
            string valueVar = firstVar;

            // 检查 "for k, v in ..." 语法
            if (Match(TokenType.Comma))
            {
                keyVar = firstVar;
                valueVar = Consume(TokenType.Identifier, "Expected value variable name").Lexeme;
            }

            Consume(TokenType.In, "Expected 'in' after variable");
            var iterable = Expression();
            List<Stmt> body;
            if (Check(TokenType.LeftBrace))
            {
                body = Block();
            }
            else
            {
                body = new List<Stmt> { Statement() };
            }
            return new ForStmt { Variable = valueVar, KeyVariable = keyVar, Iterable = iterable, Body = body, Line = Previous().Line };
        }

        /// <summary>解析 return 语句</summary>
        private Stmt ReturnStatement()
        {
            Expr value = null;
            // 检查是否有返回值
            if (!Check(TokenType.Newline) && !Check(TokenType.Semicolon) && !Check(TokenType.RightBrace) && !IsAtEnd())
            {
                value = Expression();
            }
            ConsumeStatementEnd();
            return new ReturnStmt { Value = value, Line = Previous().Line };
        }

        /// <summary>解析 try/catch/finally 语句</summary>
        private Stmt TryStatement()
        {
            var line = Previous().Line;
            var tryBlock = Block();

            string catchVar = null;
            List<Stmt> catchBlock = null;
            List<Stmt> finallyBlock = null;

            // 解析 catch 块
            SkipNewlines();
            if (Match(TokenType.Catch))
            {
                if (Match(TokenType.LeftParen))
                {
                    catchVar = Consume(TokenType.Identifier, "Expected exception variable name").Lexeme;
                    Consume(TokenType.RightParen, "Expected ')' after catch variable");
                }
                catchBlock = Block();
                SkipNewlines();
            }

            // 解析 finally 块
            if (Match(TokenType.Finally))
            {
                finallyBlock = Block();
            }

            if (catchBlock == null && finallyBlock == null)
            {
                throw new ParserException("try statement requires catch or finally block");
            }

            return new TryStmt
            {
                TryBlock = tryBlock,
                CatchVar = catchVar,
                CatchBlock = catchBlock,
                FinallyBlock = finallyBlock,
                Line = line
            };
        }

        /// <summary>解析 throw 语句</summary>
        private Stmt ThrowStatement()
        {
            var line = Previous().Line;
            var value = Expression();
            ConsumeStatementEnd();
            return new ThrowStmt { Value = value, Line = line };
        }

        /// <summary>解析表达式语句</summary>
        private Stmt ExpressionStatement()
        {
            var expr = Expression();
            ConsumeStatementEnd();
            return new ExpressionStmt { Expression = expr, Line = Previous().Line };
        }

        /// <summary>解析代码块</summary>
        private List<Stmt> Block()
        {
            Consume(TokenType.LeftBrace, "Expected '{'");
            SkipNewlines();
            var statements = new List<Stmt>();
            while (!Check(TokenType.RightBrace) && !IsAtEnd())
            {
                statements.Add(Declaration());
                SkipNewlines();
            }
            Consume(TokenType.RightBrace, "Expected '}'");
            return statements;
        }

        // ==================== 表达式解析 ====================

        /// <summary>解析表达式（入口）</summary>
        private Expr Expression() => Ternary();

        /// <summary>解析三元运算符表达式</summary>
        private Expr Ternary()
        {
            var expr = Assignment();
            if (Match(TokenType.Question))
            {
                var thenExpr = Expression();
                Consume(TokenType.Colon, "Expected ':' in ternary expression");
                var elseExpr = Ternary();
                return new TernaryExpr { Condition = expr, ThenExpr = thenExpr, ElseExpr = elseExpr, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析赋值表达式</summary>
        private Expr Assignment()
        {
            var expr = Or();

            // 简单赋值
            if (Match(TokenType.Equal))
            {
                var value = Assignment();
                if (expr is IdentifierExpr id)
                {
                    return new AssignExpr { Target = id, Value = value, Line = id.Line };
                }
                if (expr is GetExpr get)
                {
                    return new SetExpr { Object = get.Object, Name = get.Name, Value = value, Line = get.Line };
                }
                if (expr is IndexGetExpr idx)
                {
                    return new IndexSetExpr { Object = idx.Object, Index = idx.Index, Value = value, Line = idx.Line };
                }
                throw Error("Invalid assignment target");
            }

            // 复合赋值 +=, -=, *=, /=, %=
            if (Match(TokenType.PlusEqual, TokenType.MinusEqual, TokenType.StarEqual, TokenType.SlashEqual, TokenType.PercentEqual))
            {
                var op = Previous().Type;
                var binaryOp = op switch
                {
                    TokenType.PlusEqual => TokenType.Plus,
                    TokenType.MinusEqual => TokenType.Minus,
                    TokenType.StarEqual => TokenType.Star,
                    TokenType.SlashEqual => TokenType.Slash,
                    TokenType.PercentEqual => TokenType.Percent,
                    _ => TokenType.Plus
                };
                var value = Assignment();
                if (expr is IdentifierExpr || expr is GetExpr || expr is IndexGetExpr)
                {
                    return new CompoundAssignExpr { Target = expr, Operator = binaryOp, Value = value, Line = Previous().Line };
                }
                throw Error("Invalid compound assignment target");
            }

            return expr;
        }

        /// <summary>解析逻辑或表达式</summary>
        private Expr Or()
        {
            var expr = NullCoalesce();
            while (Match(TokenType.Or))
            {
                var op = Previous().Type;
                var right = NullCoalesce();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析空值合并表达式</summary>
        private Expr NullCoalesce()
        {
            var expr = And();
            while (Match(TokenType.QuestionQuestion))
            {
                var right = And();
                expr = new NullCoalesceExpr { Left = expr, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析逻辑与表达式</summary>
        private Expr And()
        {
            var expr = BitOr();
            while (Match(TokenType.And))
            {
                var op = Previous().Type;
                var right = BitOr();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析按位或表达式</summary>
        private Expr BitOr()
        {
            var expr = BitXor();
            while (Match(TokenType.BitOr))
            {
                var op = Previous().Type;
                var right = BitXor();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析按位异或表达式</summary>
        private Expr BitXor()
        {
            var expr = BitAnd();
            while (Match(TokenType.BitXor))
            {
                var op = Previous().Type;
                var right = BitAnd();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析按位与表达式</summary>
        private Expr BitAnd()
        {
            var expr = Equality();
            while (Match(TokenType.BitAnd))
            {
                var op = Previous().Type;
                var right = Equality();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析相等性表达式</summary>
        private Expr Equality()
        {
            var expr = Comparison();
            while (Match(TokenType.EqualEqual, TokenType.BangEqual))
            {
                var op = Previous().Type;
                var right = Comparison();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析比较表达式</summary>
        private Expr Comparison()
        {
            var expr = Shift();
            while (Match(TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual))
            {
                var op = Previous().Type;
                var right = Shift();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析位移表达式</summary>
        private Expr Shift()
        {
            var expr = Term();
            while (Match(TokenType.LeftShift, TokenType.RightShift))
            {
                var op = Previous().Type;
                var right = Term();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析加减表达式</summary>
        private Expr Term()
        {
            var expr = Factor();
            while (Match(TokenType.Plus, TokenType.Minus))
            {
                var op = Previous().Type;
                var right = Factor();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析乘除模表达式</summary>
        private Expr Factor()
        {
            var expr = Unary();
            while (Match(TokenType.Star, TokenType.Slash, TokenType.Percent))
            {
                var op = Previous().Type;
                var right = Unary();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析一元表达式</summary>
        private Expr Unary()
        {
            // 前缀自增/自减
            if (Match(TokenType.PlusPlus, TokenType.MinusMinus))
            {
                var op = Previous().Type;
                var operand = Unary();
                return new UpdateExpr { Target = operand, Operator = op, IsPrefix = true, Line = Previous().Line };
            }
            // 一元运算符
            if (Match(TokenType.Bang, TokenType.Minus, TokenType.BitNot))
            {
                var op = Previous().Type;
                var operand = Unary();
                return new UnaryExpr { Operator = op, Operand = operand, Line = Previous().Line };
            }
            return Postfix();
        }

        /// <summary>解析后缀表达式</summary>
        private Expr Postfix()
        {
            var expr = Call();
            // 后缀自增/自减
            if (Match(TokenType.PlusPlus, TokenType.MinusMinus))
            {
                var op = Previous().Type;
                return new UpdateExpr { Target = expr, Operator = op, IsPrefix = false, Line = Previous().Line };
            }
            return expr;
        }

        /// <summary>解析调用表达式</summary>
        private Expr Call()
        {
            var expr = Primary();

            while (true)
            {
                if (Match(TokenType.LeftParen))
                {
                    expr = FinishCall(expr);
                }
                else if (Match(TokenType.Dot))
                {
                    var name = Consume(TokenType.Identifier, "Expected property name").Lexeme;
                    expr = new GetExpr { Object = expr, Name = name, Line = Previous().Line };
                }
                else if (Match(TokenType.QuestionDot))
                {
                    var name = Consume(TokenType.Identifier, "Expected property name").Lexeme;
                    expr = new GetExpr { Object = expr, Name = name, IsOptional = true, Line = Previous().Line };
                }
                else if (Match(TokenType.LeftBracket))
                {
                    var index = Expression();
                    Consume(TokenType.RightBracket, "Expected ']'");
                    expr = new IndexGetExpr { Object = expr, Index = index, Line = Previous().Line };
                }
                else if (Match(TokenType.QuestionBracket))
                {
                    var index = Expression();
                    Consume(TokenType.RightBracket, "Expected ']'");
                    expr = new IndexGetExpr { Object = expr, Index = index, IsOptional = true, Line = Previous().Line };
                }
                else
                {
                    break;
                }
            }

            return expr;
        }

        /// <summary>完成函数调用参数解析</summary>
        /// <param name="callee">被调用的表达式</param>
        /// <returns>调用表达式节点</returns>
        private Expr FinishCall(Expr callee)
        {
            var arguments = new List<Expr>();
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    arguments.Add(Expression());
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightParen, "Expected ')' after arguments");
            return new CallExpr { Callee = callee, Arguments = arguments, Line = Previous().Line };
        }

        /// <summary>
        /// 解析基本表达式（最高优先级）
        /// </summary>
        /// <remarks>
        /// 处理：字面量、标识符、this、super、括号表达式、lambda、数组、对象
        /// </remarks>
        private Expr Primary()
        {
            if (Match(TokenType.Number, TokenType.True, TokenType.False, TokenType.Null))
            {
                var token = Previous();
                object value = token.Type switch
                {
                    TokenType.Number => token.Literal,
                    TokenType.True => true,
                    TokenType.False => false,
                    TokenType.Null => null,
                    _ => null
                };
                return new LiteralExpr { Value = value, Line = token.Line };
            }

            if (Match(TokenType.String))
            {
                var token = Previous();
                if (token.Literal is List<object> parts)
                {
                    // 预解析插值表达式，避免编译时重复解析
                    var parsedParts = new List<object>(parts.Count);
                    foreach (var part in parts)
                    {
                        if (part is StringInterpolation interp)
                        {
                            var lexer = new Lexer.Lexer(interp.Expression);
                            var tokens = lexer.Tokenize();
                            var parser = new Parser(tokens);
                            var stmts = parser.Parse();
                            if (stmts.Count == 1 && stmts[0] is ExpressionStmt exprStmt)
                            {
                                parsedParts.Add(exprStmt.Expression);
                            }
                            else
                            {
                                throw Error("String interpolation must contain a single expression");
                            }
                        }
                        else
                        {
                            parsedParts.Add(part);
                        }
                    }
                    return new StringExpr { Parts = parsedParts, Line = token.Line };
                }
                return new LiteralExpr { Value = token.Literal, Line = token.Line };
            }

            if (Match(TokenType.This))
            {
                return new ThisExpr { Line = Previous().Line };
            }

            if (Match(TokenType.Super))
            {
                Consume(TokenType.Dot, "Expected '.' after 'super'");
                var method = Consume(TokenType.Identifier, "Expected superclass method name").Lexeme;
                return new SuperExpr { Method = method, Line = Previous().Line };
            }

            if (Match(TokenType.Identifier))
            {
                return new IdentifierExpr { Name = Previous().Lexeme, Line = Previous().Line };
            }

            if (Match(TokenType.LeftParen))
            {
                if (IsLambdaStart())
                {
                    return Lambda();
                }

                var expr = Expression();
                Consume(TokenType.RightParen, "Expected ')'");
                return expr;
            }

            if (Match(TokenType.LeftBracket))
            {
                return ArrayLiteral();
            }

            if (Match(TokenType.LeftBrace))
            {
                return ObjectLiteral();
            }

            throw Error($"Unexpected token: {Peek().Type}");
        }

        /// <summary>
        /// 检测当前位置是否为 lambda 表达式的开始
        /// </summary>
        /// <remarks>
        /// 向前查看判断是否为 (params) => 形式，支持默认参数和 rest 参数
        /// </remarks>
        private bool IsLambdaStart()
        {
            var idx = _current;
            if (idx >= _tokens.Count) return false;

            if (_tokens[idx].Type == TokenType.RightParen)
            {
                idx++;
                while (idx < _tokens.Count && _tokens[idx].Type == TokenType.Newline) idx++;
                return idx < _tokens.Count && _tokens[idx].Type == TokenType.Arrow;
            }

            // Handle ...rest parameter
            if (_tokens[idx].Type == TokenType.DotDotDot)
            {
                idx++;
                if (idx >= _tokens.Count || _tokens[idx].Type != TokenType.Identifier) return false;
                idx++;
                if (idx >= _tokens.Count || _tokens[idx].Type != TokenType.RightParen) return false;
                idx++;
                while (idx < _tokens.Count && _tokens[idx].Type == TokenType.Newline) idx++;
                return idx < _tokens.Count && _tokens[idx].Type == TokenType.Arrow;
            }

            if (_tokens[idx].Type != TokenType.Identifier) return false;

            while (true)
            {
                idx++;
                if (idx >= _tokens.Count) return false;

                // Skip default value: = expression
                if (_tokens[idx].Type == TokenType.Equal)
                {
                    idx++;
                    int parenDepth = 0;
                    while (idx < _tokens.Count)
                    {
                        var t = _tokens[idx].Type;
                        if (t == TokenType.LeftParen) parenDepth++;
                        else if (t == TokenType.RightParen)
                        {
                            if (parenDepth == 0) break;
                            parenDepth--;
                        }
                        else if (t == TokenType.Comma && parenDepth == 0) break;
                        idx++;
                    }
                    if (idx >= _tokens.Count) return false;
                }

                if (_tokens[idx].Type == TokenType.Comma)
                {
                    idx++;
                    // Handle ...rest after comma
                    if (idx < _tokens.Count && _tokens[idx].Type == TokenType.DotDotDot)
                    {
                        idx++;
                        if (idx >= _tokens.Count || _tokens[idx].Type != TokenType.Identifier) return false;
                        idx++;
                        if (idx >= _tokens.Count || _tokens[idx].Type != TokenType.RightParen) return false;
                        idx++;
                        while (idx < _tokens.Count && _tokens[idx].Type == TokenType.Newline) idx++;
                        return idx < _tokens.Count && _tokens[idx].Type == TokenType.Arrow;
                    }
                    if (idx >= _tokens.Count || _tokens[idx].Type != TokenType.Identifier)
                        return false;
                    continue;
                }

                if (_tokens[idx].Type == TokenType.RightParen)
                {
                    idx++;
                    while (idx < _tokens.Count && _tokens[idx].Type == TokenType.Newline) idx++;
                    return idx < _tokens.Count && _tokens[idx].Type == TokenType.Arrow;
                }

                return false;
            }
        }

        /// <summary>
        /// 解析 lambda 表达式
        /// </summary>
        /// <remarks>
        /// 语法：(params) => expr 或 (params) => { stmts }
        /// 支持默认参数和 rest 参数
        /// </remarks>
        private Expr Lambda()
        {
            var parameters = new List<string>();
            var defaults = new List<Expr>();
            string restParam = null;
            bool hadDefault = false;
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    if (Match(TokenType.DotDotDot))
                    {
                        restParam = Consume(TokenType.Identifier, "Expected rest parameter name").Lexeme;
                        break;
                    }
                    var paramName = Consume(TokenType.Identifier, "Expected parameter name").Lexeme;
                    parameters.Add(paramName);
                    if (Match(TokenType.Equal))
                    {
                        defaults.Add(Expression());
                        hadDefault = true;
                    }
                    else
                    {
                        if (hadDefault) throw Error("Non-default parameter cannot follow default parameter");
                        defaults.Add(null);
                    }
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightParen, "Expected ')' after lambda parameters");
            Consume(TokenType.Arrow, "Expected '=>' after lambda parameters");

            if (Check(TokenType.LeftBrace))
            {
                var body = Block();
                return new LambdaExpr { Parameters = parameters, Defaults = defaults, RestParam = restParam, Block = body, Line = Previous().Line };
            }
            else
            {
                var body = Expression();
                return new LambdaExpr { Parameters = parameters, Defaults = defaults, RestParam = restParam, Body = body, Line = Previous().Line };
            }
        }

        /// <summary>解析数组字面量 [elem1, elem2, ...]</summary>
        private Expr ArrayLiteral()
        {
            var elements = new List<Expr>();
            if (!Check(TokenType.RightBracket))
            {
                do
                {
                    elements.Add(Expression());
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightBracket, "Expected ']'");
            return new ArrayExpr { Elements = elements, Line = Previous().Line };
        }

        /// <summary>解析对象字面量 { key: value, ... }</summary>
        private Expr ObjectLiteral()
        {
            var properties = new List<(string, Expr)>();
            SkipNewlines();
            if (!Check(TokenType.RightBrace))
            {
                do
                {
                    SkipNewlines();
                    string key;
                    if (Match(TokenType.Identifier))
                    {
                        key = Previous().Lexeme;
                    }
                    else if (Match(TokenType.String))
                    {
                        key = Previous().Literal as string;
                    }
                    else
                    {
                        throw Error("Expected property name");
                    }
                    Consume(TokenType.Colon, "Expected ':' after property name");
                    var value = Expression();
                    properties.Add((key, value));
                    SkipNewlines();
                } while (Match(TokenType.Comma));
            }
            SkipNewlines();
            Consume(TokenType.RightBrace, "Expected '}'");
            return new ObjectExpr { Properties = properties, Line = Previous().Line };
        }

        #region 辅助方法

        /// <summary>尝试匹配指定类型的 token，匹配成功则前进</summary>
        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }

        /// <summary>检查当前 token 是否为指定类型（不前进）</summary>
        private bool Check(TokenType type) => !IsAtEnd() && Peek().Type == type;
        /// <summary>前进到下一个 token</summary>
        private Token Advance() { if (!IsAtEnd()) _current++; return Previous(); }
        /// <summary>是否到达 token 流末尾</summary>
        private bool IsAtEnd() => Peek().Type == TokenType.Eof;
        /// <summary>获取当前 token</summary>
        private Token Peek() => _tokens[_current];
        /// <summary>获取下一个 token（不前进）</summary>
        private Token PeekNext() => _current + 1 < _tokens.Count ? _tokens[_current + 1] : _tokens[_current];
        /// <summary>获取上一个 token</summary>
        private Token Previous() => _current > 0 ? _tokens[_current - 1] : _tokens[0];

        /// <summary>消费指定类型的 token，不匹配则抛出异常</summary>
        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) return Advance();
            throw Error(message);
        }

        /// <summary>消费语句结束符（分号、换行或隐式结束）</summary>
        private void ConsumeStatementEnd()
        {
            if (Match(TokenType.Semicolon)) return;
            if (Match(TokenType.Newline)) return;
            if (Check(TokenType.RightBrace)) return;
            if (IsAtEnd()) return;
            // Allow implicit statement end
        }

        /// <summary>跳过所有换行符</summary>
        private void SkipNewlines()
        {
            while (Match(TokenType.Newline)) { }
        }

        /// <summary>创建解析错误异常</summary>
        private Exception Error(string message)
        {
            var token = Peek();
            return new ParserException(message, token.Line, token.Column);
        }

        #endregion
    }

    /// <summary>
    /// 解析器异常
    /// </summary>
    public class ParserException : Exception
    {
        public int Line { get; }
        public int Column { get; }

        public ParserException(string message, int line = 1, int column = 1)
            : base($"{message} at line {line}, column {column}")
        {
            Line = line;
            Column = column;
        }
    }
}
