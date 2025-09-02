use sharpy_compiler_toolchain::*;

#[test]
fn test_simple_addition() {
    let code = "result = a + b";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.target {
                Node::Name(name) => {
                    assert_eq!(name.id, "result");
                }
                _ => panic!("Expected Name, got {:?}", assign.target),
            }
            match &*assign.value {
                Node::BinaryOp(bin_op) => {
                    assert_eq!(bin_op.op, BinaryOp::Add);
                    match (&*bin_op.left, &*bin_op.right) {
                        (Node::Name(left_name), Node::Name(right_name)) => {
                            assert_eq!(left_name.id, "a");
                            assert_eq!(right_name.id, "b");
                        }
                        _ => panic!("Expected Name nodes for left and right operands"),
                    }
                }
                _ => panic!("Expected BinaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_multiplication() {
    let code = "result = x * y";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => match &*assign.value {
            Node::BinaryOp(bin_op) => {
                assert_eq!(bin_op.op, BinaryOp::Mult);
            }
            _ => panic!("Expected BinaryOp, got {:?}", assign.value),
        },
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_power() {
    let code = "result = base ** exponent";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => match &*assign.value {
            Node::BinaryOp(bin_op) => {
                assert_eq!(bin_op.op, BinaryOp::Pow);
            }
            _ => panic!("Expected BinaryOp, got {:?}", assign.value),
        },
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_operator_precedence() {
    let code = "result = a + b * c";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(outer_op) => {
                    // Should be: a + (b * c), so outer operation is addition
                    assert_eq!(outer_op.op, BinaryOp::Add);

                    // Left side should be 'a'
                    match &*outer_op.left {
                        Node::Name(name) => assert_eq!(name.id, "a"),
                        _ => panic!("Expected left operand to be name 'a'"),
                    }

                    // Right side should be multiplication (b * c)
                    match &*outer_op.right {
                        Node::BinaryOp(inner_op) => {
                            assert_eq!(inner_op.op, BinaryOp::Mult);
                            match (&*inner_op.left, &*inner_op.right) {
                                (Node::Name(left), Node::Name(right)) => {
                                    assert_eq!(left.id, "b");
                                    assert_eq!(right.id, "c");
                                }
                                _ => panic!("Expected multiplication operands to be names"),
                            }
                        }
                        _ => panic!("Expected right operand to be multiplication"),
                    }
                }
                _ => panic!("Expected BinaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_right_associative_power() {
    let code = "result = a ** b ** c";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(outer_op) => {
                    // Should be: a ** (b ** c), so outer operation has 'a' on left
                    assert_eq!(outer_op.op, BinaryOp::Pow);

                    // Left side should be 'a'
                    match &*outer_op.left {
                        Node::Name(name) => assert_eq!(name.id, "a"),
                        _ => panic!("Expected left operand to be name 'a'"),
                    }

                    // Right side should be power (b ** c)
                    match &*outer_op.right {
                        Node::BinaryOp(inner_op) => {
                            assert_eq!(inner_op.op, BinaryOp::Pow);
                            match (&*inner_op.left, &*inner_op.right) {
                                (Node::Name(left), Node::Name(right)) => {
                                    assert_eq!(left.id, "b");
                                    assert_eq!(right.id, "c");
                                }
                                _ => panic!("Expected power operands to be names"),
                            }
                        }
                        _ => panic!("Expected right operand to be power operation"),
                    }
                }
                _ => panic!("Expected BinaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_bitwise_operations() {
    let test_cases = vec![
        ("result = a | b", BinaryOp::BitwiseOr),
        ("result = a ^ b", BinaryOp::BitwiseXor),
        ("result = a & b", BinaryOp::BitwiseAnd),
        ("result = a << b", BinaryOp::LShift),
        ("result = a >> b", BinaryOp::RShift),
    ];

    for (code, expected_op) in test_cases {
        let mut lexer = SharpyLexer::new(code);
        let tokens = lexer.tokenize_all().expect("Lexing should succeed");

        let mut parser = Parser::new(tokens);
        let nodes = parser.parse().expect("Parsing should succeed");

        assert_eq!(nodes.len(), 1);

        match &nodes[0] {
            Node::Assign(assign) => match &*assign.value {
                Node::BinaryOp(bin_op) => {
                    assert_eq!(bin_op.op, expected_op, "Failed for: {code}");
                }
                _ => panic!("Expected BinaryOp for: {code}"),
            },
            _ => panic!("Expected Assign for: {code}"),
        }
    }
}

#[test]
fn test_mixed_arithmetic_operations() {
    let code = "result = a + b - c * d / e";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    // Should parse as: ((a + b) - ((c * d) / e))
    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(outer_op) => {
                    // Outermost should be subtraction
                    assert_eq!(outer_op.op, BinaryOp::Sub);

                    // Left side should be addition (a + b)
                    match &*outer_op.left {
                        Node::BinaryOp(add_op) => {
                            assert_eq!(add_op.op, BinaryOp::Add);
                        }
                        _ => panic!("Expected left side to be addition"),
                    }

                    // Right side should be division (c * d / e)
                    match &*outer_op.right {
                        Node::BinaryOp(div_op) => {
                            assert_eq!(div_op.op, BinaryOp::Div);
                            // Left side of division should be multiplication
                            match &*div_op.left {
                                Node::BinaryOp(mult_op) => {
                                    assert_eq!(mult_op.op, BinaryOp::Mult);
                                }
                                _ => panic!("Expected multiplication in division left side"),
                            }
                        }
                        _ => panic!("Expected right side to be division"),
                    }
                }
                _ => panic!("Expected BinaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_matrix_multiplication() {
    let code = "result = matrix1 @ matrix2";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => match &*assign.value {
            Node::BinaryOp(bin_op) => {
                assert_eq!(bin_op.op, BinaryOp::MatMult);
            }
            _ => panic!("Expected BinaryOp, got {:?}", assign.value),
        },
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}
