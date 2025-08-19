use super::super::lexer::token::Token;
use super::error::AstError;
use super::node::*;

pub struct SharpyAstBuilder {
    // Fields for building the AST
}

impl Default for SharpyAstBuilder {
    fn default() -> Self {
        Self::new()
    }
}

impl SharpyAstBuilder {
    #[must_use]
    pub const fn new() -> Self {
        Self {
            // Initialize fields
        }
    }

    /// Builds the AST from the provided tokens.
    /// # Errors
    /// Returns an `ASTError`.
    pub fn build(&self, tokens: &[Token]) -> Result<Node, AstError> {
        if tokens.is_empty() {
            return Err(AstError::EmptyInput);
        }

        Ok(Node::Pass(Pass { source: None }))
    }
}
