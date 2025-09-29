use crate::semantic::{ScopeKind, SemanticError, Symbol, SymbolTable};
/// Module registry for managing per-module symbol tables
/// and cross-module symbol resolution
use std::collections::HashMap;

/// Registry of all modules and their symbol tables
#[derive(Debug)]
pub struct ModuleRegistry {
    /// Per-module symbol tables
    modules: HashMap<String, ModuleSymbolTable>,

    /// Currently active module for analysis
    current_module: Option<String>,

    /// Search paths for module resolution
    search_paths: Vec<String>,
}

/// Symbol table for a single module with metadata
#[derive(Debug)]
pub struct ModuleSymbolTable {
    /// The symbol table for this module
    pub symbols: SymbolTable,

    /// Module file path
    pub file_path: String,

    /// Modules imported by this module
    pub imports: Vec<String>,

    /// Whether this module has been fully analyzed
    pub is_analyzed: bool,

    /// Analysis errors for this module
    pub errors: Vec<SemanticError>,
}

impl ModuleRegistry {
    /// Create a new module registry
    #[must_use]
    pub fn new() -> Self {
        Self {
            modules: HashMap::new(),
            current_module: None,
            search_paths: vec![".".to_string()], // Current directory by default
        }
    }

    /// Add search paths for module resolution
    pub fn add_search_paths<I>(&mut self, paths: I)
    where
        I: IntoIterator<Item = String>,
    {
        self.search_paths.extend(paths);
    }

    /// Register a new module
    pub fn register_module(
        &mut self,
        name: String,
        file_path: String,
    ) -> Result<(), SemanticError> {
        if self.modules.contains_key(&name) {
            return Err(SemanticError::DuplicateModule(name));
        }

        let mut symbols = SymbolTable::new();

        // Add builtin functions to the symbol table
        symbols.add_builtin_functions();

        // Create and enter the module-level scope
        symbols.enter_scope(ScopeKind::Module, Some(name.clone()));

        let module_table = ModuleSymbolTable {
            symbols,
            file_path,
            imports: Vec::new(),
            is_analyzed: false,
            errors: Vec::new(),
        };

        self.modules.insert(name, module_table);
        Ok(())
    }

    /// Set the current module for analysis
    pub fn set_current_module(&mut self, name: &str) -> Result<(), SemanticError> {
        if !self.modules.contains_key(name) {
            return Err(SemanticError::UnknownModule(name.to_string()));
        }
        self.current_module = Some(name.to_string());
        Ok(())
    }

    /// Get the current module's symbol table (mutable)
    pub fn current_module_mut(&mut self) -> Option<&mut ModuleSymbolTable> {
        let module_name = self.current_module.as_ref()?;
        self.modules.get_mut(module_name)
    }

    /// Get the current module name
    #[must_use]
    pub const fn current_module_name(&self) -> Option<&String> {
        self.current_module.as_ref()
    }

    /// Get a module's symbol table (immutable)
    #[must_use]
    pub fn get_module(&self, name: &str) -> Option<&ModuleSymbolTable> {
        self.modules.get(name)
    }

    /// Get a module's symbol table (mutable)
    pub fn get_module_mut(&mut self, name: &str) -> Option<&mut ModuleSymbolTable> {
        self.modules.get_mut(name)
    }

    /// Resolve a symbol across modules
    /// First checks current module, then imported modules
    #[must_use]
    pub fn resolve_symbol(&self, name: &str) -> Option<&Symbol> {
        // Check current module first
        if let Some(current_name) = &self.current_module
            && let Some(current_module) = self.modules.get(current_name)
        {
            if let Some(symbol) = current_module.symbols.lookup_symbol(name) {
                return Some(symbol);
            }

            // Check imported modules
            for import in &current_module.imports {
                if let Some(imported_module) = self.modules.get(import)
                    && let Some(symbol) = imported_module.symbols.lookup_symbol(name)
                {
                    return Some(symbol);
                }
            }
        }

        None
    }

    /// Get all module names
    pub fn module_names(&self) -> impl Iterator<Item = &String> {
        self.modules.keys()
    }

    /// Check if a module exists
    #[must_use]
    pub fn has_module(&self, name: &str) -> bool {
        self.modules.contains_key(name)
    }

    /// Get search paths
    #[must_use]
    pub fn search_paths(&self) -> &[String] {
        &self.search_paths
    }
}

impl Default for ModuleRegistry {
    fn default() -> Self {
        Self::new()
    }
}

impl ModuleSymbolTable {
    /// Add an import to this module
    pub fn add_import(&mut self, module_name: String) {
        if !self.imports.contains(&module_name) {
            self.imports.push(module_name);
        }
    }

    /// Mark this module as fully analyzed
    pub const fn mark_analyzed(&mut self) {
        self.is_analyzed = true;
    }

    /// Add an error to this module
    pub fn add_error(&mut self, error: SemanticError) {
        self.errors.push(error);
    }
}
