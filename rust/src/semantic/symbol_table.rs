use crate::semantic::{Scope, ScopeKind, SemanticType};
use crate::utils::SourceLocation;
use std::collections::HashMap;

/// Represents different kinds of symbols in the Sharpy language
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SymbolKind {
    /// Function or module-level function
    Function,
    /// Method within a class/struct/protocol
    Method,
    /// Class definition
    Class,
    /// Struct definition
    Struct,
    /// Protocol definition
    Protocol,
    /// Variable (local or member)
    Variable,
    /// Function parameter
    Parameter,
    /// Property (with getter/setter)
    Property,
    /// Constant value
    Constant,
    /// Import alias
    Import,
    /// Type alias
    TypeAlias,
}

/// Represents access levels for symbols
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum AccessLevel {
    Public,
    Protected,
    Private,
    Internal,
    File,
}

impl AccessLevel {
    /// Parse access level from access modifier string
    #[must_use]
    pub fn from_modifier(modifier: Option<&str>) -> Self {
        match modifier {
            Some("protected") => Self::Protected,
            Some("private") => Self::Private,
            Some("internal") => Self::Internal,
            Some("file") => Self::File,
            _ => Self::Public, // Default is public
        }
    }

    /// Get the string representation
    #[must_use]
    pub const fn as_str(&self) -> &'static str {
        match self {
            Self::Public => "public",
            Self::Protected => "protected",
            Self::Private => "private",
            Self::Internal => "internal",
            Self::File => "file",
        }
    }
}

/// Represents a symbol in the symbol table
#[derive(Debug, Clone)]
pub struct Symbol {
    /// Unique identifier for this symbol
    pub id: String,

    /// Name of the symbol (without access modifiers)
    pub name: String,

    /// Kind of symbol
    pub kind: SymbolKind,

    /// Type of the symbol
    pub symbol_type: SemanticType,

    /// Access level
    pub access_level: AccessLevel,

    /// Scope where this symbol is defined
    pub scope_id: String,

    /// Source location where symbol is defined
    pub location: Option<SourceLocation>,

    /// Whether this symbol is static
    pub is_static: bool,

    /// Generic type parameters (for classes, functions, etc.)
    pub generic_params: Vec<String>,

    /// Additional metadata specific to the symbol kind
    pub metadata: SymbolMetadata,
}

/// Additional metadata for different symbol kinds
#[derive(Debug, Clone)]
pub enum SymbolMetadata {
    /// Function metadata
    Function {
        parameters: Vec<String>, // parameter symbol ids
        return_type: Option<SemanticType>,
        is_abstract: bool,
    },
    /// Method metadata
    Method {
        parameters: Vec<String>, // parameter symbol ids
        return_type: Option<SemanticType>,
        is_abstract: bool,
        is_override: bool,
        is_virtual: bool,
    },
    /// Class metadata
    Class {
        base_class: Option<String>, // base class symbol id
        protocols: Vec<String>,     // protocol symbol ids
        members: Vec<String>,       // member symbol ids
        methods: Vec<String>,       // method symbol ids
        properties: Vec<String>,    // property symbol ids
    },
    /// Struct metadata
    Struct {
        protocols: Vec<String>,  // protocol symbol ids
        members: Vec<String>,    // member symbol ids
        methods: Vec<String>,    // method symbol ids
        properties: Vec<String>, // property symbol ids
    },
    /// Protocol metadata
    Protocol {
        base_protocols: Vec<String>, // base protocol symbol ids
        methods: Vec<String>,        // abstract method symbol ids
        properties: Vec<String>,     // abstract property symbol ids
    },
    /// Property metadata
    Property {
        has_getter: bool,
        has_setter: bool,
        is_auto: bool,
        backing_field: Option<String>, // backing field symbol id for auto properties
    },
    /// Variable metadata
    Variable {
        is_mutable: bool,
        is_member: bool,
        default_value: Option<String>, // AST representation of default value
    },
    /// Parameter metadata
    Parameter {
        default_value: Option<String>, // AST representation of default value
        is_variadic: bool,
    },
    /// Import metadata
    Import {
        module_path: String,
        imported_names: Vec<String>, // What names are imported
        alias: Option<String>,
    },
    /// No additional metadata
    None,
}

impl Symbol {
    /// Create a new symbol
    #[must_use]
    pub fn new(
        name: String,
        kind: SymbolKind,
        symbol_type: SemanticType,
        access_level: AccessLevel,
        scope_id: String,
        location: Option<SourceLocation>,
    ) -> Self {
        let id = Self::generate_id(&name, &kind, &scope_id);

        Self {
            id,
            name,
            kind,
            symbol_type,
            access_level,
            scope_id,
            location,
            is_static: false,
            generic_params: Vec::new(),
            metadata: SymbolMetadata::None,
        }
    }

    /// Generate a unique symbol ID
    fn generate_id(name: &str, kind: &SymbolKind, scope_id: &str) -> String {
        use std::sync::atomic::{AtomicUsize, Ordering};
        static COUNTER: AtomicUsize = AtomicUsize::new(0);

        let counter = COUNTER.fetch_add(1, Ordering::SeqCst);
        format!("{}:{}:{}:{}", kind.as_str(), scope_id, name, counter)
    }

    /// Set metadata for the symbol
    #[must_use]
    pub fn with_metadata(mut self, metadata: SymbolMetadata) -> Self {
        self.metadata = metadata;
        self
    }

    /// Set generic parameters
    #[must_use]
    pub fn with_generic_params(mut self, params: Vec<String>) -> Self {
        self.generic_params = params;
        self
    }

    /// Set static flag
    #[must_use]
    pub const fn with_static(mut self, is_static: bool) -> Self {
        self.is_static = is_static;
        self
    }

    /// Get the fully qualified name of this symbol
    #[must_use]
    pub fn qualified_name(&self, symbol_table: &SymbolTable) -> String {
        let mut parts = vec![self.name.clone()];
        let mut current_scope = symbol_table.scopes.get(&self.scope_id);

        while let Some(scope) = current_scope {
            if let Some(name) = &scope.name {
                parts.insert(0, name.clone());
            }

            current_scope = scope
                .parent
                .as_ref()
                .and_then(|parent_id| symbol_table.scopes.get(parent_id));
        }

        parts.join(".")
    }

    /// Check if this symbol is accessible from the given scope
    #[must_use]
    pub fn is_accessible_from(&self, from_scope_id: &str, symbol_table: &SymbolTable) -> bool {
        match self.access_level {
            AccessLevel::Protected => {
                // TODO: Check if from_scope is a subclass
                self.is_same_class_or_subclass(from_scope_id, symbol_table)
            }
            AccessLevel::Private => {
                // Only accessible from the same class/struct
                self.is_same_type_scope(from_scope_id, symbol_table)
            }
            AccessLevel::Public | AccessLevel::Internal | AccessLevel::File => {
                // TODO: Check if same module/project/file for Internal/File
                true // For now, allow public/internal/file access
            }
        }
    }

    /// Check if the symbol is in the same class scope
    fn is_same_type_scope(&self, from_scope_id: &str, symbol_table: &SymbolTable) -> bool {
        // Find the nearest class/struct scope for both symbols
        let self_class_scope = Self::find_nearest_type_scope(self, &self.scope_id, symbol_table);
        let from_class_scope = Self::find_nearest_type_scope(self, from_scope_id, symbol_table);

        match (self_class_scope, from_class_scope) {
            (Some(self_scope), Some(from_scope)) => self_scope == from_scope,
            _ => false,
        }
    }

    /// Check if the accessing scope is the same class or a subclass
    fn is_same_class_or_subclass(&self, from_scope_id: &str, symbol_table: &SymbolTable) -> bool {
        // TODO: Implement proper inheritance checking
        self.is_same_type_scope(from_scope_id, symbol_table)
    }

    /// Find the nearest class/struct/protocol scope
    fn find_nearest_type_scope(
        _self: &Self,
        scope_id: &str,
        symbol_table: &SymbolTable,
    ) -> Option<String> {
        let mut current_scope = symbol_table.scopes.get(scope_id);

        while let Some(scope) = current_scope {
            match scope.kind {
                ScopeKind::Class | ScopeKind::Struct | ScopeKind::Protocol => {
                    return Some(scope.id.clone());
                }
                _ => {}
            }

            current_scope = scope
                .parent
                .as_ref()
                .and_then(|parent_id| symbol_table.scopes.get(parent_id));
        }

        None
    }
}

impl SymbolKind {
    /// Get the string representation of the symbol kind
    #[must_use]
    pub const fn as_str(&self) -> &'static str {
        match self {
            Self::Function => "function",
            Self::Method => "method",
            Self::Class => "class",
            Self::Struct => "struct",
            Self::Protocol => "protocol",
            Self::Variable => "variable",
            Self::Parameter => "parameter",
            Self::Property => "property",
            Self::Constant => "constant",
            Self::Import => "import",
            Self::TypeAlias => "type_alias",
        }
    }
}

/// Main symbol table that manages all symbols and scopes
#[derive(Debug, Clone)]
pub struct SymbolTable {
    /// All symbols indexed by their ID
    pub symbols: HashMap<String, Symbol>,

    /// All scopes indexed by their ID
    pub scopes: HashMap<String, Scope>,

    /// Current active scope ID
    pub current_scope_id: Option<String>,

    /// Root/module scope ID
    pub root_scope_id: Option<String>,

    /// Built-in types
    pub builtin_types: HashMap<String, SemanticType>,
}

impl SymbolTable {
    /// Create a new symbol table
    #[must_use]
    pub fn new() -> Self {
        let builtin_types = crate::semantic::types::create_builtin_types();

        Self {
            symbols: HashMap::new(),
            scopes: HashMap::new(),
            current_scope_id: None,
            root_scope_id: None,
            builtin_types,
        }
    }

    /// Create and enter a new scope
    pub fn enter_scope(&mut self, kind: ScopeKind, name: Option<String>) -> String {
        let parent_id = self.current_scope_id.clone();
        let scope = Scope::new(kind, name, parent_id.clone());
        let scope_id = scope.id.clone();

        // Add as child to parent scope
        if let Some(parent_id) = &parent_id
            && let Some(parent_scope) = self.scopes.get_mut(parent_id)
        {
            parent_scope.add_child(scope_id.clone());
        }

        // Set as root scope if this is the first scope
        if self.root_scope_id.is_none() {
            self.root_scope_id = Some(scope_id.clone());
        }

        self.scopes.insert(scope_id.clone(), scope);
        self.current_scope_id = Some(scope_id.clone());

        scope_id
    }

    /// Exit the current scope and return to parent
    pub fn exit_scope(&mut self) -> Option<String> {
        if let Some(current_id) = &self.current_scope_id
            && let Some(scope) = self.scopes.get(current_id)
        {
            let parent_id = scope.parent.clone();
            self.current_scope_id.clone_from(&parent_id);
            return parent_id;
        }
        None
    }

    /// Add a symbol to the current scope
    /// # Errors
    /// Returns an error if the symbol already exists in the current scope
    pub fn add_symbol(&mut self, symbol: Symbol) -> Result<String, String> {
        let symbol_id = symbol.id.clone();
        let symbol_name = symbol.name.clone();
        let scope_id = symbol.scope_id.clone();

        // Check if symbol already exists in current scope
        if let Some(scope) = self.scopes.get(&scope_id)
            && scope.symbols.contains_key(&symbol_name)
        {
            return Err(format!("Symbol '{symbol_name}' already defined in scope"));
        }

        // Add symbol to scope
        if let Some(scope) = self.scopes.get_mut(&scope_id) {
            scope.add_symbol(symbol_name, symbol_id.clone());
        }

        // Add symbol to symbol table
        self.symbols.insert(symbol_id.clone(), symbol);

        Ok(symbol_id)
    }

    /// Look up a symbol by name, searching through scope hierarchy
    #[must_use]
    pub fn lookup_symbol(&self, name: &str) -> Option<&Symbol> {
        // Start from current scope, or root scope if no current scope
        let start_scope_id = self
            .current_scope_id
            .as_ref()
            .or(self.root_scope_id.as_ref())?;

        let mut current_scope_id = start_scope_id;

        while let Some(scope) = self.scopes.get(current_scope_id) {
            if let Some(symbol_id) = scope.get_symbol(name) {
                return self.symbols.get(symbol_id);
            }

            // Move to parent scope
            if let Some(parent_id) = &scope.parent {
                current_scope_id = parent_id;
            } else {
                break;
            }
        }

        None
    }

    /// Look up a symbol in a specific scope
    #[must_use]
    pub fn lookup_symbol_in_scope(&self, name: &str, scope_id: &str) -> Option<&Symbol> {
        let scope = self.scopes.get(scope_id)?;
        let symbol_id = scope.get_symbol(name)?;
        self.symbols.get(symbol_id)
    }

    /// Look up a type by name (including built-ins)
    #[must_use]
    pub fn lookup_type(&self, name: &str) -> Option<SemanticType> {
        // First check built-in types
        if let Some(builtin_type) = self.builtin_types.get(name) {
            return Some(builtin_type.clone());
        }

        // Then check user-defined types
        self.lookup_symbol(name)
            .and_then(|symbol| match symbol.kind {
                SymbolKind::Class | SymbolKind::Struct | SymbolKind::Protocol => {
                    Some(symbol.symbol_type.clone())
                }
                _ => None,
            })
    }

    /// Get the current scope
    #[must_use]
    pub fn current_scope(&self) -> Option<&Scope> {
        self.current_scope_id
            .as_ref()
            .and_then(|id| self.scopes.get(id))
    }

    /// Get a scope by ID
    #[must_use]
    pub fn get_scope(&self, scope_id: &str) -> Option<&Scope> {
        self.scopes.get(scope_id)
    }

    /// Get a symbol by ID
    #[must_use]
    pub fn get_symbol(&self, symbol_id: &str) -> Option<&Symbol> {
        self.symbols.get(symbol_id)
    }

    /// Get all symbols in a specific scope
    #[must_use]
    pub fn get_symbols_in_scope(&self, scope_id: &str) -> Vec<&Symbol> {
        self.scopes.get(scope_id).map_or_else(Vec::new, |scope| {
            scope
                .symbols
                .values()
                .filter_map(|symbol_id| self.symbols.get(symbol_id))
                .collect()
        })
    }

    /// Debug print the symbol table
    pub fn debug_print(&self) {
        println!("Symbol Table:");
        println!("=============");

        if let Some(root_id) = &self.root_scope_id {
            self.debug_print_scope(root_id, 0);
        }
    }

    /// Debug print a scope and its contents
    fn debug_print_scope(&self, scope_id: &str, indent: usize) {
        let indent_str = "  ".repeat(indent);

        if let Some(scope) = self.scopes.get(scope_id) {
            println!(
                "{}Scope: {} ({})",
                indent_str,
                scope.display_name(),
                scope.id
            );

            // Print symbols in this scope
            for symbol_id in scope.symbols.values() {
                if let Some(symbol) = self.symbols.get(symbol_id) {
                    println!(
                        "{}  {} {}: {} ({})",
                        indent_str,
                        symbol.access_level.as_str(),
                        symbol.kind.as_str(),
                        symbol.name,
                        symbol.symbol_type.display_name()
                    );
                }
            }

            // Print child scopes
            for child_id in &scope.children {
                self.debug_print_scope(child_id, indent + 1);
            }
        }
    }
}

impl Default for SymbolTable {
    fn default() -> Self {
        Self::new()
    }
}
