use sharpy_compiler_toolchain::*;

#[test]
fn test_binary_operations_with_types() {
    // Test that binary operations work with typed variables
    let code = "result: int = x + y * 2";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check that target is typed
            match &*assign.target {
                Node::TypedName(typed_name) => {
                    assert_eq!(typed_name.id, "result");
                    match &*typed_name.type_ {
                        Node::TypeName(type_name) => {
                            assert_eq!(type_name.name, "int");
                        }
                        _ => panic!("Expected TypeName"),
                    }
                }
                _ => panic!("Expected TypedName"),
            }

            // Check that value is a binary operation
            match &*assign.value {
                Node::BinaryOp(bin_op) => {
                    assert_eq!(bin_op.op, BinaryOp::Add);

                    // Right side should be multiplication
                    match &*bin_op.right {
                        Node::BinaryOp(mult_op) => {
                            assert_eq!(mult_op.op, BinaryOp::Mult);
                        }
                        _ => panic!("Expected multiplication on right side"),
                    }
                }
                _ => panic!("Expected BinaryOp"),
            }
        }
        _ => panic!("Expected Assign"),
    }
}

#[test]
fn test_complex_arithmetic_expression() {
    // Test a complex expression: a ** 2 + b * c - d / e
    let code = "result = a ** 2 + b * c - d / e";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(outer_op) => {
                    // Should be: ((a ** 2) + (b * c)) - (d / e)
                    assert_eq!(outer_op.op, BinaryOp::Sub);

                    // Left side should be addition
                    match &*outer_op.left {
                        Node::BinaryOp(add_op) => {
                            assert_eq!(add_op.op, BinaryOp::Add);

                            // Left side of addition should be power
                            match &*add_op.left {
                                Node::BinaryOp(pow_op) => {
                                    assert_eq!(pow_op.op, BinaryOp::Pow);
                                }
                                _ => panic!("Expected power operation"),
                            }

                            // Right side of addition should be multiplication
                            match &*add_op.right {
                                Node::BinaryOp(mult_op) => {
                                    assert_eq!(mult_op.op, BinaryOp::Mult);
                                }
                                _ => panic!("Expected multiplication"),
                            }
                        }
                        _ => panic!("Expected addition"),
                    }

                    // Right side should be division
                    match &*outer_op.right {
                        Node::BinaryOp(div_op) => {
                            assert_eq!(div_op.op, BinaryOp::Div);
                        }
                        _ => panic!("Expected division"),
                    }
                }
                _ => panic!("Expected BinaryOp"),
            }
        }
        _ => panic!("Expected Assign"),
    }
}

#[test]
fn test_bitwise_precedence() {
    // Test bitwise operator precedence: a | b ^ c & d
    // Should parse as: a | (b ^ (c & d))
    let code = "result = a | b ^ c & d";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(or_op) => {
                    assert_eq!(or_op.op, BinaryOp::BitwiseOr);

                    // Right side should be XOR
                    match &*or_op.right {
                        Node::BinaryOp(xor_op) => {
                            assert_eq!(xor_op.op, BinaryOp::BitwiseXor);

                            // Right side of XOR should be AND
                            match &*xor_op.right {
                                Node::BinaryOp(and_op) => {
                                    assert_eq!(and_op.op, BinaryOp::BitwiseAnd);
                                }
                                _ => panic!("Expected bitwise AND"),
                            }
                        }
                        _ => panic!("Expected bitwise XOR"),
                    }
                }
                _ => panic!("Expected bitwise OR"),
            }
        }
        _ => panic!("Expected Assign"),
    }
}

#[test]
fn test_shift_operations() {
    let test_cases = vec![
        ("result = a << 2", BinaryOp::LShift),
        ("result = b >> 1", BinaryOp::RShift),
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
fn test_unary_with_binary() {
    // Test unary operations combined with binary: -a + +b
    let code = "result = -a + +b";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(add_op) => {
                    assert_eq!(add_op.op, BinaryOp::Add);

                    // Left side should be unary minus
                    match &*add_op.left {
                        Node::UnaryOp(unary_op) => {
                            assert_eq!(unary_op.op, UnaryOp::UnarySub);
                        }
                        _ => panic!("Expected unary minus"),
                    }

                    // Right side should be unary plus
                    match &*add_op.right {
                        Node::UnaryOp(unary_op) => {
                            assert_eq!(unary_op.op, UnaryOp::UnaryAdd);
                        }
                        _ => panic!("Expected unary plus"),
                    }
                }
                _ => panic!("Expected BinaryOp"),
            }
        }
        _ => panic!("Expected Assign"),
    }
}

#[test]
fn test_comparison_with_arithmetic() {
    // Test that comparison binds looser than arithmetic: a + b < c * d
    let code = "result = a + b < c * d";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Compare(compare) => {
                    assert_eq!(compare.ops.len(), 1);
                    assert_eq!(compare.ops[0], CompOp::Lt);

                    // Left side should be addition
                    match &*compare.left {
                        Node::BinaryOp(add_op) => {
                            assert_eq!(add_op.op, BinaryOp::Add);
                        }
                        _ => panic!("Expected addition on left side"),
                    }

                    // Right side should be multiplication
                    match &compare.comparators[0] {
                        Node::BinaryOp(mult_op) => {
                            assert_eq!(mult_op.op, BinaryOp::Mult);
                        }
                        _ => panic!("Expected multiplication on right side"),
                    }
                }
                _ => panic!("Expected Compare"),
            }
        }
        _ => panic!("Expected Assign"),
    }
}
