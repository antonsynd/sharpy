use sharpy_compiler_toolchain::*;

#[test]
fn test_function_calls_with_binary_operations() {
    // Test function calls combined with arithmetic
    let code = "result = func(a + b) * other_func(x, y)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(bin_op) => {
                    assert_eq!(bin_op.op, BinaryOp::Mult);

                    // Left side should be func(a + b)
                    match &*bin_op.left {
                        Node::Call(call) => {
                            match &*call.function {
                                Node::Name(name) => assert_eq!(name.id, "func"),
                                _ => panic!("Expected function name 'func'"),
                            }
                            // Argument should be a + b
                            assert_eq!(call.positional_args.len(), 1);
                            match &call.positional_args[0] {
                                Node::BinaryOp(inner_op) => {
                                    assert_eq!(inner_op.op, BinaryOp::Add);
                                }
                                _ => panic!("Expected addition in function argument"),
                            }
                        }
                        _ => panic!("Expected function call on left side"),
                    }

                    // Right side should be other_func(x, y)
                    match &*bin_op.right {
                        Node::Call(call) => {
                            match &*call.function {
                                Node::Name(name) => assert_eq!(name.id, "other_func"),
                                _ => panic!("Expected function name 'other_func'"),
                            }
                            assert_eq!(call.positional_args.len(), 2);
                        }
                        _ => panic!("Expected function call on right side"),
                    }
                }
                _ => panic!("Expected BinaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_function_calls_with_comparisons() {
    // Test function calls in comparisons
    let code = "result = func(x) < other_func(y)";
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

                    // Left side should be func(x)
                    match &*compare.left {
                        Node::Call(call) => match &*call.function {
                            Node::Name(name) => assert_eq!(name.id, "func"),
                            _ => panic!("Expected function name 'func'"),
                        },
                        _ => panic!("Expected function call on left side"),
                    }

                    // Right side should be other_func(y)
                    assert_eq!(compare.comparators.len(), 1);
                    match &compare.comparators[0] {
                        Node::Call(call) => match &*call.function {
                            Node::Name(name) => assert_eq!(name.id, "other_func"),
                            _ => panic!("Expected function name 'other_func'"),
                        },
                        _ => panic!("Expected function call on right side"),
                    }
                }
                _ => panic!("Expected Compare, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_function_calls_with_unary_operations() {
    // Test function calls with unary operations
    // Note: -func(x) is parsed as (-func)(x), not -(func(x))
    // This is correct precedence (unary has higher precedence than function call)
    let code = "result = (-func)(x)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(call) => {
                    // Function should be (-func)
                    match &*call.function {
                        Node::UnaryOp(unary_op) => {
                            assert_eq!(unary_op.op, UnaryOp::UnarySub);

                            // Operand should be func
                            match &*unary_op.operand {
                                Node::Name(name) => assert_eq!(name.id, "func"),
                                _ => panic!("Expected function name 'func'"),
                            }
                        }
                        _ => panic!("Expected unary operation as function"),
                    }

                    // Should have one argument 'x'
                    assert_eq!(call.positional_args.len(), 1);
                    match &call.positional_args[0] {
                        Node::Name(name) => assert_eq!(name.id, "x"),
                        _ => panic!("Expected argument 'x'"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }

    // Test the other way: -(func(x)) should be parsed as negation of function call
    let code2 = "result = -(func(x))";
    let mut lexer2 = SharpyLexer::new(code2);
    let tokens2 = lexer2.tokenize_all().expect("Lexing should succeed");

    let mut parser2 = Parser::new(tokens2);
    let nodes2 = parser2.parse().expect("Parsing should succeed");

    assert_eq!(nodes2.len(), 1);

    match &nodes2[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::UnaryOp(unary_op) => {
                    assert_eq!(unary_op.op, UnaryOp::UnarySub);

                    // Operand should be func(x)
                    match &*unary_op.operand {
                        Node::Call(call) => match &*call.function {
                            Node::Name(name) => assert_eq!(name.id, "func"),
                            _ => panic!("Expected function name 'func'"),
                        },
                        _ => panic!("Expected function call as operand"),
                    }
                }
                _ => panic!("Expected UnaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes2[0]),
    }
}

#[test]
fn test_function_calls_with_type_annotations() {
    // Test function calls with type annotations
    let code = "result: int = func(x)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            // Check typed target
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

            // Check function call
            match &*assign.value {
                Node::Call(call) => match &*call.function {
                    Node::Name(name) => assert_eq!(name.id, "func"),
                    _ => panic!("Expected function name 'func'"),
                },
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_function_calls_with_lists() {
    // Test function calls with list arguments
    let code = "result = func([1, 2, 3])";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(call) => {
                    assert_eq!(call.positional_args.len(), 1);

                    // Argument should be a list
                    match &call.positional_args[0] {
                        Node::List(list) => {
                            assert_eq!(list.elements.len(), 3);

                            // Check list elements
                            for (i, element) in list.elements.iter().enumerate() {
                                match element {
                                    Node::Constant(constant) => match &constant.value {
                                        ConstantValue::Int(val) => {
                                            assert_eq!(*val, i64::try_from(i + 1).unwrap());
                                        }
                                        _ => panic!("Expected integer constant"),
                                    },
                                    _ => panic!("Expected constant in list"),
                                }
                            }
                        }
                        _ => panic!("Expected list argument"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_complex_function_call_expression() {
    // Test a complex expression with multiple function calls and operations
    let code = "result = func1(a + b) + func2(x) * func3(y, z=10)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(outer_op) => {
                    // Should be: func1(a + b) + (func2(x) * func3(y, z=10))
                    assert_eq!(outer_op.op, BinaryOp::Add);

                    // Left side should be func1(a + b)
                    match &*outer_op.left {
                        Node::Call(call) => match &*call.function {
                            Node::Name(name) => assert_eq!(name.id, "func1"),
                            _ => panic!("Expected function name 'func1'"),
                        },
                        _ => panic!("Expected function call on left side"),
                    }

                    // Right side should be func2(x) * func3(y, z=10)
                    match &*outer_op.right {
                        Node::BinaryOp(inner_op) => {
                            assert_eq!(inner_op.op, BinaryOp::Mult);

                            // Left side of multiplication should be func2(x)
                            match &*inner_op.left {
                                Node::Call(call) => match &*call.function {
                                    Node::Name(name) => assert_eq!(name.id, "func2"),
                                    _ => panic!("Expected function name 'func2'"),
                                },
                                _ => panic!("Expected func2 call"),
                            }

                            // Right side should be func3(y, z=10)
                            match &*inner_op.right {
                                Node::Call(call) => {
                                    match &*call.function {
                                        Node::Name(name) => assert_eq!(name.id, "func3"),
                                        _ => panic!("Expected function name 'func3'"),
                                    }
                                    // Should have positional and keyword args
                                    assert_eq!(call.positional_args.len(), 1);
                                    assert_eq!(call.keyword_args.len(), 1);
                                }
                                _ => panic!("Expected func3 call"),
                            }
                        }
                        _ => panic!("Expected multiplication on right side"),
                    }
                }
                _ => panic!("Expected BinaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}
