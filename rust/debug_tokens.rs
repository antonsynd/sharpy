use sharpy_compiler_toolchain::lexer::Lexer;

fn main() {
    let code = "if age >= 18:\n    status = \"adult\"\nelse:\n    status = \"minor\"";
    println!("Code:\n{}\n", code);

    let mut lexer = Lexer::new(code);
    let tokens = lexer.tokenize();

    println!("Tokens:");
    for (i, token) in tokens.iter().enumerate() {
        println!("  {}: {:?} at {}:{}", i, token.token_type, token.line, token.column);
    }
}
