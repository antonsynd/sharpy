use sharpy_compiler_toolchain::ast::node::Node;
use sharpy_compiler_toolchain::lexer::SharpyLexer;
use sharpy_compiler_toolchain::parser::Parser;

#[test]
fn test_empty_dict() {
    let input = "{}";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Dict(dict) = result {
        assert!(dict.keys.is_empty());
        assert!(dict.values.is_empty());
    } else {
        panic!("Expected Dict node, got {:?}", result);
    }
}

#[test]
fn test_simple_dict() {
    let input = r#"{"key": "value"}"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Dict(dict) = result {
        assert_eq!(dict.keys.len(), 1);
        assert_eq!(dict.values.len(), 1);

        if let Some(Node::Constant(key_constant)) = &dict.keys[0] {
            if let sharpy_compiler_toolchain::ast::node::ConstantValue::Str(s) = &key_constant.value
            {
                assert_eq!(s, "key");
            } else {
                panic!("Expected string constant for key");
            }
        } else {
            panic!("Expected Constant node for key");
        }

        if let Node::Constant(value_constant) = &dict.values[0] {
            if let sharpy_compiler_toolchain::ast::node::ConstantValue::Str(s) =
                &value_constant.value
            {
                assert_eq!(s, "value");
            } else {
                panic!("Expected string constant for value");
            }
        } else {
            panic!("Expected Constant node for value");
        }
    } else {
        panic!("Expected Dict node, got {:?}", result);
    }
}

#[test]
fn test_multi_item_dict() {
    let input = r#"{"a": 1, "b": 2, "c": 3}"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Dict(dict) = result {
        assert_eq!(dict.keys.len(), 3);
        assert_eq!(dict.values.len(), 3);

        // Check keys
        let expected_keys = ["a", "b", "c"];
        for (i, expected_key) in expected_keys.iter().enumerate() {
            if let Some(Node::Constant(key_constant)) = &dict.keys[i] {
                if let sharpy_compiler_toolchain::ast::node::ConstantValue::Str(s) =
                    &key_constant.value
                {
                    assert_eq!(s, expected_key);
                } else {
                    panic!("Expected string constant for key {}", i);
                }
            } else {
                panic!("Expected Constant node for key {}", i);
            }
        }

        // Check values
        let expected_values = [1, 2, 3];
        for (i, expected_value) in expected_values.iter().enumerate() {
            if let Node::Constant(value_constant) = &dict.values[i] {
                if let sharpy_compiler_toolchain::ast::node::ConstantValue::Int(n) =
                    &value_constant.value
                {
                    assert_eq!(*n, *expected_value);
                } else {
                    panic!("Expected integer constant for value {}", i);
                }
            } else {
                panic!("Expected Constant node for value {}", i);
            }
        }
    } else {
        panic!("Expected Dict node, got {:?}", result);
    }
}

#[test]
fn test_dict_with_trailing_comma() {
    let input = r#"{"key": "value",}"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Dict(dict) = result {
        assert_eq!(dict.keys.len(), 1);
        assert_eq!(dict.values.len(), 1);
    } else {
        panic!("Expected Dict node, got {:?}", result);
    }
}

#[test]
fn test_simple_set() {
    let input = "{1, 2, 3}";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Set(set) = result {
        assert_eq!(set.elements.len(), 3);

        let expected_values = [1, 2, 3];
        for (i, expected_value) in expected_values.iter().enumerate() {
            if let Node::Constant(constant) = &set.elements[i] {
                if let sharpy_compiler_toolchain::ast::node::ConstantValue::Int(n) = &constant.value
                {
                    assert_eq!(*n, *expected_value);
                } else {
                    panic!("Expected integer constant for element {}", i);
                }
            } else {
                panic!("Expected Constant node for element {}", i);
            }
        }
    } else {
        panic!("Expected Set node, got {:?}", result);
    }
}

#[test]
fn test_set_with_trailing_comma() {
    let input = "{1, 2, 3,}";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Set(set) = result {
        assert_eq!(set.elements.len(), 3);
    } else {
        panic!("Expected Set node, got {:?}", result);
    }
}

#[test]
fn test_set_with_expressions() {
    let input = "{x + 1, y * 2}";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Set(set) = result {
        assert_eq!(set.elements.len(), 2);

        // First element should be x + 1
        if let Node::BinaryOp(binary_op) = &set.elements[0] {
            if let Node::Name(name) = &*binary_op.left {
                assert_eq!(name.id, "x");
            } else {
                panic!("Expected Name node for left operand");
            }
        } else {
            panic!("Expected BinaryOp node for first element");
        }

        // Second element should be y * 2
        if let Node::BinaryOp(binary_op) = &set.elements[1] {
            if let Node::Name(name) = &*binary_op.left {
                assert_eq!(name.id, "y");
            } else {
                panic!("Expected Name node for left operand");
            }
        } else {
            panic!("Expected BinaryOp node for second element");
        }
    } else {
        panic!("Expected Set node, got {:?}", result);
    }
}

#[test]
fn test_dict_with_variable_keys() {
    let input = "{key1: value1, key2: value2}";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Dict(dict) = result {
        assert_eq!(dict.keys.len(), 2);
        assert_eq!(dict.values.len(), 2);

        // Check that keys are Name nodes (variables)
        if let Some(Node::Name(name)) = &dict.keys[0] {
            assert_eq!(name.id, "key1");
        } else {
            panic!("Expected Name node for first key");
        }

        if let Some(Node::Name(name)) = &dict.keys[1] {
            assert_eq!(name.id, "key2");
        } else {
            panic!("Expected Name node for second key");
        }
    } else {
        panic!("Expected Dict node, got {:?}", result);
    }
}
