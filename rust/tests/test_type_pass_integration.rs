#[cfg(test)]
mod type_pass_integration_tests {
    use sharpy_compiler_toolchain::ast::node::Module;
    use sharpy_compiler_toolchain::semantic::MultiPassAnalyzer;
    use sharpy_compiler_toolchain::{Node, Parser, SemanticAnalyzer, SharpyLexer};

    #[test]
    fn test_type_pass_integration() {
        let code = r#"
def add(x: int, y: int) -> int:
    return x + y

x = 5
y = 10
result = add(x, y)
"#;

        // Parse the code
        let mut lexer = SharpyLexer::new(code);
        let tokens = lexer.tokenize_all().expect("Lexer should succeed");

        let mut parser = Parser::new(tokens);
        let ast = parser.parse().expect("Parser should succeed");

        // Wrap AST in a Module node for MultiPassAnalyzer
        let module_node = Node::Module(Module {
            body: ast,
            source: None,
        });

        // Analyze with Type Pass enabled using MultiPassAnalyzer
        let mut analyzer = MultiPassAnalyzer::new();
        let result = analyzer.analyze_module(
            "test_module".to_string(),
            "test.spy".to_string(),
            &module_node,
        );

        // Type Pass should run successfully
        assert!(
            result.success,
            "Semantic analysis should succeed, but got errors: {:?}",
            result.errors
        );

        println!("✓ Type Pass integration test passed!");
        println!("  Analysis completed with {} errors", result.errors.len());
    }

    #[test]
    fn test_type_pass_with_type_errors() {
        let code = r#"
def add(x: int, y: int) -> int:
    return x + y

x = "hello"  # String assigned to what will be used as int
y = 10
result = add(x, y)  # This should potentially flag a type issue
"#;

        // Parse the code
        let mut lexer = SharpyLexer::new(code);
        let tokens = lexer.tokenize_all().expect("Lexer should succeed");

        let mut parser = Parser::new(tokens);
        let ast = parser.parse().expect("Parser should succeed");

        // Wrap AST in a Module node for MultiPassAnalyzer
        let module_node = Node::Module(Module {
            body: ast,
            source: None,
        });

        // Analyze with Type Pass enabled using MultiPassAnalyzer
        let mut analyzer = MultiPassAnalyzer::new();
        let result = analyzer.analyze_module(
            "test_module".to_string(),
            "test.spy".to_string(),
            &module_node,
        );

        // Type Pass should run (even if it finds issues)
        println!(
            "Type Pass analysis completed with {} errors",
            result.errors.len()
        );
        for (i, error) in result.errors.iter().enumerate() {
            println!("Error {}: {:?}", i + 1, error);
        }

        // The test succeeds if the Type Pass runs without crashing
        println!("✓ Type Pass error handling test passed!");
    }

    #[test]
    fn test_legacy_semantic_analyzer() {
        let code = r#"
def add(x: int, y: int) -> int:
    return x + y

x = 5
y = 10
result = add(x, y)
"#;

        // Parse the code
        let mut lexer = SharpyLexer::new(code);
        let tokens = lexer.tokenize_all().expect("Lexer should succeed");

        let mut parser = Parser::new(tokens);
        let ast = parser.parse().expect("Parser should succeed");

        // Test legacy analyzer still works
        let mut analyzer = SemanticAnalyzer::new();
        let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

        // Legacy analyzer should work
        assert!(
            result.is_ok(),
            "Legacy semantic analysis should succeed, but got error: {:?}",
            result.err()
        );

        println!("✓ Legacy semantic analyzer test passed!");
    }
}
