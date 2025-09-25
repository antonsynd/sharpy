use sharpy_compiler_toolchain::{
    Parser, SharpyLexer,
    ast::node::{ConstantValue, Node},
};

#[test]
fn test_ellipsis_parsing() {
    let input = "...";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Failed to parse");

    assert_eq!(ast.len(), 1);

    match &ast[0] {
        Node::Constant(constant) => {
            assert_eq!(constant.value, ConstantValue::Ellipsis);
        }
        _ => panic!("Expected Constant node with Ellipsis value"),
    }
}

#[test]
fn test_ellipsis_in_protocol_method() {
    let input = r#"
protocol Drawable:
    def draw(self):
        ...
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Failed to parse");

    assert!(!ast.is_empty());
    // The test should pass without parsing errors
}

#[test]
fn test_fstring_parsing_simple() {
    let input = r#"f"Hello world""#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Failed to parse");

    assert_eq!(ast.len(), 1);

    match &ast[0] {
        Node::Constant(constant) => {
            match &constant.value {
                ConstantValue::Str(s) => {
                    // The parser should extract the string content
                    assert!(s.contains("Hello world"));
                }
                _ => panic!("Expected string constant from f-string"),
            }
        }
        _ => panic!("Expected Constant node from f-string"),
    }
}

#[test]
fn test_fstring_parsing_single_quotes() {
    let input = r#"f'Hello world'"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Failed to parse");

    assert_eq!(ast.len(), 1);

    match &ast[0] {
        Node::Constant(constant) => {
            match &constant.value {
                ConstantValue::Str(s) => {
                    // The parser should extract the string content
                    assert!(s.contains("Hello world"));
                }
                _ => panic!("Expected string constant from f-string"),
            }
        }
        _ => panic!("Expected Constant node from f-string"),
    }
}

#[test]
fn test_ellipsis_vs_float_parsing() {
    // Test that ellipsis is correctly parsed vs floats
    let input = "... 3.14 ...";

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Failed to parse");

    assert_eq!(ast.len(), 3);

    // First should be ellipsis
    match &ast[0] {
        Node::Constant(constant) => {
            assert_eq!(constant.value, ConstantValue::Ellipsis);
        }
        _ => panic!("Expected Ellipsis constant"),
    }

    // Second should be float
    match &ast[1] {
        Node::Constant(constant) => match constant.value {
            ConstantValue::Float(f) => {
                assert!((f - 3.14).abs() < f64::EPSILON);
            }
            _ => panic!("Expected Float constant"),
        },
        _ => panic!("Expected Float constant"),
    }

    // Third should be ellipsis again
    match &ast[2] {
        Node::Constant(constant) => {
            assert_eq!(constant.value, ConstantValue::Ellipsis);
        }
        _ => panic!("Expected Ellipsis constant"),
    }
}

#[test]
fn test_ellipsis_in_assignment() {
    let input = "x = ...";

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Failed to parse");

    assert_eq!(ast.len(), 1);
    // Should parse successfully as an assignment with ellipsis value
}
