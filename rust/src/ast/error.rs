use thiserror::Error;

#[derive(Error, Debug, Clone, PartialEq, Eq)]
pub enum AstError {
    #[error("Empty input")]
    EmptyInput,

    #[error("Unexpected token: {0}")]
    UnexpectedToken(String),

    #[error("Invalid syntax: {0}")]
    InvalidSyntax(String),

    #[error("Failed to build AST: {0}")]
    BuildError(String),
}
