use sharpy_compiler_toolchain::{Parser, SemanticAnalyzer, SharpyLexer};

fn main() {
    let code = r"
numbers: List[int] = [1, 2, 3]
append_method = numbers.append
";

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Tokenization failed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing failed");

    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    match result {
        Ok(()) => {
            println!("Analysis succeeded");
            let symbol_table = analyzer.get_symbol_table();
            if let Some(numbers_symbol) = symbol_table.lookup_symbol("numbers") {
                println!("numbers type: {:?}", numbers_symbol.symbol_type);
            }
            if let Some(append_method_symbol) = symbol_table.lookup_symbol("append_method") {
                println!("append_method type: {:?}", append_method_symbol.symbol_type);
            }
        }
        Err(err) => println!("Analysis failed with error: {err:?}"),
    }
}
