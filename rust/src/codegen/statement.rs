use super::context::CodeGenContext;
use super::{CodeGenError, CodeGenResult};
/// Statement code generation (stub - P1)
///
/// Will generate C# code for Sharpy statements
use crate::ast::Node;

pub fn generate_statement(_stmt: &Node, _ctx: &mut CodeGenContext) -> CodeGenResult<String> {
    Err(CodeGenError::NotImplemented {
        feature: "Statement generation".to_string(),
    })
}
