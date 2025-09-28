use sharpy_compiler_toolchain::{SharpyLexer, Parser, SemanticAnalyzer};

#[test]
fn test_basic_type_checking() {
    let source = "x = 5";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Tokenizing should succeed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    // Should succeed - simple assignment
    assert!(result.is_ok(), "Basic assignment should succeed: {:?}", result);
    assert!(analyzer.get_errors().is_empty(), "Should have no errors: {:?}", analyzer.get_errors());
}

#[test]
fn test_expression_type_analysis() {
    let source = "
x = 42
y = 3.14
z = x + y
";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Tokenizing should succeed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    // Should succeed - type compatible operations
    assert!(result.is_ok(), "Mixed int/float arithmetic should succeed: {:?}", result);
}

#[test]
fn test_function_call_analysis() {
    let source = "
result = len([1, 2, 3])
message = str(42)
";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Tokenizing should succeed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    // Should succeed - built-in function calls
    assert!(result.is_ok(), "Built-in function calls should succeed: {:?}", result);
}

#[test]
fn test_constant_analysis() {
    let source = "
a = 10
b = 3.14
c = \"hello\"
d = True
e = None
";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Tokenizing should succeed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    // Should succeed - all basic constants
    assert!(result.is_ok(), "Constant assignments should succeed: {:?}", result);
    assert!(analyzer.get_errors().is_empty(), "Should have no errors: {:?}", analyzer.get_errors());
}

#[test]
fn test_arithmetic_expressions() {
    let source = "
a = 10 + 5
b = 20.5 - 3.2
c = 4 * 7
d = 15.0 / 3.0
";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Tokenizing should succeed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    // Should succeed - arithmetic operations
    assert!(result.is_ok(), "Arithmetic expressions should succeed: {:?}", result);
}

#[test]
fn test_string_concatenation() {
    let source = "
greeting = \"Hello\" + \" World\"
";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Tokenizing should succeed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    // Should succeed - string concatenation
    assert!(result.is_ok(), "String concatenation should succeed: {:?}", result);
}
