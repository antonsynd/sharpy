use sharpy_compiler_toolchain::{SemanticAnalyzer, SharpyLexer, Parser};

#[test]
fn debug_simple_function() {
    let code = r#"def hello() -> str:
    return "Hello""#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    match analyzer.analyze_module(&ast, Some("test".to_string())) {
        Ok(()) => {
            println!("Analysis succeeded!");
            analyzer.get_symbol_table().debug_print();

            // Test symbol lookup
            let hello_symbol = analyzer.get_symbol_table().lookup_symbol("hello");
            println!("Symbol lookup result: {:?}", hello_symbol.is_some());
            if let Some(symbol) = hello_symbol {
                println!("Found symbol: {} of kind {:?}", symbol.name, symbol.kind);
            }
        },
        Err(errors) => {
            println!("Analysis failed: {:?}", errors);
        }
    }
}
