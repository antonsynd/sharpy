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
    True,
    False,
    None,

    // Identifiers and Keywords
    Identifier,
    Def,
    Class,
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
    Import,
    From,
    As,

    // Operators
    Plus,           // +
    Minus,          // -
    Star,           // *
    Slash,          // /
    DoubleSlash,    // //
    Percent,        // %
    DoubleStar,     // **

    // Comparison
    Equal,          // ==
    NotEqual,       // !=
    Less,           // <
    Greater,        // >
    LessEqual,      // <=
    GreaterEqual,   // >=

    // Assignment
    Assign,         // =

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

    // Special
    Newline,
    Indent,
    Dedent,
    Eof,

    // Access modifiers
    Public,
    Private,
    Protected,
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
