use std::collections::HashMap;

/// Represents different kinds of scopes in the Sharpy language
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ScopeKind {
    /// Global/module scope
    Module,
    /// Function scope (including methods)
    Function,
    /// Class scope
    Class,
    /// Struct scope
    Struct,
    /// Protocol scope
    Protocol,
    /// Block scope (for control flow structures)
    Block,
}

/// Represents a lexical scope with symbol bindings
#[derive(Debug, Clone)]
pub struct Scope {
    /// The kind of scope this represents
    pub kind: ScopeKind,

    /// Name of this scope (e.g., function name, class name)
    pub name: Option<String>,

    /// Symbols defined in this scope
    pub symbols: HashMap<String, String>, // name -> symbol_id

    /// Parent scope (None for module scope)
    pub parent: Option<String>, // parent scope id

    /// Child scopes
    pub children: Vec<String>, // child scope ids

    /// Unique identifier for this scope
    pub id: String,
}

impl Scope {
    /// Create a new scope
    #[must_use]
    pub fn new(kind: ScopeKind, name: Option<String>, parent: Option<String>) -> Self {
        let id = Self::generate_id(&kind, name.as_ref());

        Self {
            kind,
            name,
            symbols: HashMap::new(),
            parent,
            children: Vec::new(),
            id,
        }
    }

    /// Generate a unique identifier for the scope
    fn generate_id(kind: &ScopeKind, name: Option<&String>) -> String {
        use std::sync::atomic::{AtomicUsize, Ordering};
        static COUNTER: AtomicUsize = AtomicUsize::new(0);

        let counter = COUNTER.fetch_add(1, Ordering::SeqCst);

        match (kind, name) {
            (ScopeKind::Module, Some(name)) => format!("module:{name}:{counter}"),
            (ScopeKind::Module, None) => format!("module:<anonymous>:{counter}"),
            (ScopeKind::Function, Some(name)) => format!("function:{name}:{counter}"),
            (ScopeKind::Function, None) => format!("function:<lambda>:{counter}"),
            (ScopeKind::Class, Some(name)) => format!("class:{name}:{counter}"),
            (ScopeKind::Struct, Some(name)) => format!("struct:{name}:{counter}"),
            (ScopeKind::Protocol, Some(name)) => format!("protocol:{name}:{counter}"),
            (ScopeKind::Block, _) => format!("block:{counter}"),
            _ => format!("scope:{}:{counter}", kind.as_str()),
        }
    }

    /// Add a symbol to this scope
    pub fn add_symbol(&mut self, name: String, symbol_id: String) {
        self.symbols.insert(name, symbol_id);
    }

    /// Look up a symbol in this scope (not including parent scopes)
    #[must_use]
    pub fn get_symbol(&self, name: &str) -> Option<&String> {
        self.symbols.get(name)
    }

    /// Add a child scope
    pub fn add_child(&mut self, child_id: String) {
        self.children.push(child_id);
    }

    /// Check if this scope can contain a specific symbol kind
    #[must_use]
    #[allow(clippy::match_same_arms)] // Different scopes logically have different capabilities
    pub const fn can_contain(&self, symbol_kind: &crate::semantic::SymbolKind) -> bool {
        use crate::semantic::SymbolKind;

        match (&self.kind, symbol_kind) {
            // Module scope can contain most things
            (
                ScopeKind::Module,
                SymbolKind::Class
                | SymbolKind::Struct
                | SymbolKind::Protocol
                | SymbolKind::Function
                | SymbolKind::Variable
                | SymbolKind::Constant,
            ) => true,

            // Class scope can contain methods, properties, and member variables
            (
                ScopeKind::Class,
                SymbolKind::Method
                | SymbolKind::Property
                | SymbolKind::Variable
                | SymbolKind::Constant,
            ) => true,

            // Struct scope similar to class
            (
                ScopeKind::Struct,
                SymbolKind::Method
                | SymbolKind::Property
                | SymbolKind::Variable
                | SymbolKind::Constant,
            ) => true,

            // Protocol scope can contain abstract methods and properties
            (ScopeKind::Protocol, SymbolKind::Method | SymbolKind::Property) => true,

            // Function scope can contain local variables and parameters
            (ScopeKind::Function, SymbolKind::Variable | SymbolKind::Parameter) => true,

            // Block scope can contain local variables
            (ScopeKind::Block, SymbolKind::Variable) => true,

            _ => false,
        }
    }

    /// Get a display name for this scope
    #[must_use]
    pub fn display_name(&self) -> String {
        self.name.as_ref().map_or_else(
            || self.kind.as_str().to_string(),
            |name| format!("{} {}", self.kind.as_str(), name),
        )
    }
}

impl ScopeKind {
    /// Get the string representation of the scope kind
    #[must_use]
    pub const fn as_str(&self) -> &'static str {
        match self {
            Self::Module => "module",
            Self::Function => "function",
            Self::Class => "class",
            Self::Struct => "struct",
            Self::Protocol => "protocol",
            Self::Block => "block",
        }
    }
}
