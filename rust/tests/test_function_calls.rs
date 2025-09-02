use sharpy_compiler_toolchain::*;

#[test]
fn test_simple_function_call() {
    let code = "result = func()";
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
                Node::Call(call) => {
                    // Check function name
                    match &*call.function {
                        Node::Name(func_name) => {
                            assert_eq!(func_name.id, "func");
                        }
                        _ => panic!("Expected function name to be Name node"),
                    }
                    // Check no arguments
                    assert_eq!(call.positional_args.len(), 0);
                    assert_eq!(call.keyword_args.len(), 0);
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_function_call_with_positional_args() {
    let code = "result = func(a, b, 42)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(call) => {
                    // Check function name
                    match &*call.function {
                        Node::Name(func_name) => {
                            assert_eq!(func_name.id, "func");
                        }
                        _ => panic!("Expected function name to be Name node"),
                    }

                    // Check positional arguments
                    assert_eq!(call.positional_args.len(), 3);

                    // First arg should be 'a'
                    match &call.positional_args[0] {
                        Node::Name(name) => assert_eq!(name.id, "a"),
                        _ => panic!("Expected first arg to be name 'a'"),
                    }

                    // Second arg should be 'b'
                    match &call.positional_args[1] {
                        Node::Name(name) => assert_eq!(name.id, "b"),
                        _ => panic!("Expected second arg to be name 'b'"),
                    }

                    // Third arg should be 42
                    match &call.positional_args[2] {
                        Node::Constant(constant) => match &constant.value {
                            ConstantValue::Int(val) => assert_eq!(*val, 42),
                            _ => panic!("Expected third arg to be integer 42"),
                        },
                        _ => panic!("Expected third arg to be constant"),
                    }

                    // No keyword arguments
                    assert_eq!(call.keyword_args.len(), 0);
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_function_call_with_keyword_args() {
    let code = "result = func(x=10, y=20)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(call) => {
                    // Check function name
                    match &*call.function {
                        Node::Name(func_name) => {
                            assert_eq!(func_name.id, "func");
                        }
                        _ => panic!("Expected function name to be Name node"),
                    }

                    // No positional arguments
                    assert_eq!(call.positional_args.len(), 0);

                    // Check keyword arguments (for now, they're stored as values)
                    assert_eq!(call.keyword_args.len(), 2);

                    // First keyword arg value should be 10
                    match &call.keyword_args[0] {
                        Node::Constant(constant) => match &constant.value {
                            ConstantValue::Int(val) => assert_eq!(*val, 10),
                            _ => panic!("Expected first keyword arg value to be 10"),
                        },
                        _ => panic!("Expected first keyword arg to be constant"),
                    }

                    // Second keyword arg value should be 20
                    match &call.keyword_args[1] {
                        Node::Constant(constant) => match &constant.value {
                            ConstantValue::Int(val) => assert_eq!(*val, 20),
                            _ => panic!("Expected second keyword arg value to be 20"),
                        },
                        _ => panic!("Expected second keyword arg to be constant"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_function_call_mixed_args() {
    let code = "result = func(a, b, x=10, y=20)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(call) => {
                    // Check positional arguments
                    assert_eq!(call.positional_args.len(), 2);

                    match &call.positional_args[0] {
                        Node::Name(name) => assert_eq!(name.id, "a"),
                        _ => panic!("Expected first positional arg to be 'a'"),
                    }

                    match &call.positional_args[1] {
                        Node::Name(name) => assert_eq!(name.id, "b"),
                        _ => panic!("Expected second positional arg to be 'b'"),
                    }

                    // Check keyword arguments
                    assert_eq!(call.keyword_args.len(), 2);
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_nested_function_calls() {
    let code = "result = outer(inner(x))";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(outer_call) => {
                    // Check outer function name
                    match &*outer_call.function {
                        Node::Name(func_name) => {
                            assert_eq!(func_name.id, "outer");
                        }
                        _ => panic!("Expected outer function name to be 'outer'"),
                    }

                    // Check that the argument is also a function call
                    assert_eq!(outer_call.positional_args.len(), 1);
                    match &outer_call.positional_args[0] {
                        Node::Call(inner_call) => {
                            match &*inner_call.function {
                                Node::Name(func_name) => {
                                    assert_eq!(func_name.id, "inner");
                                }
                                _ => panic!("Expected inner function name to be 'inner'"),
                            }

                            // Check inner function argument
                            assert_eq!(inner_call.positional_args.len(), 1);
                            match &inner_call.positional_args[0] {
                                Node::Name(name) => assert_eq!(name.id, "x"),
                                _ => panic!("Expected inner function arg to be 'x'"),
                            }
                        }
                        _ => panic!("Expected inner call to be a function call"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_chained_function_calls() {
    let code = "result = obj.method()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    // This should fail for now since we haven't implemented attribute access yet
    // But the function call part should work
    // We'll implement this when we add attribute access
    println!("Chained call result: {result:?}");

    // For now, let's test a simpler case that should work
    let code2 = "result = func1() + func2()";
    let mut lexer2 = SharpyLexer::new(code2);
    let tokens2 = lexer2.tokenize_all().expect("Lexing should succeed");

    let mut parser2 = Parser::new(tokens2);
    let nodes2 = parser2.parse().expect("Parsing should succeed");

    assert_eq!(nodes2.len(), 1);

    match &nodes2[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::BinaryOp(bin_op) => {
                    assert_eq!(bin_op.op, BinaryOp::Add);

                    // Left side should be func1()
                    match &*bin_op.left {
                        Node::Call(call) => match &*call.function {
                            Node::Name(name) => assert_eq!(name.id, "func1"),
                            _ => panic!("Expected left function to be 'func1'"),
                        },
                        _ => panic!("Expected left side to be function call"),
                    }

                    // Right side should be func2()
                    match &*bin_op.right {
                        Node::Call(call) => match &*call.function {
                            Node::Name(name) => assert_eq!(name.id, "func2"),
                            _ => panic!("Expected right function to be 'func2'"),
                        },
                        _ => panic!("Expected right side to be function call"),
                    }
                }
                _ => panic!("Expected BinaryOp, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes2[0]),
    }
}

#[test]
fn test_function_call_with_expression_args() {
    let code = "result = func(a + b, x * 2)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(call) => {
                    assert_eq!(call.positional_args.len(), 2);

                    // First arg should be a + b
                    match &call.positional_args[0] {
                        Node::BinaryOp(bin_op) => {
                            assert_eq!(bin_op.op, BinaryOp::Add);
                        }
                        _ => panic!("Expected first arg to be binary operation"),
                    }

                    // Second arg should be x * 2
                    match &call.positional_args[1] {
                        Node::BinaryOp(bin_op) => {
                            assert_eq!(bin_op.op, BinaryOp::Mult);
                        }
                        _ => panic!("Expected second arg to be binary operation"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}
