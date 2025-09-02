use sharpy_compiler_toolchain::*;

#[test]
fn test_complex_parenthetical_with_precedence() {
    // Test a complex expression that demonstrates correct precedence and parentheses
    let code = "result = (a + b) ** (c - d) * (e / f)";
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

                    // Left side should be (a + b) ** (c - d)
                    match &*mult_op.left {
                        Node::BinaryOp(pow_op) => {
                            assert_eq!(pow_op.op, BinaryOp::Pow);

                            // Left side of power should be (a + b)
                            match &*pow_op.left {
                                Node::BinaryOp(add_op) => {
                                    assert_eq!(add_op.op, BinaryOp::Add);
                                }
                                _ => panic!("Expected addition in power left"),
                            }

                            // Right side of power should be (c - d)
                            match &*pow_op.right {
                                Node::BinaryOp(sub_op) => {
                                    assert_eq!(sub_op.op, BinaryOp::Sub);
                                }
                                _ => panic!("Expected subtraction in power right"),
                            }
                        }
                        _ => panic!("Expected power operation on left"),
                    }

                    // Right side should be (e / f)
                    match &*mult_op.right {
                        Node::BinaryOp(div_op) => {
                            assert_eq!(div_op.op, BinaryOp::Div);
                        }
                        _ => panic!("Expected division on right"),
                    }
                }
                _ => panic!("Expected BinaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_parentheses_with_everything() {
    // Test parentheses with function calls, binary ops, unary ops, etc.
    let code = "result = -(func(a + b) * (x ** 2)) + (y / z)";
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

                    // Left side should be -(func(a + b) * (x ** 2))
                    match &*add_op.left {
                        Node::UnaryOp(unary_op) => {
                            assert_eq!(unary_op.op, UnaryOp::UnarySub);

                            // Operand should be func(a + b) * (x ** 2)
                            match &*unary_op.operand {
                                Node::BinaryOp(mult_op) => {
                                    assert_eq!(mult_op.op, BinaryOp::Mult);

                                    // Left side should be func(a + b)
                                    match &*mult_op.left {
                                        Node::Call(call) => {
                                            match &*call.function {
                                                Node::Name(name) => assert_eq!(name.id, "func"),
                                                _ => panic!("Expected function name"),
                                            }
                                            // Function arg should be (a + b)
                                            assert_eq!(call.positional_args.len(), 1);
                                            match &call.positional_args[0] {
                                                Node::BinaryOp(inner_add) => {
                                                    assert_eq!(inner_add.op, BinaryOp::Add);
                                                }
                                                _ => panic!("Expected addition in function arg"),
                                            }
                                        }
                                        _ => panic!("Expected function call"),
                                    }

                                    // Right side should be (x ** 2)
                                    match &*mult_op.right {
                                        Node::BinaryOp(pow_op) => {
                                            assert_eq!(pow_op.op, BinaryOp::Pow);
                                        }
                                        _ => panic!("Expected power operation"),
                                    }
                                }
                                _ => panic!("Expected multiplication"),
                            }
                        }
                        _ => panic!("Expected unary operation"),
                    }

                    // Right side should be (y / z)
                    match &*add_op.right {
                        Node::BinaryOp(div_op) => {
                            assert_eq!(div_op.op, BinaryOp::Div);
                        }
                        _ => panic!("Expected division"),
                    }
                }
                _ => panic!("Expected addition at top level"),
            }
        }
        _ => panic!("Expected assignment"),
    }
}
