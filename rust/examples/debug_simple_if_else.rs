use sharpy_compiler_toolchain::{Parser, SharpyLexer};

fn main() {
    let code = "if x:\n    y = 1\nelse:\n    z = 2";
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
}
