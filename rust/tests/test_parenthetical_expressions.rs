use sharpy_compiler_toolchain::*;

#[test]
fn test_simple_parenthetical_expression() {
    let code = "result = (x + y) * z";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(mult_op) => {
                    assert_eq!(mult_op.op, BinaryOp::Mult);

                    // Left side should be (x + y) - parsed as addition
                    match &*mult_op.left {
                        Node::BinaryOp(add_op) => {
                            assert_eq!(add_op.op, BinaryOp::Add);

                            match (&*add_op.left, &*add_op.right) {
                                (Node::Name(left_name), Node::Name(right_name)) => {
                                    assert_eq!(left_name.id, "x");
                                    assert_eq!(right_name.id, "y");
                                }
                                _ => panic!("Expected names x and y in addition"),
                            }
                        }
                        _ => panic!("Expected addition on left side"),
                    }

                    // Right side should be z
                    match &*mult_op.right {
                        Node::Name(name) => {
                            assert_eq!(name.id, "z");
                        }
                        _ => panic!("Expected name z on right side"),
                    }
                }
                _ => panic!("Expected BinaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_nested_parenthetical_expressions() {
    let code = "result = ((a + b) * c) + d";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(outer_add) => {
                    assert_eq!(outer_add.op, BinaryOp::Add);

                    // Left side should be ((a + b) * c)
                    match &*outer_add.left {
                        Node::BinaryOp(mult_op) => {
                            assert_eq!(mult_op.op, BinaryOp::Mult);

                            // Left side of multiplication should be (a + b)
                            match &*mult_op.left {
                                Node::BinaryOp(inner_add) => {
                                    assert_eq!(inner_add.op, BinaryOp::Add);
                                }
                                _ => panic!("Expected inner addition"),
                            }

                            // Right side should be c
                            match &*mult_op.right {
                                Node::Name(name) => assert_eq!(name.id, "c"),
                                _ => panic!("Expected name c"),
                            }
                        }
                        _ => panic!("Expected multiplication"),
                    }

                    // Right side should be d
                    match &*outer_add.right {
                        Node::Name(name) => assert_eq!(name.id, "d"),
                        _ => panic!("Expected name d"),
                    }
                }
                _ => panic!("Expected BinaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_parentheses_vs_without() {
    // Test that (x + y) * z is different from x + y * z

    // First: (x + y) * z should be (x + y) * z
    let code1 = "result = (x + y) * z";
    let mut lexer1 = SharpyLexer::new(code1);
    let tokens1 = lexer1.tokenize_all().expect("Lexing should succeed");
    let mut parser1 = Parser::new(tokens1);
    let nodes1 = parser1.parse().expect("Parsing should succeed");

    match &nodes1[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(op) => {
                    // Should be multiplication at the top level
                    assert_eq!(op.op, BinaryOp::Mult);
                    // Left side should be addition
                    match &*op.left {
                        Node::BinaryOp(inner_op) => assert_eq!(inner_op.op, BinaryOp::Add),
                        _ => panic!("Expected addition on left"),
                    }
                }
                _ => panic!("Expected multiplication"),
            }
        }
        _ => panic!("Expected assignment"),
    }

    // Second: x + y * z should be x + (y * z)
    let code2 = "result = x + y * z";
    let mut lexer2 = SharpyLexer::new(code2);
    let tokens2 = lexer2.tokenize_all().expect("Lexing should succeed");
    let mut parser2 = Parser::new(tokens2);
    let nodes2 = parser2.parse().expect("Parsing should succeed");

    match &nodes2[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(op) => {
                    // Should be addition at the top level
                    assert_eq!(op.op, BinaryOp::Add);
                    // Right side should be multiplication
                    match &*op.right {
                        Node::BinaryOp(inner_op) => assert_eq!(inner_op.op, BinaryOp::Mult),
                        _ => panic!("Expected multiplication on right"),
                    }
                }
                _ => panic!("Expected addition"),
            }
        }
        _ => panic!("Expected assignment"),
    }
}

#[test]
fn test_parentheses_with_function_calls() {
    let code = "result = (func(x) + func(y)) * z";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(mult_op) => {
                    assert_eq!(mult_op.op, BinaryOp::Mult);

                    // Left side should be func(x) + func(y)
                    match &*mult_op.left {
                        Node::BinaryOp(add_op) => {
                            assert_eq!(add_op.op, BinaryOp::Add);

                            // Both sides should be function calls
                            match (&*add_op.left, &*add_op.right) {
                                (Node::Call(call1), Node::Call(call2)) => {
                                    match (&*call1.function, &*call2.function) {
                                        (Node::Name(name1), Node::Name(name2)) => {
                                            assert_eq!(name1.id, "func");
                                            assert_eq!(name2.id, "func");
                                        }
                                        _ => panic!("Expected function names"),
                                    }
                                }
                                _ => panic!("Expected function calls"),
                            }
                        }
                        _ => panic!("Expected addition"),
                    }
                }
                _ => panic!("Expected multiplication"),
            }
        }
        _ => panic!("Expected assignment"),
    }
}

#[test]
fn test_parentheses_with_unary_operations() {
    let code = "result = -(x + y)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::UnaryOp(unary_op) => {
                    assert_eq!(unary_op.op, UnaryOp::UnarySub);

                    // Operand should be (x + y) - parsed as addition
                    match &*unary_op.operand {
                        Node::BinaryOp(add_op) => {
                            assert_eq!(add_op.op, BinaryOp::Add);
                        }
                        _ => panic!("Expected addition as operand"),
                    }
                }
                _ => panic!("Expected UnaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_empty_tuple() {
    let code = "result = ()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => match &*assign.value {
            Node::Tuple(tuple) => {
                assert_eq!(tuple.elements.len(), 0);
            }
            _ => panic!("Expected Tuple, got {:?}", assign.value),
        },
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_single_element_tuple() {
    let code = "result = (x,)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => match &*assign.value {
            Node::Tuple(tuple) => {
                assert_eq!(tuple.elements.len(), 1);
                match &tuple.elements[0] {
                    Node::Name(name) => assert_eq!(name.id, "x"),
                    _ => panic!("Expected name x in tuple"),
                }
            }
            _ => panic!("Expected Tuple, got {:?}", assign.value),
        },
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}
