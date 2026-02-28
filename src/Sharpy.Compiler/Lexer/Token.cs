using Sharpy.Compiler.Text;

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
    FStringFormatSpec, // format specification after : in f-string expression (e.g., ".2f", ">10")
    FStringEnd,     // " or ' - end of f-string
    True,
    False,
    None,

    // Identifiers and Keywords (v0.1)
    Identifier,

    // Keywords - Control Flow
    Def,
    Class,
    Struct,
    Interface,
    Enum,
    Union,
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
    With,

    // Keywords - Import
    Import,
    From,
    As,

    // Keywords - Type/Value
    Auto,           // Type inference
    Const,
    Lambda,
    Type,           // Type alias declaration

    // Keywords - Pattern Matching
    Match,
    Case,

    // Keywords - Async
    Async,
    Await,
    Yield,

    // Keywords - Members
    Property,
    Event,

    // Keywords - Other
    Del,            // Delete statement
    To,             // Type coercion operator
    Maybe,          // Optional from nullable expressions
    Super,          // Super class access

    // Future Keywords (reserved)
    Defer,
    Do,

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
    Bang,           // ! (standalone, for T !E result type syntax)
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
    ColonAssign,    // := (walrus operator)
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
    NullCoalesceAssign, // ??=

    // Operators - Special
    Question,          // ?
    NullConditional,   // ?.
    NullCoalesce,      // ??
    Ellipsis,          // ...
    PipeForward,       // |>

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
/// Kinds of trivia that can be attached to tokens.
/// </summary>
public enum TriviaKind
{
    Comment
}

/// <summary>
/// Represents trivia (non-significant syntax) attached to a token.
/// </summary>
public record Trivia
{
    public TriviaKind Kind { get; init; }
    public string Text { get; init; } = "";
    public int Line { get; init; }
    public int Column { get; init; }
    public int Position { get; init; }
}

/// <summary>
/// Represents a token in the Sharpy source code
/// </summary>
public record Token : ILocatable
{
    public TokenType Type { get; init; }
    public string Value { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Column { get; init; }

    /// <summary>
    /// The zero-based character offset where this token starts in the source text.
    /// This is -1 if position tracking was not enabled during lexing.
    /// </summary>
    public int Position { get; init; } = -1;

    /// <summary>
    /// Trivia (comments) appearing before this token.
    /// Null when trivia preservation is not enabled.
    /// </summary>
    public IReadOnlyList<Trivia>? LeadingTrivia { get; init; }

    /// <summary>
    /// Trivia (end-of-line comments) appearing after this token on the same line.
    /// Null when trivia preservation is not enabled.
    /// </summary>
    public IReadOnlyList<Trivia>? TrailingTrivia { get; init; }

    /// <summary>
    /// The length of this token in characters.
    /// This equals Value.Length for most tokens.
    /// </summary>
    public int Length => Value.Length;

    public Token(TokenType type, string value, int line, int column)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
    }

    public Token(TokenType type, string value, int line, int column, int position)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
        Position = position;
    }

    /// <summary>
    /// Gets a TextSpan representing this token's location in the source.
    /// Returns null if position tracking was not enabled.
    /// </summary>
    public TextSpan? GetSpan()
    {
        if (Position < 0)
            return null;
        return new TextSpan(Position, Length);
    }

    /// <summary>
    /// The span of this token in the source text (ILocatable implementation).
    /// Returns null if position tracking was not enabled.
    /// </summary>
    TextSpan? ILocatable.Span => GetSpan();
}
