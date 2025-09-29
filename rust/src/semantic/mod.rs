pub mod module_registry;
pub mod multi_pass_analyzer;
pub mod passes;
pub mod scope;
pub mod symbol_table;
pub mod types;

// Main semantic analyzer - now using multi-pass by default
pub use module_registry::{ModuleRegistry, ModuleSymbolTable};
pub use multi_pass_analyzer::{AnalysisResult, MultiPassAnalyzer as SemanticAnalyzer};
pub use scope::{Scope, ScopeKind};
pub use symbol_table::{AccessLevel, Symbol, SymbolKind, SymbolMetadata, SymbolTable};
pub use types::{BuiltinType, SemanticType};

/// Semantic analysis errors
#[derive(Debug, Clone, PartialEq)]
pub enum SemanticError {
    /// Symbol not found
    UndefinedSymbol(String),

    /// Duplicate symbol definition
    DuplicateSymbol(String),

    /// Type mismatch
    TypeMismatch {
        expected: crate::ast::Type,
        found: crate::ast::Type,
        line: usize,
        column: usize,
    },

    /// Invalid operation for type
    InvalidOperation {
        operation: String,
        type_name: String,
    },

    /// Type is not indexable
    NotIndexable(crate::ast::Type),

    /// Module not found
    ModuleNotFound(String),

    /// Duplicate module registration
    DuplicateModule(String),

    /// Unknown module
    UnknownModule(String),

    /// No current module set
    NoCurrentModule,

    /// Function argument count mismatch
    ArgumentCountMismatch {
        function_name: String,
        expected: usize,
        found: usize,
    },

    /// Function argument type mismatch
    ArgumentTypeMismatch {
        function_name: String,
        argument_index: usize,
        expected: String,
        found: String,
    },

    /// Variable called as function
    VariableCalledAsFunction {
        variable_name: String,
        variable_type: String,
    },

    /// Module cache error
    ModuleCacheError(String),

    /// Module load error
    ModuleLoadError(String),

    /// Access level violation
    AccessViolation {
        symbol: String,
        access_level: AccessLevel,
        context: String,
    },

    /// Attribute not found on type
    AttributeNotFound {
        object_type: String,
        attribute: String,
    },
}

impl std::fmt::Display for SemanticError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::UndefinedSymbol(name) => {
                write!(f, "Undefined symbol: {name}")
            }
            Self::DuplicateSymbol(name) => {
                write!(f, "Duplicate symbol: {name}")
            }
            Self::TypeMismatch {
                expected,
                found,
                line,
                column,
            } => {
                write!(
                    f,
                    "Type mismatch at {line}:{column}: expected {expected:?}, found {found:?}"
                )
            }
            Self::InvalidOperation {
                operation,
                type_name,
            } => {
                write!(f, "Invalid operation '{operation}' for type '{type_name}'")
            }
            Self::NotIndexable(type_name) => {
                write!(f, "Type {type_name:?} is not indexable")
            }
            Self::ModuleNotFound(name) => {
                write!(f, "Module not found: {name}")
            }
            Self::DuplicateModule(name) => {
                write!(f, "Duplicate module: {name}")
            }
            Self::UnknownModule(name) => {
                write!(f, "Unknown module: {name}")
            }
            Self::NoCurrentModule => {
                write!(f, "No current module set for analysis")
            }
            Self::ModuleCacheError(name) => {
                write!(f, "Module cache error for: {name}")
            }
            Self::ModuleLoadError(name) => {
                write!(f, "Module load error for: {name}")
            }
            Self::AccessViolation {
                symbol,
                access_level,
                context,
            } => {
                write!(
                    f,
                    "Access violation: cannot access {access_level:?} symbol '{symbol}' from {context}"
                )
            }
            Self::AttributeNotFound {
                object_type,
                attribute,
            } => {
                write!(f, "Type '{object_type}' has no attribute '{attribute}'")
            }
            Self::ArgumentCountMismatch {
                function_name,
                expected,
                found,
            } => {
                write!(
                    f,
                    "{}() takes exactly {} argument{}, got {}",
                    function_name,
                    expected,
                    if *expected == 1 { "" } else { "s" },
                    found
                )
            }
            Self::ArgumentTypeMismatch {
                function_name,
                argument_index,
                expected,
                found,
            } => {
                write!(
                    f,
                    "{}(): argument {} expected '{}', got '{}'",
                    function_name,
                    argument_index + 1,
                    expected,
                    found
                )
            }
            Self::VariableCalledAsFunction {
                variable_name,
                variable_type,
            } => {
                write!(
                    f,
                    "'{variable_name}' is not callable (it is of type '{variable_type}')"
                )
            }
        }
    }
}

impl std::error::Error for SemanticError {}

/// Convert from String error to `SemanticError`
impl From<String> for SemanticError {
    fn from(error: String) -> Self {
        Self::DuplicateSymbol(error)
    }
}

/// Convenient type alias for symbol type
pub type SymbolType = SymbolKind;
