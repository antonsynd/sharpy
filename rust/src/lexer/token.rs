use crate::utils::SourceLocation;

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum TokenType {
    // Literals
    Number(NumberType),
    String(StringType),
    FString(FStringPart),

    // Keywords - Basic
    False, True, None, And, Or, Not,

    // Keywords - Class/Structure
    Class, Struct, Protocol, Property,

    // Keywords - Function/Control
    Def, Return, If, Else, Elif,
    For, While, Break, Continue, Pass,

    // Keywords - Exception Handling
    Try, Except, Finally, Raise,

    // Keywords - Import/Module
    Import, From, As,

    // Keywords - Async
    Async, Await,

    // Keywords - Other
    Assert, Del, Global, Nonlocal, With, Yield,
    In, Is, Lambda,

    // Sharpy-specific Keywords
    Event,

    // Soft Keywords (context-dependent)
    Type, Match, Case, Get, Set, Wildcard,

    // Operators - Arithmetic
    Plus,           // +
    Minus,          // -
    Star,           // *
    Slash,          // /
    DoubleStar,     // **
    Percent,        // %
    DoubleSlash,    // //

    // Operators - Comparison
    Equal,          // =
    EqEqual,        // ==
    NotEqual,       // !=
    Less,           // <
    Greater,        // >
    LessEqual,      // <=
    GreaterEqual,   // >=

    // Operators - Bitwise
    Amper,          // &
    Vbar,           // |
    Circumflex,     // ^
    Tilde,          // ~
    LeftShift,      // <<
    RightShift,     // >>

    // Operators - Assignment
    PlusEqual,      // +=
    MinEqual,       // -=
    StarEqual,      // *=
    SlashEqual,     // /=
    PercentEqual,   // %=
    AmperEqual,     // &=
    VbarEqual,      // |=
    CircumflexEqual, // ^=
    LeftShiftEqual, // <<=
    RightShiftEqual, // >>=
    DoubleStarEqual, // **=
    DoubleSlashEqual, // //=
    AtEqual,        // @=
    ColonEqual,     // :=

    // Sharpy-specific Operators
    QuestionDot,    // ?.
    DoubleQuestion, // ??
    Question,       // ?
    Exclamation,    // !

    // Delimiters
    LeftParen,      // (
    RightParen,     // )
    LeftBracket,    // [
    RightBracket,   // ]
    LeftBrace,      // {
    RightBrace,     // }

    // Punctuation
    Dot,            // .
    Comma,          // ,
    Colon,          // :
    Semi,           // ;
    At,             // @
    RArrow,         // ->
    Ellipsis,       // ...

    // Special tokens
    Newline,
    Indent,
    Dedent,

    // Identifiers
    Name(String),

    // Comments
    Comment(String),

    // End of file
    Eof,

    // Error token
    Error(String),
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum NumberType {
    Integer(String),    // Store as string to preserve format
    Float(String),      // Store as string to preserve format
    Imaginary(String),  // Store as string to preserve format
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum StringType {
    Regular(String),
    Raw(String),
    Bytes(Vec<u8>),
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum FStringPart {
    Start(String),   // f" or f'
    Middle(String),  // text between expressions
    End(String),     // closing quote
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Token {
    pub token_type: TokenType,
    pub lexeme: String,
    pub location: SourceLocation,
    pub channel: Channel,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Channel {
    Default,
    Hidden,  // For whitespace, comments
}

impl Token {
    /// Creates a new token with the given type, lexeme, and location.
    #[must_use]
    pub const fn new(token_type: TokenType, lexeme: String, location: SourceLocation) -> Self {
        Self {
            token_type,
            lexeme,
            location,
            channel: Channel::Default,
        }
    }

    /// Sets the channel for this token.
    #[must_use]
    pub const fn with_channel(mut self, channel: Channel) -> Self {
        self.channel = channel;
        self
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum AccessModifier {
    Protected,  // _
    Private,    // __
    Internal,   // `
    File,       // ``
}
