use sharpy_compiler_toolchain::semantic::{BuiltinType, SemanticType};
use sharpy_compiler_toolchain::{Parser, SemanticAnalyzer, SharpyLexer};

#[test]
fn test_list_literal_type_inference() {
    // Test homogeneous list
    let source = "numbers = [1, 2, 3, 4]";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "List literal analysis should succeed: {:?}",
        result
    );

    // Check that the variable has the correct type
    let symbol_table = analyzer.get_symbol_table();
    if let Some(numbers_symbol) = symbol_table.lookup_symbol("numbers") {
        match &numbers_symbol.symbol_type {
            SemanticType::Generic { base, args } => {
                assert_eq!(**base, SemanticType::Builtin(BuiltinType::List));
                assert_eq!(args.len(), 1);
                assert_eq!(args[0], SemanticType::Builtin(BuiltinType::Int));
            }
            _ => panic!(
                "Expected generic List[int] type, got: {:?}",
                numbers_symbol.symbol_type
            ),
        }
    } else {
        panic!("Variable 'numbers' not found in symbol table");
    }
}

#[test]
fn test_mixed_numeric_list() {
    // Test mixed int/float list - should infer as List[float]
    let source = "mixed = [1, 2.5, 3]";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Mixed numeric list should succeed: {:?}",
        result
    );

    let symbol_table = analyzer.get_symbol_table();
    if let Some(mixed_symbol) = symbol_table.lookup_symbol("mixed") {
        match &mixed_symbol.symbol_type {
            SemanticType::Generic { base, args } => {
                assert_eq!(**base, SemanticType::Builtin(BuiltinType::List));
                assert_eq!(args[0], SemanticType::Builtin(BuiltinType::Float));
            }
            _ => panic!(
                "Expected List[float] type, got: {:?}",
                mixed_symbol.symbol_type
            ),
        }
    }
}

#[test]
fn test_dict_literal_type_inference() {
    let source = r#"scores = {"alice": 95, "bob": 87}"#;
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Dict literal analysis should succeed: {:?}",
        result
    );

    let symbol_table = analyzer.get_symbol_table();
    if let Some(scores_symbol) = symbol_table.lookup_symbol("scores") {
        match &scores_symbol.symbol_type {
            SemanticType::Generic { base, args } => {
                assert_eq!(**base, SemanticType::Builtin(BuiltinType::Dict));
                assert_eq!(args.len(), 2);
                assert_eq!(args[0], SemanticType::Builtin(BuiltinType::Str)); // keys
                assert_eq!(args[1], SemanticType::Builtin(BuiltinType::Int)); // values
            }
            _ => panic!(
                "Expected Dict[str, int] type, got: {:?}",
                scores_symbol.symbol_type
            ),
        }
    }
}

#[test]
fn test_set_literal_type_inference() {
    let source = r#"unique_names = {"alice", "bob", "charlie"}"#;
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Set literal analysis should succeed: {:?}",
        result
    );

    let symbol_table = analyzer.get_symbol_table();
    if let Some(set_symbol) = symbol_table.lookup_symbol("unique_names") {
        match &set_symbol.symbol_type {
            SemanticType::Generic { base, args } => {
                assert_eq!(**base, SemanticType::Builtin(BuiltinType::Set));
                assert_eq!(args.len(), 1);
                assert_eq!(args[0], SemanticType::Builtin(BuiltinType::Str));
            }
            _ => panic!("Expected Set[str] type, got: {:?}", set_symbol.symbol_type),
        }
    }
}

#[test]
fn test_list_subscript_access() {
    let source = r#"
numbers = [1, 2, 3]
first = numbers[0]
"#;
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "List subscript should succeed: {:?}",
        result
    );

    let symbol_table = analyzer.get_symbol_table();
    if let Some(first_symbol) = symbol_table.lookup_symbol("first") {
        assert_eq!(
            first_symbol.symbol_type,
            SemanticType::Builtin(BuiltinType::Int)
        );
    } else {
        panic!("Variable 'first' not found");
    }
}

#[test]
fn test_dict_subscript_access() {
    let source = r#"
scores = {"alice": 95}
alice_score = scores["alice"]
"#;
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Dict subscript should succeed: {:?}",
        result
    );

    let symbol_table = analyzer.get_symbol_table();
    if let Some(score_symbol) = symbol_table.lookup_symbol("alice_score") {
        assert_eq!(
            score_symbol.symbol_type,
            SemanticType::Builtin(BuiltinType::Int)
        );
    } else {
        panic!("Variable 'alice_score' not found");
    }
}

#[test]
fn test_basic_typed_assignment() {
    // Test basic typed assignment first
    let source = "count: int = 42";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Basic typed assignment should succeed: {:?}",
        result
    );
}

#[test]
fn test_typed_assignment_validation() {
    // Test valid typed assignment with generic type
    let source = "numbers: List[int] = [1, 2, 3]";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Valid typed assignment should succeed: {:?}",
        result
    );
}

#[test]
fn test_typed_assignment_type_mismatch() {
    // Test invalid typed assignment - should fail
    let source = r#"numbers: List[int] = ["hello", "world"]"#;
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(result.is_err(), "Type mismatch should fail");
    let error_msg = result.unwrap_err();
    assert!(error_msg.iter().any(|err| err.contains("Cannot assign")));
}

#[test]
fn test_empty_collections() {
    let source = r#"
empty_list = []
empty_dict = {}
"#;
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Empty collections should succeed: {:?}",
        result
    );

    let symbol_table = analyzer.get_symbol_table();

    // Check empty list type
    if let Some(list_symbol) = symbol_table.lookup_symbol("empty_list") {
        match &list_symbol.symbol_type {
            SemanticType::Generic { base, args } => {
                assert_eq!(**base, SemanticType::Builtin(BuiltinType::List));
                assert_eq!(args[0], SemanticType::Builtin(BuiltinType::Object)); // Default for empty
            }
            _ => panic!("Expected generic List type"),
        }
    }

    // Check empty dict type
    if let Some(dict_symbol) = symbol_table.lookup_symbol("empty_dict") {
        match &dict_symbol.symbol_type {
            SemanticType::Generic { base, args } => {
                assert_eq!(**base, SemanticType::Builtin(BuiltinType::Dict));
                assert_eq!(args.len(), 2);
                assert_eq!(args[0], SemanticType::Builtin(BuiltinType::Object)); // Default key type
                assert_eq!(args[1], SemanticType::Builtin(BuiltinType::Object)); // Default value type
            }
            _ => panic!("Expected generic Dict type"),
        }
    } else {
        panic!("Variable 'empty_dict' not found");
    }
}

#[test]
fn test_numeric_type_promotion() {
    // Test that int can be assigned to float type
    let source = "pi: float = 3";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Int to float assignment should succeed: {:?}",
        result
    );

    let symbol_table = analyzer.get_symbol_table();
    if let Some(pi_symbol) = symbol_table.lookup_symbol("pi") {
        assert_eq!(
            pi_symbol.symbol_type,
            SemanticType::Builtin(BuiltinType::Float)
        );
    }
}

#[test]
fn test_set_subscript_error() {
    // Sets don't support subscript access - should error
    let source = r#"
names = {"alice", "bob"}
first_name = names[0]
"#;
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(result.is_err(), "Set subscript should fail");
    let errors = result.unwrap_err();
    assert!(
        errors
            .iter()
            .any(|err| err.contains("Set objects do not support item access"))
    );
}
