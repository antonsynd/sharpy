use crate::utils::SourceLocation;
use std::fmt;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ParseError {
    UnexpectedToken {
        expected: String,
        found: String,
        location: SourceLocation,
    },
    UnexpectedEof {
        expected: String,
        location: SourceLocation,
    },
    InvalidSyntax {
        message: String,
        location: SourceLocation,
    },
}

impl fmt::Display for ParseError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::UnexpectedToken {
                expected,
                found,
                location,
            } => {
                write!(
                    f,
                    "Parse error at {}:{}: expected {}, found {}",
                    location.line, location.column, expected, found
                )
            }
            Self::UnexpectedEof { expected, location } => {
                write!(
                    f,
                    "Parse error at {}:{}: unexpected end of file, expected {}",
                    location.line, location.column, expected
                )
            }
            Self::InvalidSyntax { message, location } => {
                write!(
                    f,
                    "Parse error at {}:{}: {}",
                    location.line, location.column, message
                )
            }
        }
    }
}

impl std::error::Error for ParseError {}
