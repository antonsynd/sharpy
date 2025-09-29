use sharpy_compiler_toolchain::{Parser, SemanticAnalyzer, SharpyLexer};

fn main() {
    let code = r"
def simple_func():
    pass
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
            if let Some(simple_func_symbol) = symbol_table.lookup_symbol("simple_func") {
                println!("simple_func symbol found: {simple_func_symbol:?}");
            } else {
                println!("simple_func symbol NOT found");
            }
        }
        Err(err) => println!("Analysis failed with error: {err:?}"),
    }
}
