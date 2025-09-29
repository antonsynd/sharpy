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

    /// Analyze a module (simplified version for now)
    pub fn analyze_module(
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
                registry: std::mem::take(&mut self.registry),
            };
        }

        // Set as current module
        if let Err(err) = self.registry.set_current_module(&module_name) {
            all_errors.push(err);
            return AnalysisResult {
                errors: all_errors,
                success: false,
                registry: std::mem::take(&mut self.registry),
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
            registry: std::mem::take(&mut self.registry),
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
}

impl Default for MultiPassAnalyzer {
    fn default() -> Self {
        Self::new()
    }
}
