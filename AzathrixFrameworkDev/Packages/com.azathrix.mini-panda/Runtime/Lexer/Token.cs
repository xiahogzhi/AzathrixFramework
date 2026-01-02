namespace Azathrix.MiniPanda.Lexer
{
    public readonly struct Token
    {
        public readonly TokenType Type;
        public readonly string Lexeme;
        public readonly object Literal;
        public readonly int Line;
        public readonly int Column;

        public Token(TokenType type, string lexeme, object literal, int line, int column)
        {
            Type = type;
            Lexeme = lexeme;
            Literal = literal;
            Line = line;
            Column = column;
        }

        public override string ToString() => $"{Type} '{Lexeme}' at {Line}:{Column}";
    }
}
