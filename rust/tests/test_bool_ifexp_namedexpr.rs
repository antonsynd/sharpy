use sharpy_compiler_toolchain::*;

#[test]
fn test_boolean_and_operation() {
    let code = "x and y and z";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::BoolOp(bool_op) => {
            assert_eq!(bool_op.op, BoolOp::And);
            assert_eq!(bool_op.values.len(), 3);

            // Check all values are Name nodes
            for (i, value) in bool_op.values.iter().enumerate() {
                match value {
                    Node::Name(name) => {
                        let expected = match i {
                            0 => "x",
                            1 => "y",
                            2 => "z",
                            _ => panic!("Unexpected index"),
                        };
                        assert_eq!(name.id, expected);
                    }
                    _ => panic!("Expected Name node"),
                }
            }
        }
        _ => panic!("Expected BoolOp node"),
    }
}

#[test]
fn test_boolean_or_operation() {
    let code = "a or b or c";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::BoolOp(bool_op) => {
            assert_eq!(bool_op.op, BoolOp::Or);
            assert_eq!(bool_op.values.len(), 3);
        }
        _ => panic!("Expected BoolOp node"),
    }
}

#[test]
fn test_mixed_boolean_operations() {
    let code = "a and b or c";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    // Should parse as: (a and b) or c due to precedence
    match &nodes[0] {
        Node::BoolOp(or_op) => {
            assert_eq!(or_op.op, BoolOp::Or);
            assert_eq!(or_op.values.len(), 2);

            // First value should be an AND operation
            match &or_op.values[0] {
                Node::BoolOp(and_op) => {
                    assert_eq!(and_op.op, BoolOp::And);
                    assert_eq!(and_op.values.len(), 2);
                }
                _ => panic!("Expected nested BoolOp node"),
            }

            // Second value should be a simple name
            match &or_op.values[1] {
                Node::Name(name) => {
                    assert_eq!(name.id, "c");
                }
                _ => panic!("Expected Name node"),
            }
        }
        _ => panic!("Expected BoolOp node"),
    }
}

#[test]
fn test_ternary_expression() {
    let code = "x if condition else y";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::IfExp(if_exp) => {
            // Check body (result if true)
            match if_exp.body.as_ref() {
                Node::Name(name) => assert_eq!(name.id, "x"),
                _ => panic!("Expected Name node for body"),
            }

            // Check test condition
            match if_exp.test.as_ref() {
                Node::Name(name) => assert_eq!(name.id, "condition"),
                _ => panic!("Expected Name node for test"),
            }

            // Check else clause
            match if_exp.else_.as_ref() {
                Node::Name(name) => assert_eq!(name.id, "y"),
                _ => panic!("Expected Name node for else"),
            }
        }
        _ => panic!("Expected IfExp node"),
    }
}

#[test]
fn test_nested_ternary_expression() {
    let code = "a if x else b if y else c";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    // Should parse as: a if x else (b if y else c)
    match &nodes[0] {
        Node::IfExp(if_exp) => {
            // Check body
            match if_exp.body.as_ref() {
                Node::Name(name) => assert_eq!(name.id, "a"),
                _ => panic!("Expected Name node for body"),
            }

            // Check test condition
            match if_exp.test.as_ref() {
                Node::Name(name) => assert_eq!(name.id, "x"),
                _ => panic!("Expected Name node for test"),
            }

            // Check else clause should be another IfExp
            match if_exp.else_.as_ref() {
                Node::IfExp(nested_if) => match nested_if.body.as_ref() {
                    Node::Name(name) => assert_eq!(name.id, "b"),
                    _ => panic!("Expected Name node for nested body"),
                },
                _ => panic!("Expected nested IfExp node for else"),
            }
        }
        _ => panic!("Expected IfExp node"),
    }
}

#[test]
fn test_named_expression() {
    let code = "result := compute()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::NamedExpr(named_expr) => {
            // Check target (variable being assigned)
            match named_expr.target.as_ref() {
                Node::Name(name) => assert_eq!(name.id, "result"),
                _ => panic!("Expected Name node for target"),
            }

            // Check value (expression being assigned)
            match named_expr.value.as_ref() {
                Node::Call(call) => match call.function.as_ref() {
                    Node::Name(name) => assert_eq!(name.id, "compute"),
                    _ => panic!("Expected Name node for function"),
                },
                _ => panic!("Expected Call node for value"),
            }
        }
        _ => panic!("Expected NamedExpr node"),
    }
}

#[test]
fn test_boolean_not_operation() {
    let code = "not x";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::UnaryOp(unary_op) => {
            assert_eq!(unary_op.op, UnaryOp::Not);

            match unary_op.operand.as_ref() {
                Node::Name(name) => assert_eq!(name.id, "x"),
                _ => panic!("Expected Name node for operand"),
            }
        }
        _ => panic!("Expected UnaryOp node"),
    }
}

#[test]
fn test_chained_not_operation() {
    let code = "not not x";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::UnaryOp(outer_not) => {
            assert_eq!(outer_not.op, UnaryOp::Not);

            // Should be nested NOT operations
            match outer_not.operand.as_ref() {
                Node::UnaryOp(inner_not) => {
                    assert_eq!(inner_not.op, UnaryOp::Not);

                    match inner_not.operand.as_ref() {
                        Node::Name(name) => assert_eq!(name.id, "x"),
                        _ => panic!("Expected Name node for inner operand"),
                    }
                }
                _ => panic!("Expected nested UnaryOp node"),
            }
        }
        _ => panic!("Expected UnaryOp node"),
    }
}
