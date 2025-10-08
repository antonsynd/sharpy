/// Type generation (stub - P1)
///
/// Will generate C# classes, structs, interfaces from Sharpy types
use super::context::CodeGenContext;
use super::{CodeGenError, CodeGenResult};

pub fn generate_class(
    _class_def: &crate::ast::node::ClassDef,
    _ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    Err(CodeGenError::NotImplemented {
        feature: "Class generation".to_string(),
    })
}

pub fn generate_struct(
    _struct_def: &crate::ast::node::StructDef,
    _ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    Err(CodeGenError::NotImplemented {
        feature: "Struct generation".to_string(),
    })
}

pub fn generate_protocol(
    _protocol_def: &crate::ast::node::ProtocolDef,
    _ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    Err(CodeGenError::NotImplemented {
        feature: "Protocol generation".to_string(),
    })
}
