/// Multi-pass semantic analyzer coordinator
/// Orchestrates the execution of semantic analysis passes
use crate::ast::node::Node;
use crate::semantic::passes::{AnalysisPass, DeclarationPass, ImportPass, TypePass};
use crate::semantic::{ModuleRegistry, SemanticError};

/// Multi-pass semantic analyzer
pub struct MultiPassAnalyzer {
    /// Module registry for cross-module analysis
    registry: ModuleRegistry,

    /// Whether to continue analysis on errors
    continue_on_errors: bool,

    /// Last analysis errors (for backward compatibility)
    last_errors: Vec<String>,
}

/// Result of complete semantic analysis
#[derive(Debug)]
pub struct AnalysisResult {
    /// All errors from all passes
    pub errors: Vec<SemanticError>,

    /// Whether analysis completed successfully
    pub success: bool,

    /// Module registry with analyzed symbols
    pub registry: ModuleRegistry,
}

impl MultiPassAnalyzer {
    /// Create a new multi-pass analyzer
    #[must_use]
    pub fn new() -> Self {
        Self {
            registry: ModuleRegistry::new(),
            continue_on_errors: false,
            last_errors: Vec::new(),
        }
    }

    /// Enable/disable continuing analysis on errors
    pub const fn set_continue_on_errors(&mut self, continue_on_errors: bool) {
        self.continue_on_errors = continue_on_errors;
    }

    /// Add search paths for module resolution
    pub fn add_search_paths<I>(&mut self, paths: I)
    where
        I: IntoIterator<Item = String>,
    {
        self.registry.add_search_paths(paths);
    }

    /// Internal multi-pass analysis method
    fn analyze_module_internal(
        &mut self,
        module_name: String,
        file_path: String,
        ast: &Node,
    ) -> AnalysisResult {
        let mut all_errors = Vec::new();

        // Register the module
        if let Err(err) = self
            .registry
            .register_module(module_name.clone(), file_path)
        {
            all_errors.push(err);
            return AnalysisResult {
                errors: all_errors,
                success: false,
                registry: ModuleRegistry::new(),
            };
        }

        // Set as current module
        if let Err(err) = self.registry.set_current_module(&module_name) {
            all_errors.push(err);
            return AnalysisResult {
                errors: all_errors,
                success: false,
                registry: ModuleRegistry::new(),
            };
        }

        // Create passes (simplified for now)
        let mut passes: Vec<Box<dyn AnalysisPass>> = vec![
            Box::new(DeclarationPass),
            Box::new(ImportPass),
            Box::new(TypePass::new()),
        ];

        // Run each pass
        let mut has_errors = false;
        for pass in &mut passes {
            println!("Running {}...", pass.name());

            // Check if we should skip this pass due to previous errors
            if has_errors && !pass.can_continue_with_errors() && !self.continue_on_errors {
                println!("Skipping {} due to previous errors", pass.name());
                continue;
            }

            let result = pass.run(ast, &mut self.registry);

            // Collect errors
            if !result.errors.is_empty() {
                has_errors = true;
                all_errors.extend(result.errors);
            }

            // Check if we should continue
            if !result.should_continue && !self.continue_on_errors {
                break;
            }
        }

        // Mark module as analyzed
        if let Some(module) = self.registry.get_module_mut(&module_name) {
            module.mark_analyzed();
            module.errors.extend(all_errors.clone());
        }

        AnalysisResult {
            success: all_errors.is_empty(),
            errors: all_errors,
            // Don't take the registry for backward compatibility - just create a placeholder
            registry: ModuleRegistry::new(),
        }
    }

    /// Get the current module registry
    #[must_use]
    pub const fn registry(&self) -> &ModuleRegistry {
        &self.registry
    }

    /// Get the current module registry (mutable)
    pub const fn registry_mut(&mut self) -> &mut ModuleRegistry {
        &mut self.registry
    }

    // === Backward-Compatible API ===

    /// Backward-compatible `analyze_module` for legacy tests
    /// Analyzes a list of statements as a module
    pub fn analyze_module(
        &mut self,
        statements: &[Node],
        module_name: Option<String>,
    ) -> Result<(), Vec<String>> {
        use crate::ast::node::Module;

        let module_name = module_name.unwrap_or_else(|| "main".to_string());
        let file_path = format!("{module_name}.spy");

        // Wrap statements in a Module node
        let module_node = Node::Module(Module {
            body: statements.to_vec(),
            source: None,
        });

        // Use the internal multi-pass analysis
        let result = self.analyze_module_internal(module_name, file_path, &module_node);

        if result.success {
            self.last_errors.clear();
            Ok(())
        } else {
            // Convert SemanticError to String for backward compatibility
            let string_errors: Vec<String> =
                result.errors.into_iter().map(|e| e.to_string()).collect();
            self.last_errors = string_errors.clone();
            Err(string_errors)
        }
    }

    /// Get errors from the last analysis (for backward compatibility)
    #[must_use]
    pub const fn get_errors(&self) -> &Vec<String> {
        &self.last_errors
    }

    /// Get a reference to the symbol table from the current module
    /// For backward compatibility with legacy tests
    #[must_use]
    pub fn get_symbol_table(&self) -> &crate::semantic::SymbolTable {
        // Get the symbol table from the current module, or return a static empty one
        if let Some(current_module_name) = self.registry.current_module_name() {
            println!("DEBUG: Current module name: {current_module_name}");
            if let Some(current_module) = self.registry.get_module(current_module_name) {
                println!("DEBUG: Found current module, returning its symbol table");
                return &current_module.symbols;
            }
            println!("DEBUG: Current module not found in registry");
        } else {
            println!("DEBUG: No current module name");
        }

        // Return a reference to an empty symbol table for compatibility
        println!("DEBUG: Returning empty symbol table");
        static EMPTY_SYMBOL_TABLE: std::sync::LazyLock<crate::semantic::SymbolTable> =
            std::sync::LazyLock::new(crate::semantic::SymbolTable::new);
        &EMPTY_SYMBOL_TABLE
    }
}

impl Default for MultiPassAnalyzer {
    fn default() -> Self {
        Self::new()
    }
}
