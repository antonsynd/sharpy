use sharpy_compiler_toolchain::*;

#[test]
fn test_chained_function_calls() {
    // Test func()() - calling the result of func()
    let code = "result = func()()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(outer_call) => {
                    // The outer call should have no arguments
                    assert_eq!(outer_call.positional_args.len(), 0);
                    assert_eq!(outer_call.keyword_args.len(), 0);

                    // The function being called should be func()
                    match &*outer_call.function {
                        Node::Call(inner_call) => {
                            // Inner call should also have no arguments
                            assert_eq!(inner_call.positional_args.len(), 0);
                            assert_eq!(inner_call.keyword_args.len(), 0);

                            // The function of the inner call should be 'func'
                            match &*inner_call.function {
                                Node::Name(name) => {
                                    assert_eq!(name.id, "func");
                                }
                                _ => panic!("Expected function name 'func'"),
                            }
                        }
                        _ => panic!("Expected inner function call"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_chained_function_calls_with_args() {
    // Test get_func(x)(y, z) - calling the result of get_func(x) with args y, z
    let code = "result = get_func(x)(y, z)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(outer_call) => {
                    // The outer call should have 2 arguments: y, z
                    assert_eq!(outer_call.positional_args.len(), 2);

                    match (
                        &outer_call.positional_args[0],
                        &outer_call.positional_args[1],
                    ) {
                        (Node::Name(name1), Node::Name(name2)) => {
                            assert_eq!(name1.id, "y");
                            assert_eq!(name2.id, "z");
                        }
                        _ => panic!("Expected y and z as arguments"),
                    }

                    // The function being called should be get_func(x)
                    match &*outer_call.function {
                        Node::Call(inner_call) => {
                            // Inner call should have 1 argument: x
                            assert_eq!(inner_call.positional_args.len(), 1);

                            match &inner_call.positional_args[0] {
                                Node::Name(name) => assert_eq!(name.id, "x"),
                                _ => panic!("Expected x as argument"),
                            }

                            // The function of the inner call should be 'get_func'
                            match &*inner_call.function {
                                Node::Name(name) => {
                                    assert_eq!(name.id, "get_func");
                                }
                                _ => panic!("Expected function name 'get_func'"),
                            }
                        }
                        _ => panic!("Expected inner function call"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_triple_chained_function_calls() {
    // Test func()()() - three levels of chaining
    let code = "result = func()()()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(call3) => {
                    // Third call should have no arguments
                    assert_eq!(call3.positional_args.len(), 0);

                    // Function should be func()()
                    match &*call3.function {
                        Node::Call(call2) => {
                            // Second call should have no arguments
                            assert_eq!(call2.positional_args.len(), 0);

                            // Function should be func()
                            match &*call2.function {
                                Node::Call(call1) => {
                                    // First call should have no arguments
                                    assert_eq!(call1.positional_args.len(), 0);

                                    // Function should be 'func'
                                    match &*call1.function {
                                        Node::Name(name) => {
                                            assert_eq!(name.id, "func");
                                        }
                                        _ => panic!("Expected function name 'func'"),
                                    }
                                }
                                _ => panic!("Expected first function call"),
                            }
                        }
                        _ => panic!("Expected second function call"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_chained_calls_with_expressions() {
    // Test (func1() + func2())(x * y) - calling result of expression with args
    let code = "result = (func1() + func2())(x * y)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(call) => {
                    // Call should have one argument: x * y
                    assert_eq!(call.positional_args.len(), 1);

                    match &call.positional_args[0] {
                        Node::BinaryOp(bin_op) => {
                            assert_eq!(bin_op.op, BinaryOp::Mult);
                        }
                        _ => panic!("Expected multiplication as argument"),
                    }

                    // Function should be (func1() + func2())
                    match &*call.function {
                        Node::BinaryOp(bin_op) => {
                            assert_eq!(bin_op.op, BinaryOp::Add);

                            // Both sides should be function calls
                            match (&*bin_op.left, &*bin_op.right) {
                                (Node::Call(call1), Node::Call(call2)) => {
                                    match (&*call1.function, &*call2.function) {
                                        (Node::Name(name1), Node::Name(name2)) => {
                                            assert_eq!(name1.id, "func1");
                                            assert_eq!(name2.id, "func2");
                                        }
                                        _ => panic!("Expected function names"),
                                    }
                                }
                                _ => panic!("Expected function calls in addition"),
                            }
                        }
                        _ => panic!("Expected addition as function"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_method_style_chaining() {
    // Test what looks like method chaining: obj.method().another()
    // Note: This will fail until attribute access is implemented
    // For now, let's test a similar pattern with function calls
    let code = "result = get_obj()(get_method())()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(final_call) => {
                    // Final call should have no arguments
                    assert_eq!(final_call.positional_args.len(), 0);

                    // Function should be get_obj()(get_method())
                    match &*final_call.function {
                        Node::Call(middle_call) => {
                            // Middle call should have one argument: get_method()
                            assert_eq!(middle_call.positional_args.len(), 1);

                            match &middle_call.positional_args[0] {
                                Node::Call(inner_call) => match &*inner_call.function {
                                    Node::Name(name) => assert_eq!(name.id, "get_method"),
                                    _ => panic!("Expected get_method"),
                                },
                                _ => panic!("Expected get_method() call"),
                            }

                            // Function should be get_obj()
                            match &*middle_call.function {
                                Node::Call(first_call) => match &*first_call.function {
                                    Node::Name(name) => assert_eq!(name.id, "get_obj"),
                                    _ => panic!("Expected get_obj"),
                                },
                                _ => panic!("Expected get_obj() call"),
                            }
                        }
                        _ => panic!("Expected middle call"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}

#[test]
fn test_curried_function_calls() {
    // Test curried function pattern: add(5)(3)
    let code = "result = add(5)(3)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Assign(assign) => {
            match &*assign.value {
                Node::Call(outer_call) => {
                    // Outer call should have one argument: 3
                    assert_eq!(outer_call.positional_args.len(), 1);

                    match &outer_call.positional_args[0] {
                        Node::Constant(constant) => match &constant.value {
                            ConstantValue::Int(val) => assert_eq!(*val, 3),
                            _ => panic!("Expected integer 3"),
                        },
                        _ => panic!("Expected constant 3"),
                    }

                    // Function should be add(5)
                    match &*outer_call.function {
                        Node::Call(inner_call) => {
                            // Inner call should have one argument: 5
                            assert_eq!(inner_call.positional_args.len(), 1);

                            match &inner_call.positional_args[0] {
                                Node::Constant(constant) => match &constant.value {
                                    ConstantValue::Int(val) => assert_eq!(*val, 5),
                                    _ => panic!("Expected integer 5"),
                                },
                                _ => panic!("Expected constant 5"),
                            }

                            // Function should be 'add'
                            match &*inner_call.function {
                                Node::Name(name) => {
                                    assert_eq!(name.id, "add");
                                }
                                _ => panic!("Expected function name 'add'"),
                            }
                        }
                        _ => panic!("Expected inner call"),
                    }
                }
                _ => panic!("Expected Call, got {:?}", assign.value),
            }
        }
        _ => panic!("Expected Assign, got {:?}", nodes[0]),
    }
}
