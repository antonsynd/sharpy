use sharpy_compiler_toolchain::ast::node::Node;
use sharpy_compiler_toolchain::lexer::SharpyLexer;
use sharpy_compiler_toolchain::parser::Parser;

#[test]
fn test_simple_lambda() {
    let input = "lambda x: x + 1";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Lambda(lambda) = result {
        // Check arguments
        assert_eq!(lambda.args.args.len(), 1);
        assert_eq!(lambda.args.args[0].name, "x");
        assert!(lambda.args.args[0].type_.is_none()); // No type annotation
        assert!(lambda.args.args[0].default.is_none()); // No default value

        // Check return type (should be None)
        assert!(lambda.return_type.is_none());

        // Check body is a binary operation (x + 1)
        if let Node::BinaryOp(binary_op) = &*lambda.body {
            if let Node::Name(name) = &*binary_op.left {
                assert_eq!(name.id, "x");
            } else {
                panic!("Expected Name node for left operand");
            }

            if let Node::Constant(constant) = &*binary_op.right {
                if let sharpy_compiler_toolchain::ast::node::ConstantValue::Int(n) = &constant.value
                {
                    assert_eq!(*n, 1);
                } else {
                    panic!("Expected integer constant");
                }
            } else {
                panic!("Expected Constant node for right operand");
            }
        } else {
            panic!("Expected BinaryOp node for lambda body");
        }
    } else {
        panic!("Expected Lambda node, got {:?}", result);
    }
}

#[test]
fn test_lambda_with_multiple_args() {
    let input = "lambda x, y, z: x + y * z";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Lambda(lambda) = result {
        // Check arguments
        assert_eq!(lambda.args.args.len(), 3);
        assert_eq!(lambda.args.args[0].name, "x");
        assert_eq!(lambda.args.args[1].name, "y");
        assert_eq!(lambda.args.args[2].name, "z");

        // All args should have no type annotations
        for arg in &lambda.args.args {
            assert!(arg.type_.is_none());
            assert!(arg.default.is_none());
        }

        // Check return type
        assert!(lambda.return_type.is_none());

        // Body should be a binary operation (x + (y * z))
        if let Node::BinaryOp(_) = &*lambda.body {
            // We don't need to verify the exact structure, just that it parsed
        } else {
            panic!("Expected BinaryOp node for lambda body");
        }
    } else {
        panic!("Expected Lambda node, got {:?}", result);
    }
}

#[test]
fn test_lambda_no_args() {
    let input = "lambda: 42";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Lambda(lambda) = result {
        // Check no arguments
        assert_eq!(lambda.args.args.len(), 0);
        assert!(lambda.args.vararg.is_none());

        // Check return type
        assert!(lambda.return_type.is_none());

        // Check body is a constant (42)
        if let Node::Constant(constant) = &*lambda.body {
            if let sharpy_compiler_toolchain::ast::node::ConstantValue::Int(n) = &constant.value {
                assert_eq!(*n, 42);
            } else {
                panic!("Expected integer constant");
            }
        } else {
            panic!("Expected Constant node for lambda body");
        }
    } else {
        panic!("Expected Lambda node, got {:?}", result);
    }
}

#[test]
fn test_lambda_with_return_type() {
    let input = "lambda x: -> int x * 2";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Lambda(lambda) = result {
        // Check arguments
        assert_eq!(lambda.args.args.len(), 1);
        assert_eq!(lambda.args.args[0].name, "x");

        // Check return type annotation exists
        assert!(lambda.return_type.is_some());
        if let Some(return_type) = &lambda.return_type {
            if let Node::TypeName(type_name) = &**return_type {
                assert_eq!(type_name.name, "int");
            } else {
                panic!("Expected TypeName node for return type");
            }
        }

        // Check body
        if let Node::BinaryOp(_) = &*lambda.body {
            // Body should be x * 2
        } else {
            panic!("Expected BinaryOp node for lambda body");
        }
    } else {
        panic!("Expected Lambda node, got {:?}", result);
    }
}

#[test]
fn test_lambda_in_function_call() {
    let input = "map(lambda x: x * 2, numbers)";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Call(call) = result {
        // Function should be 'map'
        if let Node::Name(name) = &*call.function {
            assert_eq!(name.id, "map");
        } else {
            panic!("Expected Name node for function");
        }

        // Should have 2 positional arguments
        assert_eq!(call.positional_args.len(), 2);

        // First argument should be a lambda
        if let Node::Lambda(lambda) = &call.positional_args[0] {
            assert_eq!(lambda.args.args.len(), 1);
            assert_eq!(lambda.args.args[0].name, "x");
        } else {
            panic!("Expected Lambda node for first argument");
        }

        // Second argument should be 'numbers'
        if let Node::Name(name) = &call.positional_args[1] {
            assert_eq!(name.id, "numbers");
        } else {
            panic!("Expected Name node for second argument");
        }
    } else {
        panic!("Expected Call node, got {:?}", result);
    }
}

#[test]
fn test_lambda_with_complex_body() {
    let input = "lambda x: x.method()[0].value + func(x * 2)";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Lambda(lambda) = result {
        // Check argument
        assert_eq!(lambda.args.args.len(), 1);
        assert_eq!(lambda.args.args[0].name, "x");

        // Check that body parsed as a binary operation
        if let Node::BinaryOp(_) = &*lambda.body {
            // The complex expression should parse correctly
            println!("✅ Lambda with complex body parsed successfully");
        } else {
            panic!("Expected BinaryOp node for lambda body");
        }
    } else {
        panic!("Expected Lambda node, got {:?}", result);
    }
}

#[test]
fn test_nested_lambdas() {
    let input = "lambda x: lambda y: x + y";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Lambda(outer_lambda) = result {
        // Outer lambda should have one argument 'x'
        assert_eq!(outer_lambda.args.args.len(), 1);
        assert_eq!(outer_lambda.args.args[0].name, "x");

        // Body should be another lambda
        if let Node::Lambda(inner_lambda) = &*outer_lambda.body {
            assert_eq!(inner_lambda.args.args.len(), 1);
            assert_eq!(inner_lambda.args.args[0].name, "y");

            // Inner lambda body should be x + y
            if let Node::BinaryOp(_) = &*inner_lambda.body {
                println!("✅ Nested lambdas parsed successfully");
            } else {
                panic!("Expected BinaryOp node for inner lambda body");
            }
        } else {
            panic!("Expected Lambda node for outer lambda body");
        }
    } else {
        panic!("Expected Lambda node, got {:?}", result);
    }
}
