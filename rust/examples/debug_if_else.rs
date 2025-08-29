use sharpy_compiler_toolchain::{Parser, SharpyLexer};

fn main() {
    let code = "if age >= 18:\n    status = \"adult\"\nelse:\n    status = \"minor\"";
    println!("Code:\n{}\n", code);

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().unwrap();

    println!("Tokens:");
    for (i, token) in tokens.iter().enumerate() {
        println!(
            "  {}: {:?} at {}:{}",
            i, token.token_type, token.location.line, token.location.column
        );
    }

    println!("\nParsing...");
    let mut parser = Parser::new(tokens);
    match parser.parse() {
        Ok(nodes) => {
            println!("Success: {} AST nodes", nodes.len());
        }
        Err(err) => {
            println!("Parse error: {}", err);
        }
    }
}
