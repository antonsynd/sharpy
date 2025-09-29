use sharpy_compiler_toolchain::{Parser, SemanticAnalyzer, SharpyLexer};
use std::env;
use std::fs;

fn main() {
    let args: Vec<String> = env::args().collect();

    let code = if args.len() > 1 {
        fs::read_to_string(&args[1]).expect("Failed to read file")
    } else {
        "result = unknown_function(42)".to_string()
    };

    let mut lexer = SharpyLexer::new(&code);
    let tokens = lexer.tokenize_all().expect("Tokenization failed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing failed");

    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    match result {
        Ok(()) => {
            println!("Analysis succeeded");
        }
        Err(errors) => {
            println!("Analysis failed with errors:");
            for error in errors {
                println!("  {error}");
            }
        }
    }
}
