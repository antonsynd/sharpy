use sharpy_compiler_toolchain::ast::node::Node;
use sharpy_compiler_toolchain::lexer::SharpyLexer;
use sharpy_compiler_toolchain::parser::Parser;

#[test]
fn test_simple_subscript() {
    let input = "arr[0]";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Subscript(subscript) = result {
        if let Node::Name(name) = &*subscript.value {
            assert_eq!(name.id, "arr");
        } else {
            panic!("Expected Name node for value");
        }

        if let Node::Constant(constant) = &*subscript.slice {
            if let sharpy_compiler_toolchain::ast::node::ConstantValue::Int(n) = &constant.value {
                assert_eq!(*n, 0);
            } else {
                panic!("Expected integer constant");
            }
        } else {
            panic!("Expected Constant node for slice");
        }
    } else {
        panic!("Expected Subscript node, got {:?}", result);
    }
}

#[test]
fn test_chained_subscript() {
    let input = "matrix[i][j]";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Subscript(outer_subscript) = result {
        // The outer subscript should have slice 'j'
        if let Node::Name(name) = &*outer_subscript.slice {
            assert_eq!(name.id, "j");
        } else {
            panic!("Expected Name node for outer slice");
        }

        // The value should be another subscript with matrix[i]
        if let Node::Subscript(inner_subscript) = &*outer_subscript.value {
            if let Node::Name(name) = &*inner_subscript.value {
                assert_eq!(name.id, "matrix");
            } else {
                panic!("Expected Name node for inner value");
            }

            if let Node::Name(name) = &*inner_subscript.slice {
                assert_eq!(name.id, "i");
            } else {
                panic!("Expected Name node for inner slice");
            }
        } else {
            panic!("Expected Subscript node for inner subscript");
        }
    } else {
        panic!("Expected Subscript node, got {:?}", result);
    }
}

#[test]
fn test_subscript_with_expression() {
    let input = "data[key + 1]";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Subscript(subscript) = result {
        if let Node::Name(name) = &*subscript.value {
            assert_eq!(name.id, "data");
        } else {
            panic!("Expected Name node for value");
        }

        // The slice should be a binary operation: key + 1
        if let Node::BinaryOp(binary_op) = &*subscript.slice {
            if let Node::Name(left) = &*binary_op.left {
                assert_eq!(left.id, "key");
            } else {
                panic!("Expected Name node for left operand");
            }

            if let Node::Constant(right) = &*binary_op.right {
                if let sharpy_compiler_toolchain::ast::node::ConstantValue::Int(n) = &right.value {
                    assert_eq!(*n, 1);
                } else {
                    panic!("Expected integer constant for right operand");
                }
            } else {
                panic!("Expected Constant node for right operand");
            }
        } else {
            panic!("Expected BinaryOp node for slice");
        }
    } else {
        panic!("Expected Subscript node, got {:?}", result);
    }
}

#[test]
fn test_subscript_with_function_call() {
    let input = "get_array()[index]";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Subscript(subscript) = result {
        // The value should be a function call
        if let Node::Call(call) = &*subscript.value {
            if let Node::Name(name) = &*call.function {
                assert_eq!(name.id, "get_array");
            } else {
                panic!("Expected Name node for function");
            }
        } else {
            panic!("Expected Call node for value");
        }

        // The slice should be 'index'
        if let Node::Name(name) = &*subscript.slice {
            assert_eq!(name.id, "index");
        } else {
            panic!("Expected Name node for slice");
        }
    } else {
        panic!("Expected Subscript node, got {:?}", result);
    }
}

#[test]
fn test_mixed_attribute_and_subscript() {
    let input = "obj.data[key].value";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    // This should parse as: (((obj.data)[key]).value)
    if let Node::Attribute(final_attr) = result {
        assert_eq!(final_attr.attr, "value");

        if let Node::Subscript(subscript) = &*final_attr.value {
            if let Node::Name(name) = &*subscript.slice {
                assert_eq!(name.id, "key");
            } else {
                panic!("Expected Name node for slice");
            }

            if let Node::Attribute(attr) = &*subscript.value {
                assert_eq!(attr.attr, "data");

                if let Node::Name(name) = &*attr.value {
                    assert_eq!(name.id, "obj");
                } else {
                    panic!("Expected Name node for object");
                }
            } else {
                panic!("Expected Attribute node for subscript value");
            }
        } else {
            panic!("Expected Subscript node for final attr value");
        }
    } else {
        panic!("Expected Attribute node, got {:?}", result);
    }
}
