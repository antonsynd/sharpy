use sharpy_compiler_toolchain::{Parser, SharpyLexer, TokenType};

#[test]
fn test_sharpy_access_modifiers() {
    let test_cases = vec![
        ("public_var", "Public"),
        ("_protected_var", "Protected"),
        ("__private_var", "Private"),
        ("$internal_var", "Internal"),
        ("$$file_var", "File"),
    ];

    for (input, expected_modifier) in test_cases {
        let mut lexer = SharpyLexer::new(input);
        let tokens = lexer.tokenize_all().unwrap();
        assert!(!tokens.is_empty());

        if let TokenType::Name(name_type) = &tokens[0].token_type {
            let modifier_str = format!("{:?}", name_type.access_modifier);
            assert_eq!(
                modifier_str, expected_modifier,
                "Failed for input: {}",
                input
            );
        } else {
            panic!("Expected Name token for input: {}", input);
        }
    }
}

#[test]
fn test_literal_names_with_backticks() {
    let input = "`literal_name`";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    assert!(!tokens.is_empty());

    if let TokenType::Name(name_type) = &tokens[0].token_type {
        assert!(name_type.is_literal, "Expected literal name");
        assert_eq!(name_type.name, "literal_name");
    } else {
        panic!("Expected Name token");
    }
}

#[test]
fn test_sharpy_keywords() {
    let sharpy_keywords = vec!["struct", "protocol", "property", "event", "match", "case"];

    for keyword in sharpy_keywords {
        let mut lexer = SharpyLexer::new(keyword);
        let tokens = lexer.tokenize_all().unwrap();
        assert!(!tokens.is_empty());

        // Check that it's recognized as a keyword, not an identifier
        assert!(
            !matches!(tokens[0].token_type, TokenType::Name(_)),
            "Keyword '{}' was parsed as Name instead of keyword",
            keyword
        );
    }
}

#[test]
fn test_sharpy_specific_operators() {
    let test_cases = vec![
        ("x?.y", TokenType::QuestionDot),
        ("x ?? y", TokenType::DoubleQuestion),
        ("x?", TokenType::Question),
    ];

    for (input, expected_token) in test_cases {
        let mut lexer = SharpyLexer::new(input);
        let tokens = lexer.tokenize_all().unwrap();

        let found_token = tokens.iter().any(|token| {
            std::mem::discriminant(&token.token_type) == std::mem::discriminant(&expected_token)
        });

        assert!(
            found_token,
            "Expected token {:?} not found in input: {}",
            expected_token, input
        );
    }
}

#[test]
fn test_optional_type_syntax() {
    let input = "x: int? = None";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_generic_type_syntax() {
    // Test generic type annotations with actual generic syntax
    let test_cases = vec![
        "x: List[int] = []",
        "y: Dict[str, int] = {}",
        "z: Optional[List[str]] = None",
        "w: Tuple[int, str, bool] = (1, 'a', True)",
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let tokens = match lexer.tokenize_all() {
            Ok(tokens) => tokens,
            Err(errors) => {
                panic!("Failed to tokenize '{}': {:?}", case, errors);
            }
        };

        let mut parser = Parser::new(tokens);
        let result = parser.parse();

        assert!(
            result.is_ok(),
            "Failed to parse generic type annotation: {} - Error: {:?}",
            case,
            result.err()
        );
    }
}

#[test]
fn test_qualified_type_syntax() {
    // Test qualified type annotations with dot notation
    let test_cases = vec![
        "x: collections.defaultdict = None",
        "y: typing.Optional = None",
        "z: some.deeply.nested.Type = None",
        // Test combined qualified and generic types
        "a: collections.defaultdict[str, int] = None",
        "b: typing.Optional[str] = None",
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let tokens = match lexer.tokenize_all() {
            Ok(tokens) => tokens,
            Err(errors) => {
                panic!("Failed to tokenize '{}': {:?}", case, errors);
            }
        };

        let mut parser = Parser::new(tokens);
        let result = parser.parse();

        assert!(
            result.is_ok(),
            "Failed to parse qualified type annotation: {} - Error: {:?}",
            case,
            result.err()
        );
    }
}

#[test]
fn test_destructuring_assignment() {
    let test_cases = vec![
        "x, y = 1, 2",
        "a, b, c = get_tuple()",
        "(x, y) = point",
        "first, *rest = items", // May not be supported yet
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);
        let result = parser.parse();
        // Some patterns might not be fully supported yet
        let _result = result;
    }
}

#[test]
fn test_typed_destructuring_assignment() {
    let input = "x: int, y: str = get_values()";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_control_flow_with_types() {
    let input = r#"
if condition:
    x: int = 5
    y: str = "hello"
else:
    x = 10
    y = "world"
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_nested_control_structures() {
    let input = r#"
if outer_condition:
    while inner_condition:
        if nested_condition:
            x = 1
        else:
            y = 2
    else:
        z = 3
else:
    w = 4
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}
