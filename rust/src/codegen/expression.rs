/// Expression code generation (P0 - basic literals and operations)
///
/// P0 implementation focuses on:
/// - Literals (int, float, str, bool, None)
/// - Basic binary operations
/// - Basic comparisons
use crate::ast::Node;
use crate::ast::node::{BinaryOp, CompOp, ConstantValue, UnaryOp};

use super::context::CodeGenContext;
use super::{CodeGenError, CodeGenResult};

/// Generate code for an expression node
pub fn generate_expression(expr: &Node, ctx: &mut CodeGenContext) -> CodeGenResult<String> {
    match expr {
        Node::Constant(const_node) => generate_constant(&const_node.value, ctx),
        Node::Name(name_node) => generate_name(&name_node.id, ctx),
        Node::BinaryOp(binop_node) => generate_binary_op(binop_node, ctx),
        Node::UnaryOp(unop_node) => generate_unary_op(unop_node, ctx),
        Node::Compare(cmp_node) => generate_compare(cmp_node, ctx),
        _ => Err(CodeGenError::NotImplemented {
            feature: format!("Expression: {expr:?}"),
        }),
    }
}

/// Generate constant literal
fn generate_constant(
    const_val: &ConstantValue,
    _ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    match const_val {
        ConstantValue::Int(n) => Ok(n.to_string()),
        ConstantValue::Float(f) => Ok(format!("{f}")),
        ConstantValue::Str(s) => Ok(format!("\"{}\"", escape_string(s))),
        ConstantValue::Bool(b) => Ok(if *b { "true" } else { "false" }.to_string()),
        ConstantValue::None => Ok("null".to_string()),
        _ => Err(CodeGenError::NotImplemented {
            feature: format!("Constant type: {const_val:?}"),
        }),
    }
}

/// Escape string for C# literal
fn escape_string(s: &str) -> String {
    s.replace('\\', "\\\\")
        .replace('"', "\\\"")
        .replace('\n', "\\n")
        .replace('\r', "\\r")
        .replace('\t', "\\t")
}

/// Generate name reference
fn generate_name(name: &str, ctx: &mut CodeGenContext) -> CodeGenResult<String> {
    // Look up in symbol table to determine if it's a builtin or user-defined
    let symbol = ctx.lookup_symbol(name);

    if let Some(_sym) = symbol {
        // For P0, use the mangled name
        // TODO: Handle different symbol kinds (local var vs parameter vs member)
        ctx.mangle_name(name, super::NameContext::LocalVariable, false)
    } else {
        // Not in symbol table - might be a builtin or error
        Ok(name.to_string())
    }
}

/// Generate binary operation
fn generate_binary_op(
    binop: &crate::ast::node::BinaryOp_,
    ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    let left = generate_expression(&binop.left, ctx)?;
    let right = generate_expression(&binop.right, ctx)?;

    let (op_str, needs_func) = match binop.op {
        BinaryOp::Add => ("+", false),
        BinaryOp::Sub => ("-", false),
        BinaryOp::Mult => ("*", false),
        BinaryOp::Div => ("/", false),
        BinaryOp::FloorDiv => ("/", false), // TODO: Need Math.Floor wrapper
        BinaryOp::Mod => ("%", false),
        BinaryOp::Pow => ("Math.Pow", true),
        BinaryOp::BitwiseAnd => ("&", false),
        BinaryOp::BitwiseOr => ("|", false),
        BinaryOp::BitwiseXor => ("^", false),
        BinaryOp::LShift => ("<<", false),
        BinaryOp::RShift => (">>", false),
        _ => {
            return Err(CodeGenError::NotImplemented {
                feature: format!("Binary operator: {binop:?}"),
            });
        }
    };

    if needs_func {
        Ok(format!("{op_str}({left}, {right})"))
    } else {
        Ok(format!("({left} {op_str} {right})"))
    }
}

/// Generate unary operation
fn generate_unary_op(
    unop: &crate::ast::node::UnaryOp_,
    ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    let operand = generate_expression(&unop.operand, ctx)?;

    let op_str = match unop.op {
        UnaryOp::UnarySub => "-",
        UnaryOp::UnaryAdd => "+",
        UnaryOp::Invert => "~",
        UnaryOp::Not => "!",
    };

    Ok(format!("({op_str}{operand})"))
}

/// Generate comparison
fn generate_compare(
    cmp: &crate::ast::node::Compare,
    ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    let left = generate_expression(&cmp.left, ctx)?;

    // For P0, only handle single comparison
    if cmp.comparators.len() != 1 || cmp.ops.len() != 1 {
        return Err(CodeGenError::NotImplemented {
            feature: "Chained comparisons".to_string(),
        });
    }

    let right = generate_expression(&cmp.comparators[0], ctx)?;
    let op_str = match cmp.ops[0] {
        CompOp::Eq => "==",
        CompOp::NotEq => "!=",
        CompOp::Lt => "<",
        CompOp::Gt => ">",
        CompOp::LtE => "<=",
        CompOp::GtE => ">=",
        _ => {
            return Err(CodeGenError::NotImplemented {
                feature: format!("Comparison operator: {:?}", cmp.ops[0]),
            });
        }
    };

    Ok(format!("({left} {op_str} {right})"))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::semantic::SymbolTable;

    #[test]
    fn test_generate_int_constant() {
        let mut ctx = CodeGenContext::new(SymbolTable::new());
        let result = generate_constant(&ConstantValue::Int(42), &mut ctx).unwrap();
        assert_eq!(result, "42");
    }

    #[test]
    fn test_generate_float_constant() {
        let mut ctx = CodeGenContext::new(SymbolTable::new());
        let result = generate_constant(&ConstantValue::Float(3.14), &mut ctx).unwrap();
        assert_eq!(result, "3.14");
    }

    #[test]
    fn test_generate_string_constant() {
        let mut ctx = CodeGenContext::new(SymbolTable::new());
        let result = generate_constant(&ConstantValue::Str("hello".to_string()), &mut ctx).unwrap();
        assert_eq!(result, "\"hello\"");
    }

    #[test]
    fn test_generate_bool_constant() {
        let mut ctx = CodeGenContext::new(SymbolTable::new());
        let result = generate_constant(&ConstantValue::Bool(true), &mut ctx).unwrap();
        assert_eq!(result, "true");
    }

    #[test]
    fn test_generate_none_constant() {
        let mut ctx = CodeGenContext::new(SymbolTable::new());
        let result = generate_constant(&ConstantValue::None, &mut ctx).unwrap();
        assert_eq!(result, "null");
    }

    #[test]
    fn test_escape_string() {
        assert_eq!(escape_string("hello"), "hello");
        assert_eq!(escape_string("hello\"world"), "hello\\\"world");
        assert_eq!(escape_string("hello\\world"), "hello\\\\world");
        assert_eq!(escape_string("hello\nworld"), "hello\\nworld");
    }
}
