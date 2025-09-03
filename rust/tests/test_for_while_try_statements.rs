use sharpy_compiler_toolchain::*;

#[test]
fn test_simple_for_loop() {
    let code = "for x in items:\n    print(x)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::For(for_loop) => {
            // Check target
            match for_loop.target.as_ref() {
                Node::Name(name) => assert_eq!(name.id, "x"),
                _ => panic!("Expected Name node for target"),
            }

            // Check iterator
            match for_loop.iter.as_ref() {
                Node::Name(name) => assert_eq!(name.id, "items"),
                _ => panic!("Expected Name node for iterator"),
            }

            // Check body
            assert_eq!(for_loop.body.len(), 1);
            match &for_loop.body[0] {
                Node::Call(call) => match call.function.as_ref() {
                    Node::Name(name) => assert_eq!(name.id, "print"),
                    _ => panic!("Expected Name node for function"),
                },
                _ => panic!("Expected Call node in body"),
            }

            // Check no else clause
            assert!(for_loop.else_.is_empty());
        }
        _ => panic!("Expected For node"),
    }
}

#[test]
fn test_for_loop_with_else() {
    let code = "for x in items:\n    print(x)\nelse:\n    print('done')";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::For(for_loop) => {
            // Check body
            assert_eq!(for_loop.body.len(), 1);

            // Check else clause
            assert_eq!(for_loop.else_.len(), 1);
            match &for_loop.else_[0] {
                Node::Call(call) => match call.function.as_ref() {
                    Node::Name(name) => assert_eq!(name.id, "print"),
                    _ => panic!("Expected Name node for function"),
                },
                _ => panic!("Expected Call node in else clause"),
            }
        }
        _ => panic!("Expected For node"),
    }
}

#[test]
fn test_simple_while_loop() {
    let code = "while condition:\n    do_something()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::While(while_loop) => {
            // Check test condition
            match while_loop.test.as_ref() {
                Node::Name(name) => assert_eq!(name.id, "condition"),
                _ => panic!("Expected Name node for test"),
            }

            // Check body
            assert_eq!(while_loop.body.len(), 1);
            match &while_loop.body[0] {
                Node::Call(call) => match call.function.as_ref() {
                    Node::Name(name) => assert_eq!(name.id, "do_something"),
                    _ => panic!("Expected Name node for function"),
                },
                _ => panic!("Expected Call node in body"),
            }

            // Check no else clause
            assert!(while_loop.else_.is_empty());
        }
        _ => panic!("Expected While node"),
    }
}

#[test]
fn test_while_loop_with_else() {
    let code = "while condition:\n    do_something()\nelse:\n    print('finished')";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::While(while_loop) => {
            // Check body
            assert_eq!(while_loop.body.len(), 1);

            // Check else clause
            assert_eq!(while_loop.else_.len(), 1);
            match &while_loop.else_[0] {
                Node::Call(call) => match call.function.as_ref() {
                    Node::Name(name) => assert_eq!(name.id, "print"),
                    _ => panic!("Expected Name node for function"),
                },
                _ => panic!("Expected Call node in else clause"),
            }
        }
        _ => panic!("Expected While node"),
    }
}

#[test]
fn test_simple_try_except() {
    let code = "try:\n    risky_operation()\nexcept:\n    handle_error()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Try(try_stmt) => {
            // Check body
            assert_eq!(try_stmt.body.len(), 1);
            match &try_stmt.body[0] {
                Node::Call(call) => match call.function.as_ref() {
                    Node::Name(name) => assert_eq!(name.id, "risky_operation"),
                    _ => panic!("Expected Name node for function"),
                },
                _ => panic!("Expected Call node in try body"),
            }

            // Check exception handlers
            assert_eq!(try_stmt.handlers.len(), 1);
            let handler = &try_stmt.handlers[0];
            assert!(handler.type_.is_none()); // No specific exception type
            assert!(handler.name.is_none()); // No variable name
            assert_eq!(handler.body.len(), 1);

            // Check no else or finally
            assert!(try_stmt.else_.is_empty());
            assert!(try_stmt.finalbody.is_empty());
        }
        _ => panic!("Expected Try node"),
    }
}

#[test]
fn test_try_except_with_type() {
    let code = "try:\n    risky_operation()\nexcept ValueError:\n    handle_value_error()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Try(try_stmt) => {
            // Check exception handlers
            assert_eq!(try_stmt.handlers.len(), 1);
            let handler = &try_stmt.handlers[0];

            // Check exception type
            assert!(handler.type_.is_some());
            match handler.type_.as_ref().unwrap().as_ref() {
                Node::Name(name) => assert_eq!(name.id, "ValueError"),
                _ => panic!("Expected Name node for exception type"),
            }

            assert!(handler.name.is_none()); // No variable name
        }
        _ => panic!("Expected Try node"),
    }
}

#[test]
fn test_try_except_as_variable() {
    let code = "try:\n    risky_operation()\nexcept ValueError as e:\n    print(e)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Try(try_stmt) => {
            // Check exception handlers
            assert_eq!(try_stmt.handlers.len(), 1);
            let handler = &try_stmt.handlers[0];

            // Check exception type
            assert!(handler.type_.is_some());
            match handler.type_.as_ref().unwrap().as_ref() {
                Node::Name(name) => assert_eq!(name.id, "ValueError"),
                _ => panic!("Expected Name node for exception type"),
            }

            // Check variable name
            assert_eq!(handler.name.as_ref().unwrap(), "e");
        }
        _ => panic!("Expected Try node"),
    }
}

#[test]
fn test_try_except_else_finally() {
    let code = "try:\n    risky_operation()\nexcept:\n    handle_error()\nelse:\n    success()\nfinally:\n    cleanup()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Try(try_stmt) => {
            // Check body
            assert_eq!(try_stmt.body.len(), 1);

            // Check handlers
            assert_eq!(try_stmt.handlers.len(), 1);

            // Check else clause
            assert_eq!(try_stmt.else_.len(), 1);
            match &try_stmt.else_[0] {
                Node::Call(call) => match call.function.as_ref() {
                    Node::Name(name) => assert_eq!(name.id, "success"),
                    _ => panic!("Expected Name node for function"),
                },
                _ => panic!("Expected Call node in else clause"),
            }

            // Check finally clause
            assert_eq!(try_stmt.finalbody.len(), 1);
            match &try_stmt.finalbody[0] {
                Node::Call(call) => match call.function.as_ref() {
                    Node::Name(name) => assert_eq!(name.id, "cleanup"),
                    _ => panic!("Expected Name node for function"),
                },
                _ => panic!("Expected Call node in finally clause"),
            }
        }
        _ => panic!("Expected Try node"),
    }
}

#[test]
fn test_multiple_except_handlers() {
    let code = "try:\n    risky_operation()\nexcept ValueError:\n    handle_value_error()\nexcept TypeError:\n    handle_type_error()";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::Try(try_stmt) => {
            // Check handlers
            assert_eq!(try_stmt.handlers.len(), 2);

            // First handler
            let handler1 = &try_stmt.handlers[0];
            match handler1.type_.as_ref().unwrap().as_ref() {
                Node::Name(name) => assert_eq!(name.id, "ValueError"),
                _ => panic!("Expected Name node for first exception type"),
            }

            // Second handler
            let handler2 = &try_stmt.handlers[1];
            match handler2.type_.as_ref().unwrap().as_ref() {
                Node::Name(name) => assert_eq!(name.id, "TypeError"),
                _ => panic!("Expected Name node for second exception type"),
            }
        }
        _ => panic!("Expected Try node"),
    }
}

#[test]
fn test_for_loop_destructuring() {
    let code = "for x, y in pairs:\n    print(x, y)";
    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let nodes = parser.parse().expect("Parsing should succeed");

    assert_eq!(nodes.len(), 1);

    match &nodes[0] {
        Node::For(for_loop) => {
            // Check target is a tuple (destructuring)
            match for_loop.target.as_ref() {
                Node::Tuple(tuple) => {
                    assert_eq!(tuple.elements.len(), 2);
                    match &tuple.elements[0] {
                        Node::Name(name) => assert_eq!(name.id, "x"),
                        _ => panic!("Expected Name node for first target"),
                    }
                    match &tuple.elements[1] {
                        Node::Name(name) => assert_eq!(name.id, "y"),
                        _ => panic!("Expected Name node for second target"),
                    }
                }
                _ => panic!("Expected Tuple node for destructuring target"),
            }
        }
        _ => panic!("Expected For node"),
    }
}
