use sharpy_compiler_toolchain::*;

#[test]
fn test_simple_assignment() {
    let code = "x = 42";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check target is a name
            match &*assign.target {
                Node::Name(name) => {
                    assert_eq!(name.id, "x");
                }
                _ => panic!("Expected Name node for target"),
            }

            // Check value is a constant
            match assign.value.as_ref() {
                Node::Constant(constant) => match &constant.value {
                    ConstantValue::Int(val) => assert_eq!(*val, 42),
                    _ => panic!("Expected integer constant"),
                },
                _ => panic!("Expected Constant node for value"),
            }
        }
        _ => panic!("Expected Assign node"),
    }
}

#[test]
fn test_typed_assignment() {
    let code = "x: int = 42";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check target is a typed name
            match &*assign.target {
                Node::TypedName(typed_name) => {
                    assert_eq!(typed_name.id, "x");
                    match &*typed_name.type_ {
                        Node::Name(type_name) => assert_eq!(type_name.id, "int"),
                        _ => panic!("Expected Name node for type"),
                    }
                }
                _ => panic!("Expected TypedName node for target"),
            }

            // Check value is a constant
            match assign.value.as_ref() {
                Node::Constant(constant) => match &constant.value {
                    ConstantValue::Int(val) => assert_eq!(*val, 42),
                    _ => panic!("Expected integer constant"),
                },
                _ => panic!("Expected Constant node for value"),
            }
        }
        _ => panic!("Expected Assign node"),
    }
}

#[test]
fn test_list_assignment() {
    let code = "x = [1, 2, 3]";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check value is a list
            match assign.value.as_ref() {
                Node::List(list) => {
                    assert_eq!(list.elements.len(), 3);

                    // Check each element
                    for (i, element) in list.elements.iter().enumerate() {
                        match element {
                            Node::Constant(constant) => match &constant.value {
                                ConstantValue::Int(val) => assert_eq!(*val, (i + 1) as i64),
                                _ => panic!("Expected integer constant"),
                            },
                            _ => panic!("Expected Constant node in list"),
                        }
                    }
                }
                _ => panic!("Expected List node for value"),
            }
        }
        _ => panic!("Expected Assign node"),
    }
}

#[test]
fn test_nested_list_assignment() {
    let code = "x = [[1, 2], [3, 4]]";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check value is a list
            match assign.value.as_ref() {
                Node::List(outer_list) => {
                    assert_eq!(outer_list.elements.len(), 2);

                    // Check each inner list
                    for (i, element) in outer_list.elements.iter().enumerate() {
                        match element {
                            Node::List(inner_list) => {
                                assert_eq!(inner_list.elements.len(), 2);

                                for (j, inner_element) in inner_list.elements.iter().enumerate() {
                                    match inner_element {
                                        Node::Constant(constant) => match &constant.value {
                                            ConstantValue::Int(val) => {
                                                let expected = (i * 2 + j + 1) as i64;
                                                assert_eq!(*val, expected);
                                            }
                                            _ => panic!("Expected integer constant"),
                                        },
                                        _ => panic!("Expected Constant node in inner list"),
                                    }
                                }
                            }
                            _ => panic!("Expected List node in outer list"),
                        }
                    }
                }
                _ => panic!("Expected List node for value"),
            }
        }
        _ => panic!("Expected Assign node"),
    }
}

#[test]
fn test_string_assignment() {
    let code = r#"name = "hello""#;
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check value is a string constant
            match assign.value.as_ref() {
                Node::Constant(constant) => match &constant.value {
                    ConstantValue::Str(val) => assert_eq!(val, "hello"),
                    _ => panic!("Expected string constant"),
                },
                _ => panic!("Expected Constant node for value"),
            }
        }
        _ => panic!("Expected Assign node"),
    }
}

#[test]
fn test_boolean_assignment() {
    let code = "flag = True";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check value is a boolean constant
            match assign.value.as_ref() {
                Node::Constant(constant) => match &constant.value {
                    ConstantValue::Bool(val) => assert_eq!(*val, true),
                    _ => panic!("Expected boolean constant"),
                },
                _ => panic!("Expected Constant node for value"),
            }
        }
        _ => panic!("Expected Assign node"),
    }
}

#[test]
fn test_simple_destructuring_assignment() {
    let code = "x, y = (1, 2)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check target is a tuple
            match &*assign.target {
                Node::Tuple(tuple) => {
                    assert_eq!(tuple.elements.len(), 2);

                    // Check first target element
                    match &tuple.elements[0] {
                        Node::Name(name) => assert_eq!(name.id, "x"),
                        _ => panic!("Expected Name node for first target"),
                    }

                    // Check second target element
                    match &tuple.elements[1] {
                        Node::Name(name) => assert_eq!(name.id, "y"),
                        _ => panic!("Expected Name node for second target"),
                    }
                }
                _ => panic!("Expected Tuple node for target"),
            }

            // Check value is a tuple
            match assign.value.as_ref() {
                Node::Tuple(tuple) => {
                    assert_eq!(tuple.elements.len(), 2);

                    // Check first value element
                    match &tuple.elements[0] {
                        Node::Constant(constant) => match &constant.value {
                            ConstantValue::Int(val) => assert_eq!(*val, 1),
                            _ => panic!("Expected integer constant"),
                        },
                        _ => panic!("Expected Constant node for first value"),
                    }

                    // Check second value element
                    match &tuple.elements[1] {
                        Node::Constant(constant) => match &constant.value {
                            ConstantValue::Int(val) => assert_eq!(*val, 2),
                            _ => panic!("Expected integer constant"),
                        },
                        _ => panic!("Expected Constant node for second value"),
                    }
                }
                _ => panic!("Expected Tuple node for value"),
            }
        }
        _ => panic!("Expected Assign node"),
    }
}

#[test]
fn test_typed_destructuring_assignment() {
    let code = "x: int, y: float = (1, 2.5)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check target is a tuple with typed names
            match &*assign.target {
                Node::Tuple(tuple) => {
                    assert_eq!(tuple.elements.len(), 2);

                    // Check first target element (typed)
                    match &tuple.elements[0] {
                        Node::TypedName(typed_name) => {
                            assert_eq!(typed_name.id, "x");
                            match &*typed_name.type_ {
                                Node::Name(type_name) => assert_eq!(type_name.id, "int"),
                                _ => panic!("Expected Name node for type"),
                            }
                        }
                        _ => panic!("Expected TypedName node for first target"),
                    }

                    // Check second target element (typed)
                    match &tuple.elements[1] {
                        Node::TypedName(typed_name) => {
                            assert_eq!(typed_name.id, "y");
                            match &*typed_name.type_ {
                                Node::Name(type_name) => assert_eq!(type_name.id, "float"),
                                _ => panic!("Expected Name node for type"),
                            }
                        }
                        _ => panic!("Expected TypedName node for second target"),
                    }
                }
                _ => panic!("Expected Tuple node for target"),
            }

            // Check value is a tuple
            match assign.value.as_ref() {
                Node::Tuple(tuple) => {
                    assert_eq!(tuple.elements.len(), 2);

                    // Check first value element
                    match &tuple.elements[0] {
                        Node::Constant(constant) => match &constant.value {
                            ConstantValue::Int(val) => assert_eq!(*val, 1),
                            _ => panic!("Expected integer constant"),
                        },
                        _ => panic!("Expected Constant node for first value"),
                    }

                    // Check second value element
                    match &tuple.elements[1] {
                        Node::Constant(constant) => match &constant.value {
                            ConstantValue::Float(val) => assert_eq!(*val, 2.5),
                            _ => panic!("Expected float constant"),
                        },
                        _ => panic!("Expected Constant node for second value"),
                    }
                }
                _ => panic!("Expected Tuple node for value"),
            }
        }
        _ => panic!("Expected Assign node"),
    }
}

#[test]
fn test_mixed_typed_destructuring_assignment() {
    let code = "x: int, y = (1, 2.5)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check target is a tuple with mixed typing
            match &*assign.target {
                Node::Tuple(tuple) => {
                    assert_eq!(tuple.elements.len(), 2);

                    // Check first target element (typed)
                    match &tuple.elements[0] {
                        Node::TypedName(typed_name) => {
                            assert_eq!(typed_name.id, "x");
                            match &*typed_name.type_ {
                                Node::Name(type_name) => assert_eq!(type_name.id, "int"),
                                _ => panic!("Expected Name node for type"),
                            }
                        }
                        _ => panic!("Expected TypedName node for first target"),
                    }

                    // Check second target element (untyped)
                    match &tuple.elements[1] {
                        Node::Name(name) => assert_eq!(name.id, "y"),
                        _ => panic!("Expected Name node for second target"),
                    }
                }
                _ => panic!("Expected Tuple node for target"),
            }
        }
        _ => panic!("Expected Assign node"),
    }
}

#[test]
fn test_destructuring_from_list() {
    let code = "x, y = [1, 2]";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check target is a tuple
            match &*assign.target {
                Node::Tuple(tuple) => {
                    assert_eq!(tuple.elements.len(), 2);
                }
                _ => panic!("Expected Tuple node for target"),
            }

            // Check value is a list
            match assign.value.as_ref() {
                Node::List(list) => {
                    assert_eq!(list.elements.len(), 2);
                }
                _ => panic!("Expected List node for value"),
            }
        }
        _ => panic!("Expected Assign node"),
    }
}
