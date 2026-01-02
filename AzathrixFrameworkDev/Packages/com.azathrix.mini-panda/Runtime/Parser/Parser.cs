using System;
using System.Collections.Generic;
using Azathrix.MiniPanda.Lexer;

namespace Azathrix.MiniPanda.Parser
{
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _current;

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
        }

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

        private Stmt Declaration()
        {
            if (Match(TokenType.Var)) return VarDeclaration(false);
            if (Match(TokenType.Func)) return FuncDeclaration(false);
            if (Match(TokenType.Class)) return ClassDeclaration(false);
            if (Match(TokenType.Import)) return ImportDeclaration(false);
            if (Match(TokenType.Global))
            {
                if (Match(TokenType.Import)) return ImportDeclaration(true);
                if (Match(TokenType.Var)) return VarDeclaration(true);
                if (Match(TokenType.Func)) return FuncDeclaration(true);
                if (Match(TokenType.Class)) return ClassDeclaration(true);
                throw Error("Expected 'import', 'var', 'func', or 'class' after 'global'");
            }
            return Statement();
        }

        private Stmt VarDeclaration(bool isGlobal)
        {
            var name = Consume(TokenType.Identifier, "Expected variable name").Lexeme;
            Expr initializer = null;
            if (Match(TokenType.Equal))
            {
                initializer = Expression();
            }
            ConsumeStatementEnd();
            return new VarDecl { Name = name, Initializer = initializer, IsGlobal = isGlobal, Line = Previous().Line };
        }

        private FuncDecl FuncDeclaration(bool isGlobal)
        {
            var name = Consume(TokenType.Identifier, "Expected function name").Lexeme;
            Consume(TokenType.LeftParen, "Expected '(' after function name");
            var parameters = new List<string>();
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    parameters.Add(Consume(TokenType.Identifier, "Expected parameter name").Lexeme);
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightParen, "Expected ')' after parameters");

            List<Stmt> body;
            if (Check(TokenType.LeftBrace))
            {
                body = Block();
            }
            else
            {
                // Single expression function: func double(x) return x * 2
                var stmt = Statement();
                body = new List<Stmt> { stmt };
            }

            return new FuncDecl { Name = name, Parameters = parameters, Body = body, IsGlobal = isGlobal, Line = Previous().Line };
        }

        private Stmt ClassDeclaration(bool isGlobal)
        {
            var name = Consume(TokenType.Identifier, "Expected class name").Lexeme;
            string superClass = null;
            if (Match(TokenType.Colon))
            {
                superClass = Consume(TokenType.Identifier, "Expected superclass name").Lexeme;
            }
            Consume(TokenType.LeftBrace, "Expected '{' before class body");
            SkipNewlines();

            var fields = new List<VarDecl>();
            var methods = new List<FuncDecl>();

            while (!Check(TokenType.RightBrace) && !IsAtEnd())
            {
                SkipNewlines();
                if (Check(TokenType.RightBrace)) break;
                if (Match(TokenType.Var))
                {
                    var fieldToken = Consume(TokenType.Identifier, "Expected field name");
                    Expr initializer = null;
                    if (Match(TokenType.Equal))
                    {
                        initializer = Expression();
                    }
                    ConsumeStatementEnd();
                    fields.Add(new VarDecl { Name = fieldToken.Lexeme, Initializer = initializer, Line = fieldToken.Line, Column = fieldToken.Column });
                }
                else if (Match(TokenType.Func))
                {
                    methods.Add(FuncDeclaration(false));
                }
                else if (Check(TokenType.Identifier) && Peek().Lexeme == name && CheckNext(TokenType.LeftParen))
                {
                    // Constructor: ClassName(...) { ... }
                    Advance(); // consume class name
                    methods.Add(ParseConstructor(name));
                }
                else
                {
                    throw new ParserException($"Unexpected token in class body: {Peek().Type} at line {Peek().Line}, column {Peek().Column}");
                }
                SkipNewlines();
            }

            Consume(TokenType.RightBrace, "Expected '}' after class body");
            return new ClassDecl { Name = name, SuperClass = superClass, Fields = fields, Methods = methods, IsGlobal = isGlobal, Line = Previous().Line };
        }

        private FuncDecl ParseConstructor(string className)
        {
            Consume(TokenType.LeftParen, "Expected '(' after constructor name");
            var parameters = new List<string>();
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    parameters.Add(Consume(TokenType.Identifier, "Expected parameter name").Lexeme);
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightParen, "Expected ')' after parameters");
            var body = Block();
            return new FuncDecl { Name = className, Parameters = parameters, Body = body, Line = Previous().Line };
        }

        private bool CheckNext(TokenType type)
        {
            if (_current + 1 >= _tokens.Count) return false;
            return _tokens[_current + 1].Type == type;
        }

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

        private Stmt Statement()
        {
            if (Match(TokenType.If)) return IfStatement();
            if (Match(TokenType.While)) return WhileStatement();
            if (Match(TokenType.For)) return ForStatement();
            if (Match(TokenType.Return)) return ReturnStatement();
            if (Match(TokenType.Break)) { ConsumeStatementEnd(); return new BreakStmt { Line = Previous().Line }; }
            if (Match(TokenType.Continue)) { ConsumeStatementEnd(); return new ContinueStmt { Line = Previous().Line }; }
            if (Check(TokenType.LeftBrace)) return new BlockStmt { Statements = Block(), Line = Previous().Line };
            return ExpressionStatement();
        }

        private Stmt IfStatement()
        {
            var condition = Expression();
            List<Stmt> thenBranch;
            List<Stmt> elseBranch = null;

            if (Check(TokenType.LeftBrace))
            {
                thenBranch = Block();
            }
            else
            {
                thenBranch = new List<Stmt> { Statement() };
            }

            SkipNewlines();
            if (Match(TokenType.Else))
            {
                if (Match(TokenType.If))
                {
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

        private Stmt ForStatement()
        {
            var variable = Consume(TokenType.Identifier, "Expected variable name").Lexeme;
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
            return new ForStmt { Variable = variable, Iterable = iterable, Body = body, Line = Previous().Line };
        }

        private Stmt ReturnStatement()
        {
            Expr value = null;
            if (!Check(TokenType.Newline) && !Check(TokenType.Semicolon) && !Check(TokenType.RightBrace) && !IsAtEnd())
            {
                value = Expression();
            }
            ConsumeStatementEnd();
            return new ReturnStmt { Value = value, Line = Previous().Line };
        }

        private Stmt ExpressionStatement()
        {
            var expr = Expression();
            ConsumeStatementEnd();
            return new ExpressionStmt { Expression = expr, Line = Previous().Line };
        }

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

        private Expr Expression() => Ternary();

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

        private Expr Assignment()
        {
            var expr = Or();

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

        private Expr Or()
        {
            var expr = And();
            while (Match(TokenType.Or))
            {
                var op = Previous().Type;
                var right = And();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

        private Expr And()
        {
            var expr = Equality();
            while (Match(TokenType.And))
            {
                var op = Previous().Type;
                var right = Equality();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

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

        private Expr Comparison()
        {
            var expr = Term();
            while (Match(TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual))
            {
                var op = Previous().Type;
                var right = Term();
                expr = new BinaryExpr { Left = expr, Operator = op, Right = right, Line = Previous().Line };
            }
            return expr;
        }

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

        private Expr Unary()
        {
            if (Match(TokenType.PlusPlus, TokenType.MinusMinus))
            {
                var op = Previous().Type;
                var operand = Unary();
                return new UpdateExpr { Target = operand, Operator = op, IsPrefix = true, Line = Previous().Line };
            }
            if (Match(TokenType.Bang, TokenType.Minus))
            {
                var op = Previous().Type;
                var operand = Unary();
                return new UnaryExpr { Operator = op, Operand = operand, Line = Previous().Line };
            }
            return Postfix();
        }

        private Expr Postfix()
        {
            var expr = Call();
            if (Match(TokenType.PlusPlus, TokenType.MinusMinus))
            {
                var op = Previous().Type;
                return new UpdateExpr { Target = expr, Operator = op, IsPrefix = false, Line = Previous().Line };
            }
            return expr;
        }

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
                else if (Match(TokenType.LeftBracket))
                {
                    var index = Expression();
                    Consume(TokenType.RightBracket, "Expected ']'");
                    expr = new IndexGetExpr { Object = expr, Index = index, Line = Previous().Line };
                }
                else
                {
                    break;
                }
            }

            return expr;
        }

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
                    return new StringExpr { Parts = parts, Line = token.Line };
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

            if (_tokens[idx].Type != TokenType.Identifier) return false;

            while (true)
            {
                idx++;
                if (idx >= _tokens.Count) return false;

                if (_tokens[idx].Type == TokenType.Comma)
                {
                    idx++;
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

        private Expr Lambda()
        {
            var parameters = new List<string>();
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    parameters.Add(Consume(TokenType.Identifier, "Expected parameter name").Lexeme);
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightParen, "Expected ')' after lambda parameters");
            Consume(TokenType.Arrow, "Expected '=>' after lambda parameters");

            if (Check(TokenType.LeftBrace))
            {
                var body = Block();
                return new LambdaExpr { Parameters = parameters, Block = body, Line = Previous().Line };
            }
            else
            {
                var body = Expression();
                return new LambdaExpr { Parameters = parameters, Body = body, Line = Previous().Line };
            }
        }

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

        // Helpers
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

        private bool Check(TokenType type) => !IsAtEnd() && Peek().Type == type;
        private Token Advance() { if (!IsAtEnd()) _current++; return Previous(); }
        private bool IsAtEnd() => Peek().Type == TokenType.Eof;
        private Token Peek() => _tokens[_current];
        private Token PeekNext() => _current + 1 < _tokens.Count ? _tokens[_current + 1] : _tokens[_current];
        private Token Previous() => _tokens[_current - 1];

        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) return Advance();
            throw Error(message);
        }

        private void ConsumeStatementEnd()
        {
            if (Match(TokenType.Semicolon)) return;
            if (Match(TokenType.Newline)) return;
            if (Check(TokenType.RightBrace)) return;
            if (IsAtEnd()) return;
            // Allow implicit statement end
        }

        private void SkipNewlines()
        {
            while (Match(TokenType.Newline)) { }
        }

        private Exception Error(string message)
        {
            var token = Peek();
            return new ParserException($"{message} at line {token.Line}, column {token.Column}");
        }
    }

    public class ParserException : Exception
    {
        public ParserException(string message) : base(message) { }
    }
}
