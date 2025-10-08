/// Code generation module for Sharpy → C# transpilation
///
/// This module transforms a typed AST into C# source code.
///
/// # Architecture
///
/// The code generator operates in several phases:
/// 1. **Module Setup**: Generate namespace, using statements, __Module__ class
/// 2. **Type Generation**: Classes, structs, protocols → interfaces
/// 3. **Member Generation**: Methods, properties, fields with name mangling
/// 4. **Statement Generation**: Control flow, exception handling
/// 5. **Expression Generation**: Operations, calls, literals
///
/// # Name Mangling
///
/// - `snake_case` → `PascalCase` for types, methods, properties
/// - `snake_case` → `camelCase` for parameters, local variables
/// - `__dunder__` → `__Dunder__` for magic methods
/// - Backtick names preserved exactly
///
/// # Design Principles
///
/// - Generate idiomatic C# (not literal translations)
/// - Preserve semantic equivalence to Sharpy
/// - Emit debuggable code with source mappings
/// - No warnings from C# compiler
pub mod context;
pub mod expression;
pub mod generator;
pub mod name_mangling;
pub mod statement;
pub mod type_gen;

pub use generator::CodeGenerator;
pub use name_mangling::{NameContext, NameMangler};

/// Result type for code generation operations
pub type CodeGenResult<T> = Result<T, CodeGenError>;

/// Errors that can occur during code generation
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CodeGenError {
    /// Symbol not found in symbol table
    UndefinedSymbol { name: String, location: String },

    /// Type cannot be translated to C#
    UnsupportedType { sharpy_type: String, reason: String },

    /// Name mangling collision detected
    NameCollision {
        sharpy_name1: String,
        sharpy_name2: String,
        csharp_name: String,
    },

    /// Invalid AST node for context
    InvalidNode { expected: String, found: String },

    /// Feature not yet implemented
    NotImplemented { feature: String },

    /// Internal error in code generator
    InternalError { message: String },
}

impl std::fmt::Display for CodeGenError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::UndefinedSymbol { name, location } => {
                write!(f, "Undefined symbol '{name}' at {location}")
            }
            Self::UnsupportedType {
                sharpy_type,
                reason,
            } => {
                write!(f, "Unsupported type '{sharpy_type}': {reason}")
            }
            Self::NameCollision {
                sharpy_name1,
                sharpy_name2,
                csharp_name,
            } => {
                write!(
                    f,
                    "Name collision: '{sharpy_name1}' and '{sharpy_name2}' both mangle to '{csharp_name}'"
                )
            }
            Self::InvalidNode { expected, found } => {
                write!(f, "Invalid AST node: expected {expected}, found {found}")
            }
            Self::NotImplemented { feature } => {
                write!(f, "Feature not implemented: {feature}")
            }
            Self::InternalError { message } => {
                write!(f, "Internal code generation error: {message}")
            }
        }
    }
}

impl std::error::Error for CodeGenError {}
