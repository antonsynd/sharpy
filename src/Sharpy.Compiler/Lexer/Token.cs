namespace Sharpy.Compiler.Lexer;

/// <summary>
/// Represents the type of a token in Sharpy source code
/// </summary>
public enum TokenType
{
    // Literals
    Integer,
    Float,
    String,
    RawString,      // r-string (raw string)
    FStringStart,   // f" or f' - start of f-string
    FStringText,    // literal text segment in f-string
    FStringExprStart, // { - start of interpolated expression in f-string
    FStringExprEnd,   // } - end of interpolated expression in f-string
    FStringEnd,     // " or ' - end of f-string
    True,
    False,
    None,

    // Identifiers and Keywords (v0.5)
    Identifier,

    // Keywords - Control Flow
    Def,
    Class,
    Struct,
    Interface,
    Enum,
    If,
    Else,
    Elif,
    While,
    For,
    In,
    Return,
    Break,
    Continue,
    Pass,
    Try,
    Except,
    Finally,
    Raise,
    Assert,

    // Keywords - Import
    Import,
    From,
    As,

    // Keywords - Type/Value
    Auto,           // Type inference
    Const,
    Lambda,

    // Boolean operators (keywords)
    And,
    Or,
    Not,
    Is,

    // Operators - Arithmetic
    Plus,           // +
    Minus,          // -
    Star,           // *
    Slash,          // /
    DoubleSlash,    // //
    Percent,        // %
    DoubleStar,     // **

    // Operators - Comparison
    Equal,          // ==
    NotEqual,       // !=
    Less,           // <
    Greater,        // >
    LessEqual,      // <=
    GreaterEqual,   // >=

    // Operators - Bitwise
    Ampersand,      // &
    Pipe,           // |
    Caret,          // ^
    Tilde,          // ~
    LeftShift,      // <<
    RightShift,     // >>

    // Operators - Assignment
    Assign,         // =
    PlusAssign,     // +=
    MinusAssign,    // -=
    StarAssign,     // *=
    SlashAssign,    // /=
    DoubleSlashAssign, // //=
    PercentAssign,  // %=
    DoubleStarAssign,  // **=
    AmpersandAssign,   // &=
    PipeAssign,        // |=
    CaretAssign,       // ^=
    LeftShiftAssign,   // <<=
    RightShiftAssign,  // >>=

    // Operators - Special
    Question,          // ?
    NullConditional,   // ?.
    NullCoalesce,      // ??
    Ellipsis,          // ...

    // Delimiters
    LeftParen,      // (
    RightParen,     // )
    LeftBracket,    // [
    RightBracket,   // ]
    LeftBrace,      // {
    RightBrace,     // }
    Comma,          // ,
    Colon,          // :
    Semicolon,      // ;
    Dot,            // .
    Arrow,          // ->
    At,             // @ (decorators)
    Backslash,      // \ (line continuation)

    // Special
    Newline,
    Indent,
    Dedent,
    Eof,
    Backtick,       // ` (for literal names)

    // Comment (usually skipped but useful for documentation tools)
    Comment,
}

/// <summary>
/// Represents a token in the Sharpy source code
/// </summary>
public record Token
{
    public TokenType Type { get; init; }
    public string Value { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Column { get; init; }

    public Token(TokenType type, string value, int line, int column)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
    }
}
