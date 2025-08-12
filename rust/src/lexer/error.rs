use thiserror::Error;

#[derive(Error, Debug, Clone, PartialEq, Eq)]
pub enum LexerError {
    #[error("Unexpected character: '{0}'")]
    UnexpectedCharacter(char),

    #[error("Unterminated string literal")]
    UnterminatedString,

    #[error("Unterminated f-string")]
    UnterminatedFString,

    #[error("Invalid escape sequence: '{0}'")]
    InvalidEscapeSequence(String),

    #[error("Invalid number format: '{0}'")]
    InvalidNumber(String),

    #[error("Invalid indentation: {0}")]
    InvalidIndentation(String),

    #[error("Inconsistent use of tabs and spaces in indentation")]
    InconsistentIndentation,

    #[error("Inconsistent dedent")]
    InconsistentDedent,

    #[error("Unexpected brace in f-string")]
    UnexpectedBrace,

    #[error("Invalid Unicode identifier")]
    InvalidUnicodeIdentifier,

    #[error("Unexpected end of file")]
    UnexpectedEof,

    #[error("Invalid character in number: '{0}'")]
    InvalidNumberCharacter(char),

    #[error("First statement indented")]
    FirstStatementIndented,
}
