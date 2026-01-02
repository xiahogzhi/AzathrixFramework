namespace Azathrix.MiniPanda.Lexer
{
    public enum TokenType
    {
        // Literals
        Number,
        String,
        True,
        False,
        Null,
        Identifier,

        // Operators
        Plus,           // +
        Minus,          // -
        Star,           // *
        Slash,          // /
        Percent,        // %
        Equal,          // =
        PlusEqual,      // +=
        MinusEqual,     // -=
        StarEqual,      // *=
        SlashEqual,     // /=
        PlusPlus,       // ++
        MinusMinus,     // --
        EqualEqual,     // ==
        BangEqual,      // !=
        Less,           // <
        LessEqual,      // <=
        Greater,        // >
        GreaterEqual,   // >=
        Bang,           // !
        And,            // &&
        Or,             // ||
        Arrow,          // =>
        Question,       // ?

        // Delimiters
        LeftParen,      // (
        RightParen,     // )
        LeftBrace,      // {
        RightBrace,     // }
        LeftBracket,    // [
        RightBracket,   // ]
        Comma,          // ,
        Dot,            // .
        Colon,          // :
        Semicolon,      // ;

        // Keywords
        Var,
        Func,
        Class,
        If,
        Else,
        While,
        For,
        In,
        Return,
        Break,
        Continue,
        Import,
        As,
        Global,
        This,
        Super,

        // Special
        Newline,
        Eof
    }
}