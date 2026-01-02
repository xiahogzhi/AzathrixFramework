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
        PercentEqual,   // %=
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
        QuestionQuestion, // ??
        QuestionDot,    // ?.
        QuestionBracket, // ?[

        // Bitwise operators
        BitAnd,         // &
        BitOr,          // |
        BitXor,         // ^
        BitNot,         // ~
        LeftShift,      // <<
        RightShift,     // >>

        // Delimiters
        LeftParen,      // (
        RightParen,     // )
        LeftBrace,      // {
        RightBrace,     // }
        LeftBracket,    // [
        RightBracket,   // ]
        Comma,          // ,
        Dot,            // .
        DotDotDot,      // ...
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
        Try,
        Catch,
        Finally,
        Throw,
        Enum,
        Static,
        Export,

        // Special
        Newline,
        Eof
    }
}