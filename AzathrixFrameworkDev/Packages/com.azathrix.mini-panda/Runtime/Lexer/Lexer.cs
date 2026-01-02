using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace Azathrix.MiniPanda.Lexer
{
    public class Lexer
    {
        private readonly string _source;
        private readonly List<Token> _tokens = new List<Token>();
        private int _start;
        private int _current;
        private int _line = 1;
        private int _column = 1;
        private int _startColumn = 1;

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
            {"true", TokenType.True},
            {"false", TokenType.False},
            {"null", TokenType.Null}
        };

        public Lexer(string source)
        {
            _source = source;
        }

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
                case '.': AddToken(TokenType.Dot); break;
                case ':': AddToken(TokenType.Colon); break;
                case ';': AddToken(TokenType.Semicolon); break;
                case '?': AddToken(TokenType.Question); break;
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
                case '<': AddToken(Match('=') ? TokenType.LessEqual : TokenType.Less); break;
                case '>': AddToken(Match('=') ? TokenType.GreaterEqual : TokenType.Greater); break;
                case '&':
                    if (Match('&')) AddToken(TokenType.And);
                    else throw new LexerException($"Unexpected character '&' at line {_line}, column {_column}. Did you mean '&&'?");
                    break;
                case '|':
                    if (Match('|')) AddToken(TokenType.Or);
                    else throw new LexerException($"Unexpected character '|' at line {_line}, column {_column}. Did you mean '||'?");
                    break;

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
                    if (IsDigit(c)) Number();
                    else if (IsAlpha(c)) Identifier();
                    break;
            }
        }

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

        private void Identifier()
        {
            while (IsAlphaNumeric(Peek())) Advance();

            var text = _source.Substring(_start, _current - _start);
            var type = Keywords.TryGetValue(text, out var keyword) ? keyword : TokenType.Identifier;
            AddToken(type);
        }

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

        private bool Match(char expected)
        {
            if (IsAtEnd() || _source[_current] != expected) return false;
            _current++;
            _column++;
            return true;
        }

        private char Peek() => IsAtEnd() ? '\0' : _source[_current];
        private char PeekNext() => _current + 1 >= _source.Length ? '\0' : _source[_current + 1];

        private char Advance()
        {
            _column++;
            return _source[_current++];
        }

        private bool IsAtEnd() => _current >= _source.Length;
        private bool IsDigit(char c) => c >= '0' && c <= '9';
        private bool IsAlpha(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
        private bool IsAlphaNumeric(char c) => IsAlpha(c) || IsDigit(c);

        private void AddToken(TokenType type, object literal = null)
        {
            var text = _source.Substring(_start, _current - _start);
            _tokens.Add(new Token(type, text, literal, _line, _startColumn));
        }
    }

    public class StringInterpolation
    {
        public string Expression { get; }
        public StringInterpolation(string expression) => Expression = expression;
    }

    public class LexerException : System.Exception
    {
        public LexerException(string message) : base(message) { }
    }
}
