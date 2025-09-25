use sharpy_compiler_toolchain::{SharpyLexer, TokenType};

fn main() {
    let input = r#"f"Hello world""#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    println!("Input: {}", input);
    println!("Number of tokens: {}", tokens.len());

    for (i, token) in tokens.iter().enumerate() {
        println!("Token {}: {:?}", i, token.token_type);
    }
}
