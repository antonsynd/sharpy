use sharpy_compiler_toolchain::ast::node::Node;
use sharpy_compiler_toolchain::lexer::SharpyLexer;
use sharpy_compiler_toolchain::parser::Parser;

#[test]
fn test_lambda_integration_with_phase1_features() {
    // Test lambda expressions combined with all Phase 1 features:
    // - Attribute access
    // - Subscripting
    // - Dict/Set literals
    // - Function calls

    let input = r#"{"transform": lambda x: x.data[0].value * 2, "filter": lambda item: item.score > threshold, "processors": [lambda name: api.get_processor(name), lambda config: factory.create(config)]}"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Dict(dict) = result {
        assert_eq!(dict.keys.len(), 3);

        // First value should be a lambda
        if let Node::Lambda(lambda) = &dict.values[0] {
            assert_eq!(lambda.args.args.len(), 1);
            assert_eq!(lambda.args.args[0].name, "x");

            // Body should be a complex expression: x.data[0].value * 2
            if let Node::BinaryOp(_) = &*lambda.body {
                println!("✅ Lambda with attribute access and subscripting parsed");
            } else {
                panic!("Expected BinaryOp for lambda body");
            }
        } else {
            panic!("Expected Lambda for first value");
        }

        // Second value should be a lambda
        if let Node::Lambda(lambda) = &dict.values[1] {
            assert_eq!(lambda.args.args.len(), 1);
            assert_eq!(lambda.args.args[0].name, "item");
            println!("✅ Lambda with comparison expression parsed");
        } else {
            panic!("Expected Lambda for second value");
        }

        // Third value should be a list containing lambdas
        if let Node::List(list) = &dict.values[2] {
            assert_eq!(list.elements.len(), 2);

            // Both elements should be lambdas
            for (i, element) in list.elements.iter().enumerate() {
                if let Node::Lambda(_) = element {
                    println!("✅ Lambda {} in list parsed", i + 1);
                } else {
                    panic!("Expected Lambda for list element {}", i);
                }
            }
        } else {
            panic!("Expected List for third value");
        }

        println!("🎉 Successfully parsed complex structure with:");
        println!("   - Dict literal containing lambdas");
        println!("   - List literal containing lambdas");
        println!("   - Lambdas with attribute access (x.data[0].value)");
        println!("   - Lambdas with function calls (api.get_processor(name))");
        println!("   - Lambdas with comparison expressions");
    } else {
        panic!("Expected Dict node, got {:?}", result);
    }
}

#[test]
fn test_lambda_as_function_arguments() {
    // Test lambda expressions used as function arguments
    let input = "process(data.items(), lambda x: x.transform(), lambda y: y.validate())";

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Call(call) = result {
        // Should be 3 arguments
        assert_eq!(call.positional_args.len(), 3);

        // First argument: data.items() (attribute access + function call)
        if let Node::Call(_) = &call.positional_args[0] {
            println!("✅ Method call as first argument parsed");
        } else {
            panic!("Expected Call for first argument");
        }

        // Second argument: lambda
        if let Node::Lambda(lambda) = &call.positional_args[1] {
            assert_eq!(lambda.args.args[0].name, "x");
            println!("✅ Lambda as second argument parsed");
        } else {
            panic!("Expected Lambda for second argument");
        }

        // Third argument: lambda
        if let Node::Lambda(lambda) = &call.positional_args[2] {
            assert_eq!(lambda.args.args[0].name, "y");
            println!("✅ Lambda as third argument parsed");
        } else {
            panic!("Expected Lambda for third argument");
        }
    } else {
        panic!("Expected Call node, got {:?}", result);
    }
}

#[test]
fn test_lambda_chaining_with_postfix_operations() {
    // Test lambda expressions that can be called immediately and chained
    let input = "(lambda x: factory.create(x))(config).setup()[0].run()";

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    // The final result should be a method call (.run())
    if let Node::Call(final_call) = result {
        // The function being called should be an attribute (.run)
        if let Node::Attribute(attr) = &*final_call.function {
            assert_eq!(attr.attr, "run");

            // The value should be a subscript ([0])
            if let Node::Subscript(_) = &*attr.value {
                println!("✅ Lambda with immediate invocation and chaining parsed:");
                println!("   (lambda x: factory.create(x))(config).setup()[0].run()");
            } else {
                panic!("Expected Subscript before .run()");
            }
        } else {
            panic!("Expected Attribute for final call");
        }
    } else {
        panic!("Expected Call node for final result, got {:?}", result);
    }
}

#[test]
fn test_lambda_with_all_expression_types() {
    // Test lambda that uses every type of expression in its body
    // Simplified version since we don't have set comprehensions yet
    let input = r#"lambda x, y: {"result": x.data[y] + func(x * 2), "items": collection.items}[key].process()"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Lambda(lambda) = result {
        // Should have 2 arguments
        assert_eq!(lambda.args.args.len(), 2);
        assert_eq!(lambda.args.args[0].name, "x");
        assert_eq!(lambda.args.args[1].name, "y");

        // Body should be a complex chained expression
        if let Node::Call(_) = &*lambda.body {
            println!("✅ Lambda with complex body expression parsed:");
            println!("   - Dict literal");
            println!("   - Attribute access");
            println!("   - Subscripting");
            println!("   - Function calls");
            println!("   - Binary operations");
            println!("   - Chained postfix operations");
        } else {
            panic!("Expected Call for lambda body");
        }
    } else {
        panic!("Expected Lambda node, got {:?}", result);
    }
}
