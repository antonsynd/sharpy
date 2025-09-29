use sharpy_compiler_toolchain::{BuiltinType, Parser, SemanticAnalyzer, SemanticType, SharpyLexer};

#[test]
fn test_simple_constructor() {
    let code = r#"
class Simple:
    pass

obj = Simple()
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Tokenization failed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing failed");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Analysis should succeed: {:?}",
        result.err()
    );

    let symbol_table = analyzer.get_symbol_table();
    let obj_symbol = symbol_table
        .lookup_symbol("obj")
        .expect("obj should be in symbol table");

    // Should be an instance of Simple class
    match &obj_symbol.symbol_type {
        SemanticType::Class { name, .. } => {
            assert_eq!(name, "Simple");
        }
        _ => panic!(
            "Expected class type for obj, got {:?}",
            obj_symbol.symbol_type
        ),
    }
}
