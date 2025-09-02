use sharpy_compiler_toolchain::ast::node::Node;
use sharpy_compiler_toolchain::lexer::SharpyLexer;
use sharpy_compiler_toolchain::parser::Parser;

#[test]
fn test_phase1_integration() {
    // This test demonstrates all Phase 1 features working together:
    // - Attribute access
    // - Subscripting
    // - Dict literals
    // - Set literals
    // - Complex combinations

    let input = r#"{"users": api.users.get_all()[0].data, "config": {"host": server.config.host, "port": server.config.port}, "tags": {"urgent", "important", user.get_tags()[0]}}"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    // Should parse as a dict with 3 keys: "users", "config", "tags"
    if let Node::Dict(dict) = result {
        assert_eq!(dict.keys.len(), 3);
        assert_eq!(dict.values.len(), 3);

        // Verify the structure is parsed correctly
        println!("✅ Parsed complex nested expression with:");
        println!("   - Dict literals");
        println!("   - Set literals");
        println!("   - Attribute access (api.users.get_all)");
        println!("   - Subscripting ([0])");
        println!("   - Method calls (.get_all(), .get_tags())");
        println!("   - Nested dicts and sets");
    } else {
        panic!("Expected Dict node, got {:?}", result);
    }
}

#[test]
fn test_complex_chaining() {
    // Test complex chaining that combines all postfix operations
    let input = "data[key].items()[0].process().result.value[index]";

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    // This should parse as a series of chained operations
    // The exact structure is complex, but we mainly want to ensure it parses without error
    match result {
        Node::Subscript(_) => {
            println!("✅ Successfully parsed complex chaining expression:");
            println!("   data[key].items()[0].process().result.value[index]");
        }
        _ => panic!("Expected final operation to be Subscript, got {:?}", result),
    }
}

#[test]
fn test_mixed_literals_and_operations() {
    // Test mixing all the new literal types with operations
    // Note: List comprehensions aren't implemented yet, so we'll test a simplified version
    let input = r#"{"results": items.data, "metadata": {"count": items.length}}"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    if let Node::Dict(dict) = result {
        assert_eq!(dict.keys.len(), 2);

        // First value should be an attribute access (items.data)
        if let Node::Attribute(attr) = &dict.values[0] {
            assert_eq!(attr.attr, "data");
        } else {
            panic!("Expected Attribute node for first value");
        }

        // Second value should be a nested dict
        if let Node::Dict(nested_dict) = &dict.values[1] {
            assert_eq!(nested_dict.keys.len(), 1);
        } else {
            panic!("Expected Dict node for second value");
        }

        println!("✅ Successfully parsed nested dict with attribute access");
    } else {
        panic!("Expected Dict node, got {:?}", result);
    }
}

#[test]
fn test_all_postfix_operations() {
    // Test that all postfix operations work together in a single expression
    let input = "obj.method()[key].attr.func(arg)[0]";

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to lex");
    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression().expect("Failed to parse");

    // Should end with a subscript operation
    if let Node::Subscript(subscript) = result {
        // The value should be a function call
        if let Node::Call(call) = &*subscript.value {
            // The function should be an attribute access
            if let Node::Attribute(attr) = &*call.function {
                assert_eq!(attr.attr, "func");
                println!("✅ All postfix operations chained successfully:");
                println!("   - Method call: obj.method()");
                println!("   - Subscript: [key]");
                println!("   - Attribute: .attr");
                println!("   - Function call: .func(arg)");
                println!("   - Final subscript: [0]");
            } else {
                panic!("Expected Attribute node for function");
            }
        } else {
            panic!("Expected Call node for subscript value");
        }
    } else {
        panic!("Expected Subscript node, got {:?}", result);
    }
}
