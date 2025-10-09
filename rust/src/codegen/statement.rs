use super::context::CodeGenContext;
use super::expression::generate_expression;
use super::{CodeGenError, CodeGenResult};
/// Statement code generation
///
/// Generates C# code for Sharpy statements
use crate::ast::Node;

/// Generate C# code for a statement
///
/// # Arguments
/// * `stmt` - The statement AST node
/// * `ctx` - Code generation context
///
/// # Returns
/// Generated C# code as a string
pub fn generate_statement(stmt: &Node, ctx: &mut CodeGenContext) -> CodeGenResult<String> {
    match stmt {
        Node::FunctionDef(func_def) => generate_function_def(func_def, ctx),
        Node::Return(ret) => generate_return(ret, ctx),
        Node::Assign(assign) => generate_assign(assign, ctx),
        Node::If(if_stmt) => generate_if(if_stmt, ctx),
        Node::While(while_stmt) => generate_while(while_stmt, ctx),
        Node::For(for_stmt) => generate_for(for_stmt, ctx),
        Node::Pass(_) => {
            // Pass translates to empty statement or comment
            Ok("// pass".to_string())
        }
        // Expression statements (e.g., function calls)
        Node::Call(_) => {
            let expr_code = generate_expression(stmt, ctx)?;
            Ok(format!("{expr_code};"))
        }
        _ => Err(CodeGenError::NotImplemented {
            feature: format!("Statement: {stmt:?}"),
        }),
    }
}

/// Generate function definition
fn generate_function_def(
    func_def: &crate::ast::node::FunctionDef,
    ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    let mut output = String::new();

    // Generate XML doc comment if docstring exists
    if let Some(first_stmt) = func_def.body.first()
        && let Node::Constant(c) = first_stmt
        && let crate::ast::node::ConstantValue::Str(docstring) = &c.value
    {
        output.push_str(&ctx.get_indent());
        output.push_str("/// <summary>\n");
        for line in docstring.lines() {
            output.push_str(&ctx.get_indent());
            output.push_str(&format!("/// {line}\n"));
        }
        output.push_str(&ctx.get_indent());
        output.push_str("/// </summary>\n");
    }

    // Determine access modifier
    let access = if func_def.name == "main" || func_def.name == "__init__" {
        "public"
    } else {
        "private"
    };

    // Mangle function name
    let mangled_name = ctx.mangle_name(
        &func_def.name,
        super::name_mangling::NameContext::Method,
        false,
    )?;

    // Generate return type
    let return_type = if func_def.return_type.is_some() {
        // For P0, we'll just use 'void' or simple type names
        // Full type resolution from AST nodes comes in P1
        "void".to_string()
    } else {
        "void".to_string()
    };

    // Generate parameters
    let mut params = Vec::new();
    for arg in &func_def.args.args {
        let param_name = ctx.mangle_name(
            &arg.name,
            super::name_mangling::NameContext::Parameter,
            false,
        )?;
        let param_type = if arg.type_.is_some() {
            // For P0, use 'object' for typed parameters
            // Full type resolution comes in P1
            "object".to_string()
        } else {
            "object".to_string() // Untyped parameter
        };
        params.push(format!("{param_type} {param_name}"));
    }

    // Function signature
    output.push_str(&ctx.get_indent());
    output.push_str(&format!(
        "{access} static {return_type} {mangled_name}({})\n",
        params.join(", ")
    ));
    output.push_str(&ctx.get_indent());
    output.push_str("{\n");

    // Generate body
    ctx.indent();
    for stmt in &func_def.body {
        // Skip docstrings (already handled)
        if let Node::Constant(c) = stmt
            && matches!(c.value, crate::ast::node::ConstantValue::Str(_))
        {
            continue;
        }

        let stmt_code = generate_statement(stmt, ctx)?;
        if !stmt_code.is_empty() {
            output.push_str(&ctx.get_indent());
            output.push_str(&stmt_code);
            output.push('\n');
        }
    }
    ctx.dedent();

    output.push_str(&ctx.get_indent());
    output.push('}');
    Ok(output)
}

/// Generate return statement
fn generate_return(
    ret: &crate::ast::node::Return,
    ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    if let Some(value) = &ret.value {
        let expr_code = generate_expression(value, ctx)?;
        Ok(format!("return {expr_code};"))
    } else {
        Ok("return;".to_string())
    }
}

/// Generate assignment statement
fn generate_assign(
    assign: &crate::ast::node::Assign,
    ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    let value_code = generate_expression(&assign.value, ctx)?;

    match assign.target.as_ref() {
        Node::Name(name) => {
            let var_name = ctx.mangle_name(
                &name.id,
                super::name_mangling::NameContext::LocalVariable,
                false,
            )?;

            // For P0, we'll use 'var' for type inference
            Ok(format!("var {var_name} = {value_code};"))
        }
        _ => Err(CodeGenError::NotImplemented {
            feature: format!("Assignment target: {:?}", assign.target),
        }),
    }
}

/// Generate if statement
fn generate_if(if_stmt: &crate::ast::node::If, ctx: &mut CodeGenContext) -> CodeGenResult<String> {
    let mut output = String::new();

    // Test condition
    let test_code = generate_expression(&if_stmt.test, ctx)?;
    output.push_str(&format!("if ({test_code})\n"));
    output.push_str(&ctx.get_indent());
    output.push_str("{\n");

    // Body
    ctx.indent();
    for stmt in &if_stmt.body {
        let stmt_code = generate_statement(stmt, ctx)?;
        if !stmt_code.is_empty() {
            output.push_str(&ctx.get_indent());
            output.push_str(&stmt_code);
            output.push('\n');
        }
    }
    ctx.dedent();

    output.push_str(&ctx.get_indent());
    output.push('}');

    // Else clause
    if !if_stmt.else_.is_empty() {
        output.push('\n');
        output.push_str(&ctx.get_indent());
        output.push_str("else\n");
        output.push_str(&ctx.get_indent());
        output.push_str("{\n");

        ctx.indent();
        for stmt in &if_stmt.else_ {
            let stmt_code = generate_statement(stmt, ctx)?;
            if !stmt_code.is_empty() {
                output.push_str(&ctx.get_indent());
                output.push_str(&stmt_code);
                output.push('\n');
            }
        }
        ctx.dedent();

        output.push_str(&ctx.get_indent());
        output.push('}');
    }

    Ok(output)
}

/// Generate while loop
fn generate_while(
    while_stmt: &crate::ast::node::While,
    ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    let mut output = String::new();

    // Test condition
    let test_code = generate_expression(&while_stmt.test, ctx)?;
    output.push_str(&format!("while ({test_code})\n"));
    output.push_str(&ctx.get_indent());
    output.push_str("{\n");

    // Body
    ctx.indent();
    for stmt in &while_stmt.body {
        let stmt_code = generate_statement(stmt, ctx)?;
        if !stmt_code.is_empty() {
            output.push_str(&ctx.get_indent());
            output.push_str(&stmt_code);
            output.push('\n');
        }
    }
    ctx.dedent();

    output.push_str(&ctx.get_indent());
    output.push('}');

    Ok(output)
}

/// Generate for loop
fn generate_for(
    for_stmt: &crate::ast::node::For,
    ctx: &mut CodeGenContext,
) -> CodeGenResult<String> {
    let mut output = String::new();

    // Get loop variable
    let loop_var = match for_stmt.target.as_ref() {
        Node::Name(name) => ctx.mangle_name(
            &name.id,
            super::name_mangling::NameContext::LocalVariable,
            false,
        )?,
        _ => {
            return Err(CodeGenError::NotImplemented {
                feature: "Complex for loop target".to_string(),
            });
        }
    };

    // Generate iterable expression
    let iter_code = generate_expression(&for_stmt.iter, ctx)?;

    // Use foreach for C#
    output.push_str(&format!("foreach (var {loop_var} in {iter_code})\n"));
    output.push_str(&ctx.get_indent());
    output.push_str("{\n");

    // Body
    ctx.indent();
    for stmt in &for_stmt.body {
        let stmt_code = generate_statement(stmt, ctx)?;
        if !stmt_code.is_empty() {
            output.push_str(&ctx.get_indent());
            output.push_str(&stmt_code);
            output.push('\n');
        }
    }
    ctx.dedent();

    output.push_str(&ctx.get_indent());
    output.push('}');

    Ok(output)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ast::node::{Arguments, Constant, ConstantValue, FunctionDef, Name, Return};
    use crate::semantic::SymbolTable;

    #[test]
    fn test_generate_return() {
        let symbol_table = SymbolTable::new();
        let mut ctx = CodeGenContext::new(symbol_table);

        let ret = Return {
            value: Some(Box::new(Node::Constant(Constant {
                value: ConstantValue::Int(42),
                source: None,
            }))),
            source: None,
        };

        let code = generate_return(&ret, &mut ctx).unwrap();
        assert_eq!(code, "return 42;");
    }

    #[test]
    fn test_generate_simple_function() {
        let symbol_table = SymbolTable::new();
        let mut ctx = CodeGenContext::new(symbol_table);

        let func_def = FunctionDef {
            name: "hello_world".to_string(),
            args: Arguments { args: vec![] },
            body: vec![Node::Return(Return {
                value: Some(Box::new(Node::Constant(Constant {
                    value: ConstantValue::Str("Hello!".to_string()),
                    source: None,
                }))),
                source: None,
            })],
            decorators: vec![],
            return_type: None,
            access_modifier: None,
            source: None,
        };

        let code = generate_function_def(&func_def, &mut ctx).unwrap();
        assert!(code.contains("private static void HelloWorld()"));
        assert!(code.contains("return \"Hello!\";"));
    }

    #[test]
    fn test_generate_assignment() {
        let symbol_table = SymbolTable::new();
        let mut ctx = CodeGenContext::new(symbol_table);

        let assign = crate::ast::node::Assign {
            target: Box::new(Node::Name(Name {
                id: "my_var".to_string(),
                source: None,
            })),
            value: Box::new(Node::Constant(Constant {
                value: ConstantValue::Int(42),
                source: None,
            })),
            source: None,
        };

        let code = generate_assign(&assign, &mut ctx).unwrap();
        assert_eq!(code, "var myVar = 42;");
    }
}
