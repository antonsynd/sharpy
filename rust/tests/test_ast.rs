use sharpy_compiler_toolchain::ast::*;
use sharpy_compiler_toolchain::lexer::{LexerError, SharpyLexer};

fn generate_ast(code: &str) -> Result<Node, anyhow::Error> {
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().map_err(|errs: Vec<LexerError>| {
        anyhow::anyhow!(
            "Lexer errors: {}",
            errs.iter()
                .map(|e| format!("{}", e))
                .collect::<Vec<_>>()
                .join(", ")
        )
    })?;

    let builder = SharpyAstBuilder::new();
    let ast = builder.build(&tokens)?;

    Ok(ast)
}

#[test]
fn test_name() {
    let code = "x";
    let ast = generate_ast(code);
    assert!(ast.is_ok());
}
