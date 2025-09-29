use sharpy_compiler_toolchain::{BuiltinType, Parser, SemanticAnalyzer, SemanticType, SharpyLexer};

#[test]
fn test_comprehensive_method_calls_and_attributes() {
    let code = r#"
# Test comprehensive method functionality
text: str = "Hello World"
numbers: List[int] = [1, 2, 3]
data: Dict[str, int] = {"a": 1, "b": 2}

# Basic method access (should return function types)
upper_method = text.upper
split_method = text.split
append_method = numbers.append

# Method calls (should return result types)
upper_result = text.upper()        # str
split_result = text.split()        # List[str]
length_result = len(text)          # int

# Chained method calls
chained_result = text.lower().split()

# Collection method calls
numbers.append(4)
first_element = numbers.pop()
dict_keys = data.keys()
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

    // Check that method access returns function types
    let upper_method_symbol = symbol_table
        .lookup_symbol("upper_method")
        .expect("upper_method should be in symbol table");
    assert!(matches!(
        upper_method_symbol.symbol_type,
        SemanticType::Function { .. }
    ));

    // Check that method call returns correct result type
    let upper_result_symbol = symbol_table
        .lookup_symbol("upper_result")
        .expect("upper_result should be in symbol table");
    assert_eq!(
        upper_result_symbol.symbol_type,
        SemanticType::Builtin(BuiltinType::Str)
    );

    // Check chained method call
    let chained_result_symbol = symbol_table
        .lookup_symbol("chained_result")
        .expect("chained_result should be in symbol table");
    match &chained_result_symbol.symbol_type {
        SemanticType::Generic { base, args } => {
            assert_eq!(**base, SemanticType::Builtin(BuiltinType::List));
            assert_eq!(args[0], SemanticType::Builtin(BuiltinType::Str));
        }
        _ => panic!("Expected List[str] type for chained method call result"),
    }

    // Check that len() function works correctly
    let length_result_symbol = symbol_table
        .lookup_symbol("length_result")
        .expect("length_result should be in symbol table");
    assert_eq!(
        length_result_symbol.symbol_type,
        SemanticType::Builtin(BuiltinType::Int)
    );
}

#[test]
fn test_method_call_argument_validation() {
    let code = r#"
text: str = "hello"
# This should work - upper() takes no arguments
result1 = text.upper()

# This should fail - upper() doesn't take arguments
result2 = text.upper("invalid_arg")
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Tokenization failed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing failed");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    // Should fail due to invalid method argument
    assert!(
        result.is_err(),
        "Analysis should fail for method with wrong argument count"
    );
    let error_msg = format!("{:?}", result.err().unwrap());
    assert!(
        error_msg.contains("takes exactly") && error_msg.contains("arguments"),
        "Error message should contain argument count information"
    );
}

#[test]
fn test_attribute_vs_method_call_distinction() {
    let code = r#"
s: str = "test"
# Attribute access - returns function
method_reference = s.upper
# Method call - returns result
method_result = s.upper()
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

    // Method reference should be function type
    let method_ref = symbol_table.lookup_symbol("method_reference").unwrap();
    assert!(matches!(
        method_ref.symbol_type,
        SemanticType::Function { .. }
    ));

    // Method result should be string type
    let method_result = symbol_table.lookup_symbol("method_result").unwrap();
    assert_eq!(
        method_result.symbol_type,
        SemanticType::Builtin(BuiltinType::Str)
    );
}
