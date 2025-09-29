use crate::ast::node::Node;
/// Multi-pass semantic analysis framework
use crate::semantic::SemanticError;
use crate::semantic::module_registry::ModuleRegistry;

pub mod declaration_pass;
pub mod import_pass;
pub mod type_pass;

pub use declaration_pass::DeclarationPass;
pub use import_pass::ImportPass;
pub use type_pass::TypePass;

/// Result of running an analysis pass
#[derive(Debug)]
pub struct PassResult {
    /// Errors encountered during this pass
    pub errors: Vec<SemanticError>,

    /// Whether analysis should continue to next pass
    pub should_continue: bool,
}

/// Trait for analysis passes
pub trait AnalysisPass {
    /// Name of this pass for debugging/logging
    fn name(&self) -> &'static str;

    /// Run this analysis pass on the AST
    fn run(&mut self, ast: &Node, registry: &mut ModuleRegistry) -> PassResult;

    /// Whether this pass can continue with errors from previous passes
    fn can_continue_with_errors(&self) -> bool;
}
