use sharpy_compiler_toolchain::ast::node::Node;
use sharpy_compiler_toolchain::lexer::SharpyLexer;
use sharpy_compiler_toolchain::parser::Parser;

#[test]
fn test_simple_attribute_access() {
    let input = "obj.attr";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Attribute(attr) = result {
        assert_eq!(attr.attr, "attr");
        if let Node::Name(name) = &*attr.value {
            assert_eq!(name.id, "obj");
        } else {
            panic!("Expected Name node for value");
        }
    } else {
        panic!("Expected Attribute node, got {:?}", result);
    }
}

#[test]
fn test_chained_attribute_access() {
    let input = "obj.module.function";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Attribute(outer_attr) = result {
        assert_eq!(outer_attr.attr, "function");

        if let Node::Attribute(inner_attr) = &*outer_attr.value {
            assert_eq!(inner_attr.attr, "module");

            if let Node::Name(name) = &*inner_attr.value {
                assert_eq!(name.id, "obj");
            } else {
                panic!("Expected Name node for inner value");
            }
        } else {
            panic!("Expected Attribute node for outer value");
        }
    } else {
        panic!("Expected Attribute node, got {:?}", result);
    }
}

#[test]
fn test_attribute_access_with_function_call() {
    let input = "obj.method()";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Call(call) = result {
        if let Node::Attribute(attr) = &*call.function {
            assert_eq!(attr.attr, "method");
            if let Node::Name(name) = &*attr.value {
                assert_eq!(name.id, "obj");
            } else {
                panic!("Expected Name node for attribute value");
            }
        } else {
            panic!("Expected Attribute node for function");
        }
        assert!(call.positional_args.is_empty());
        assert!(call.keyword_args.is_empty());
    } else {
        panic!("Expected Call node, got {:?}", result);
    }
}

#[test]
fn test_complex_attribute_chain() {
    let input = "api.client.get_data().result.value";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    // This should parse as: ((((api.client).get_data()).result).value)
    if let Node::Attribute(final_attr) = result {
        assert_eq!(final_attr.attr, "value");

        if let Node::Attribute(result_attr) = &*final_attr.value {
            assert_eq!(result_attr.attr, "result");

            if let Node::Call(call) = &*result_attr.value {
                if let Node::Attribute(method_attr) = &*call.function {
                    assert_eq!(method_attr.attr, "get_data");

                    if let Node::Attribute(client_attr) = &*method_attr.value {
                        assert_eq!(client_attr.attr, "client");

                        if let Node::Name(name) = &*client_attr.value {
                            assert_eq!(name.id, "api");
                        } else {
                            panic!("Expected Name node for api");
                        }
                    } else {
                        panic!("Expected Attribute node for client");
                    }
                } else {
                    panic!("Expected Attribute node for method");
                }
            } else {
                panic!("Expected Call node for get_data()");
            }
        } else {
            panic!("Expected Attribute node for result");
        }
    } else {
        panic!("Expected Attribute node, got {:?}", result);
    }
}
