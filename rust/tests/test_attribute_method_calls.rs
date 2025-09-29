use sharpy_compiler_toolchain::{BuiltinType, Parser, SemanticAnalyzer, SemanticType, SharpyLexer};

#[test]
fn test_string_method_access() {
    let code = r#"
s: str = "hello"
upper_method = s.upper
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

    // Check that upper_method has the correct function type
    let symbol_table = analyzer.get_symbol_table();
    let upper_method_symbol = symbol_table
        .lookup_symbol("upper_method")
        .expect("upper_method should be in symbol table");

    match &upper_method_symbol.symbol_type {
        SemanticType::Function { return_type, .. } => {
            assert!(return_type.is_some(), "Method should have return type");
            if let Some(return_type) = return_type {
                assert_eq!(**return_type, SemanticType::Builtin(BuiltinType::Str));
            }
        }
        _ => panic!("Expected function type for method access"),
    }
}

#[test]
fn test_list_method_access() {
    let code = r#"
lst: List[int] = [1, 2, 3]
append_method = lst.append
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
}

#[test]
fn test_attribute_access_on_builtin_types() {
    let code = r#"
s: str = "test"
split_func = s.split
upper_func = s.upper

numbers: List[int] = [1, 2, 3]
pop_func = numbers.pop
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
}

#[test]
fn test_invalid_attribute_access() {
    let code = r#"
s: str = "hello"
invalid = s.nonexistent_method
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Tokenization failed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing failed");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_err(),
        "Analysis should fail for invalid attribute"
    );
    let error_msg = format!("{:?}", result.err().unwrap());
    assert!(error_msg.contains("has no attribute"));
}

#[test]
fn test_method_call_vs_method_access() {
    let code = r#"
s: str = "hello"
method_ref = s.upper    # Method access (returns function)
method_call = s.upper() # Method call (returns result)
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

    // method_ref should be a function type
    let method_ref_symbol = symbol_table
        .lookup_symbol("method_ref")
        .expect("method_ref should be in symbol table");
    assert!(matches!(
        method_ref_symbol.symbol_type,
        SemanticType::Function { .. }
    ));

    // method_call should be the return type (string)
    let method_call_symbol = symbol_table
        .lookup_symbol("method_call")
        .expect("method_call should be in symbol table");
    assert_eq!(
        method_call_symbol.symbol_type,
        SemanticType::Builtin(BuiltinType::Str)
    );
}

#[test]
fn test_chained_attribute_access() {
    let code = r#"
s: str = "hello world"
result = s.split().pop()
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
}
