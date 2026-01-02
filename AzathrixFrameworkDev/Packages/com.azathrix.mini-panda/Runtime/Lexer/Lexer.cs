using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace Azathrix.MiniPanda.Lexer
{
    /// <summary>
    /// 词法分析器
    /// </summary>
    /// <remarks>
    /// 将源代码字符串转换为 Token 序列，支持：
    /// - 关键字和标识符
    /// - 数字和字符串字面量（含插值）
    /// - 各种运算符和分隔符
    /// - 单行和多行注释
    /// </remarks>
    public class Lexer
    {
        private readonly string _source;              // 源代码
        private readonly List<Token> _tokens = new List<Token>();  // Token 列表
        private int _start;        // 当前 token 起始位置
        private int _current;      // 当前扫描位置
        private int _line = 1;     // 当前行号
        private int _column = 1;   // 当前列号
        private int _startColumn = 1;  // 当前 token 起始列号

        /// <summary>关键字映射表</summary>
        private static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
        {
            {"var", TokenType.Var},
            {"func", TokenType.Func},
            {"class", TokenType.Class},
            {"if", TokenType.If},
            {"else", TokenType.Else},
            {"while", TokenType.While},
            {"for", TokenType.For},
            {"in", TokenType.In},
            {"return", TokenType.Return},
            {"break", TokenType.Break},
            {"continue", TokenType.Continue},
            {"import", TokenType.Import},
            {"as", TokenType.As},
            {"global", TokenType.Global},
            {"this", TokenType.This},
            {"super", TokenType.Super},
            {"try", TokenType.Try},
            {"catch", TokenType.Catch},
            {"finally", TokenType.Finally},
            {"throw", TokenType.Throw},
            {"enum", TokenType.Enum},
            {"static", TokenType.Static},
            {"export", TokenType.Export},
            {"true", TokenType.True},
            {"false", TokenType.False},
            {"null", TokenType.Null}
        };

        /// <summary>创建词法分析器</summary>
        /// <param name="source">源代码字符串</param>
        public Lexer(string source)
        {
            _source = source;
        }

        /// <summary>
        /// 执行词法分析
        /// </summary>
        /// <returns>Token 列表</returns>
        public List<Token> Tokenize()
        {
            while (!IsAtEnd())
            {
                _start = _current;
                _startColumn = _column;
                ScanToken();
            }
            _tokens.Add(new Token(TokenType.Eof, "", null, _line, _column));
            return _tokens;
        }

        /// <summary>扫描单个 Token</summary>
        private void ScanToken()
        {
            char c = Advance();
            switch (c)
            {
                case '(': AddToken(TokenType.LeftParen); break;
                case ')': AddToken(TokenType.RightParen); break;
                case '{': AddToken(TokenType.LeftBrace); break;
                case '}': AddToken(TokenType.RightBrace); break;
                case '[': AddToken(TokenType.LeftBracket); break;
                case ']': AddToken(TokenType.RightBracket); break;
                case ',': AddToken(TokenType.Comma); break;
                case '.':
                    if (Match('.') && Match('.')) AddToken(TokenType.DotDotDot);
                    else AddToken(TokenType.Dot);
                    break;
                case ':': AddToken(TokenType.Colon); break;
                case ';': AddToken(TokenType.Semicolon); break;
                case '?':
                    if (Match('?')) AddToken(TokenType.QuestionQuestion);
                    else if (Match('.')) AddToken(TokenType.QuestionDot);
                    else if (Match('[')) AddToken(TokenType.QuestionBracket);
                    else AddToken(TokenType.Question);
                    break;
                case '+':
                    if (Match('+')) AddToken(TokenType.PlusPlus);
                    else if (Match('=')) AddToken(TokenType.PlusEqual);
                    else AddToken(TokenType.Plus);
                    break;
                case '-':
                    if (Match('-')) AddToken(TokenType.MinusMinus);
                    else if (Match('=')) AddToken(TokenType.MinusEqual);
                    else AddToken(TokenType.Minus);
                    break;
                case '*':
                    AddToken(Match('=') ? TokenType.StarEqual : TokenType.Star);
                    break;
                case '%':
                    AddToken(Match('=') ? TokenType.PercentEqual : TokenType.Percent);
                    break;

                case '!': AddToken(Match('=') ? TokenType.BangEqual : TokenType.Bang); break;
                case '=':
                    if (Match('=')) AddToken(TokenType.EqualEqual);
                    else if (Match('>')) AddToken(TokenType.Arrow);
                    else AddToken(TokenType.Equal);
                    break;
                case '<':
                    if (Match('<')) AddToken(TokenType.LeftShift);
                    else if (Match('=')) AddToken(TokenType.LessEqual);
                    else AddToken(TokenType.Less);
                    break;
                case '>':
                    if (Match('>')) AddToken(TokenType.RightShift);
                    else if (Match('=')) AddToken(TokenType.GreaterEqual);
                    else AddToken(TokenType.Greater);
                    break;
                case '&':
                    if (Match('&')) AddToken(TokenType.And);
                    else AddToken(TokenType.BitAnd);
                    break;
                case '|':
                    if (Match('|')) AddToken(TokenType.Or);
                    else AddToken(TokenType.BitOr);
                    break;
                case '^': AddToken(TokenType.BitXor); break;
                case '~': AddToken(TokenType.BitNot); break;

                case '/':
                    if (Match('/'))
                    {
                        while (Peek() != '\n' && !IsAtEnd()) Advance();
                    }
                    else if (Match('*'))
                    {
                        BlockComment();
                    }
                    else if (Match('='))
                    {
                        AddToken(TokenType.SlashEqual);
                    }
                    else
                    {
                        AddToken(TokenType.Slash);
                    }
                    break;

                case ' ':
                case '\r':
                case '\t':
                    break;

                case '\n':
                    AddToken(TokenType.Newline);
                    _line++;
                    _column = 1;
                    break;

                case '"': String(); break;

                default:
                    if (IsDigit(c)) Number();       // 数字
                    else if (IsAlpha(c)) Identifier();  // 标识符或关键字
                    break;
            }
        }

        /// <summary>解析字符串字面量（支持插值和转义）</summary>
        private void String()
        {
            var sb = new StringBuilder();
            var hasInterpolation = false;
            var parts = new List<object>();

            while (Peek() != '"' && !IsAtEnd())
            {
                if (Peek() == '\n')
                {
                    _line++;
                    _column = 0;
                }
                if (Peek() == '{' && PeekNext() != '{')
                {
                    hasInterpolation = true;
                    if (sb.Length > 0)
                    {
                        parts.Add(sb.ToString());
                        sb.Clear();
                    }
                    Advance(); // consume '{'
                    var exprStart = _current;
                    int braceCount = 1;
                    while (braceCount > 0 && !IsAtEnd())
                    {
                        if (Peek() == '{') braceCount++;
                        else if (Peek() == '}') braceCount--;
                        if (braceCount > 0) Advance();
                    }
                    var expr = _source.Substring(exprStart, _current - exprStart);
                    parts.Add(new StringInterpolation(expr));
                    Advance(); // consume '}'
                }
                else if (Peek() == '\\')
                {
                    Advance();
                    if (!IsAtEnd())
                    {
                        char escaped = Advance();
                        switch (escaped)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'r': sb.Append('\r'); break;
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '{': sb.Append('{'); break;
                            default: sb.Append(escaped); break;
                        }
                    }
                }
                else
                {
                    sb.Append(Advance());
                }
            }

            if (IsAtEnd())
            {
                throw new LexerException($"Unterminated string at line {_line}");
            }

            Advance(); // closing "

            if (hasInterpolation)
            {
                if (sb.Length > 0) parts.Add(sb.ToString());
                AddToken(TokenType.String, parts);
            }
            else
            {
                AddToken(TokenType.String, sb.ToString());
            }
        }

        /// <summary>解析数字字面量（整数和浮点数）</summary>
        private void Number()
        {
            while (IsDigit(Peek())) Advance();

            if (Peek() == '.' && IsDigit(PeekNext()))
            {
                Advance();
                while (IsDigit(Peek())) Advance();
            }

            var value = double.Parse(_source.Substring(_start, _current - _start), CultureInfo.InvariantCulture);
            AddToken(TokenType.Number, value);
        }

        /// <summary>解析标识符或关键字</summary>
        private void Identifier()
        {
            while (IsAlphaNumeric(Peek())) Advance();

            var text = _source.Substring(_start, _current - _start);
            var type = Keywords.TryGetValue(text, out var keyword) ? keyword : TokenType.Identifier;
            AddToken(type);
        }

        /// <summary>跳过块注释（支持嵌套）</summary>
        private void BlockComment()
        {
            int depth = 1;
            while (depth > 0 && !IsAtEnd())
            {
                if (Peek() == '/' && PeekNext() == '*')
                {
                    Advance(); Advance();
                    depth++;
                }
                else if (Peek() == '*' && PeekNext() == '/')
                {
                    Advance(); Advance();
                    depth--;
                }
                else
                {
                    if (Peek() == '\n')
                    {
                        _line++;
                        _column = 0;
                    }
                    Advance();
                }
            }
        }

        #region 辅助方法

        /// <summary>尝试匹配下一个字符</summary>
        private bool Match(char expected)
        {
            if (IsAtEnd() || _source[_current] != expected) return false;
            _current++;
            _column++;
            return true;
        }

        /// <summary>查看当前字符（不前进）</summary>
        private char Peek() => IsAtEnd() ? '\0' : _source[_current];
        /// <summary>查看下一个字符（不前进）</summary>
        private char PeekNext() => _current + 1 >= _source.Length ? '\0' : _source[_current + 1];

        /// <summary>前进并返回当前字符</summary>
        private char Advance()
        {
            _column++;
            return _source[_current++];
        }

        /// <summary>是否到达源代码末尾</summary>
        private bool IsAtEnd() => _current >= _source.Length;
        /// <summary>是否为数字字符</summary>
        private bool IsDigit(char c) => c >= '0' && c <= '9';
        /// <summary>是否为字母或下划线</summary>
        private bool IsAlpha(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
        /// <summary>是否为字母、数字或下划线</summary>
        private bool IsAlphaNumeric(char c) => IsAlpha(c) || IsDigit(c);

        /// <summary>添加 Token 到列表</summary>
        private void AddToken(TokenType type, object literal = null)
        {
            var text = _source.Substring(_start, _current - _start);
            _tokens.Add(new Token(type, text, literal, _line, _startColumn));
        }

        #endregion
    }

    /// <summary>
    /// 字符串插值表达式
    /// </summary>
    public class StringInterpolation
    {
        /// <summary>插值表达式源代码</summary>
        public string Expression { get; }
        public StringInterpolation(string expression) => Expression = expression;
    }

    /// <summary>
    /// 词法分析异常
    /// </summary>
    public class LexerException : System.Exception
    {
        public LexerException(string message) : base(message) { }
    }
}
