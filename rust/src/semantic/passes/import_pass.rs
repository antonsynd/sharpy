use super::{AnalysisPass, PassResult};
use crate::ast::node::{Import, ImportFrom, Node};
use crate::semantic::SemanticError;
use crate::semantic::module_registry::ModuleRegistry;
use std::fs;
/// Import resolution pass
/// Handles import statements and module dependency resolution
use std::path::PathBuf;

/// Second pass: resolve imports and module dependencies
pub struct ImportPass;

impl AnalysisPass for ImportPass {
    fn name(&self) -> &'static str {
        "Import Pass"
    }

    fn run(&mut self, ast: &Node, registry: &mut ModuleRegistry) -> PassResult {
        let mut errors = Vec::new();

        if let Err(err) = self.resolve_imports(ast, registry) {
            errors.push(err);
        }

        PassResult {
            errors,
            should_continue: true, // Continue even with import errors
        }
    }

    fn can_continue_with_errors(&self) -> bool {
        true // Import errors don't prevent type checking
    }
}

impl ImportPass {
    /// Resolve all import statements in the AST
    fn resolve_imports(
        &mut self,
        node: &Node,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        match node {
            Node::Module(module) => {
                for stmt in &module.body {
                    self.resolve_imports(stmt, registry)?;
                }
            }

            Node::Import(import) => {
                self.resolve_import_statement(import, registry)?;
            }

            Node::ImportFrom(import_from) => {
                self.resolve_import_from_statement(import_from, registry)?;
            }

            _ => {
                // For other nodes, we don't need to recurse as imports should be top-level
            }
        }

        Ok(())
    }

    /// Resolve a simple import statement
    fn resolve_import_statement(
        &mut self,
        import: &Import,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        for alias in &import.names {
            let module_name = &alias.name;
            let module_path = self.resolve_module_path(module_name, registry)?;

            // Check if module is already registered
            if !registry.has_module(module_name) {
                // Register the new module
                registry.register_module(module_name.clone(), module_path.clone())?;
            }

            // Add import to current module
            if let Some(current_module) = registry.current_module_mut() {
                current_module.add_import(module_name.clone());
            }
        }

        Ok(())
    }

    /// Resolve an import from statement
    fn resolve_import_from_statement(
        &mut self,
        import_from: &ImportFrom,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        if let Some(ref module_name) = import_from.module {
            let module_path = self.resolve_module_path(module_name, registry)?;

            // Check if module is already registered
            if !registry.has_module(module_name) {
                // Register the new module
                registry.register_module(module_name.clone(), module_path)?;
            }

            // Add import to current module
            if let Some(current_module) = registry.current_module_mut() {
                current_module.add_import(module_name.clone());
            }
        }

        Ok(())
    }

    /// Resolve module name to file path using search paths
    fn resolve_module_path(
        &self,
        module_name: &str,
        registry: &ModuleRegistry,
    ) -> Result<String, SemanticError> {
        // Convert module name to file path (e.g., "foo.bar" -> "foo/bar.spy")
        let relative_path = module_name.replace('.', "/") + ".spy";

        // Search in all search paths
        for search_path in registry.search_paths() {
            let full_path = PathBuf::from(search_path).join(&relative_path);

            if full_path.exists() {
                return Ok(full_path.to_string_lossy().to_string());
            }
        }

        // Check cache directory
        let cache_path = PathBuf::from(".sharpy_cache").join(&relative_path);
        if cache_path.exists() {
            return Ok(cache_path.to_string_lossy().to_string());
        }

        Err(SemanticError::ModuleNotFound(module_name.to_string()))
    }

    /// Cache a module for future use
    #[allow(dead_code)]
    fn cache_module(&self, module_name: &str, content: &str) -> Result<(), SemanticError> {
        let cache_dir = PathBuf::from(".sharpy_cache");
        if !cache_dir.exists() {
            fs::create_dir_all(&cache_dir)
                .map_err(|_| SemanticError::ModuleCacheError(module_name.to_string()))?;
        }

        let relative_path = module_name.replace('.', "/") + ".spy";
        let cache_file = cache_dir.join(&relative_path);

        // Create parent directories if needed
        if let Some(parent) = cache_file.parent() {
            fs::create_dir_all(parent)
                .map_err(|_| SemanticError::ModuleCacheError(module_name.to_string()))?;
        }

        fs::write(&cache_file, content)
            .map_err(|_| SemanticError::ModuleCacheError(module_name.to_string()))?;

        Ok(())
    }

    /// Load a module from cache or file system
    #[allow(dead_code)]
    fn load_module(&self, module_path: &str) -> Result<String, SemanticError> {
        fs::read_to_string(module_path)
            .map_err(|_| SemanticError::ModuleLoadError(module_path.to_string()))
    }
}
