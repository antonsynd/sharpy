use sharpy_compiler_toolchain::semantic::{BuiltinType, SemanticType};
use sharpy_compiler_toolchain::{Parser, SemanticAnalyzer, SharpyLexer};

#[test]
fn test_builtin_function_argument_validation() {
    // Test len() with correct argument count
    let source = "result = len([1, 2, 3])";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));
    assert!(
        result.is_ok(),
        "len() with 1 argument should succeed: {:?}",
        result
    );

    // Test len() with incorrect argument count (too many)
    let source = "result = len([1, 2], [3, 4])";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));
    assert!(result.is_err());
    let errors = result.unwrap_err();
    assert!(
        errors
            .iter()
            .any(|err| err.contains("Function 'len' expects 1 argument, got 2"))
    );

    // Test str() with correct argument count
    let source = "result = str(42)";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));
    assert!(
        result.is_ok(),
        "str() with 1 argument should succeed: {:?}",
        result
    );

    // Test str() with incorrect argument count (too few)
    let source = "result = str()";
    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));
    assert!(result.is_err());
    let errors = result.unwrap_err();
    assert!(
        errors
            .iter()
            .any(|err| err.contains("Function 'str' expects 1 argument, got 0"))
    );
}

#[test]
fn test_user_function_argument_validation() {
    // Define a function with typed parameters and call it correctly
    let source = r#"
def add_numbers(x: int, y: int) -> int:
    return x + y

result = add_numbers(5, 3)
"#;

    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));
    assert!(
        result.is_ok(),
        "Function call with correct arguments should succeed: {:?}",
        result
    );

    // Test calling the function with wrong argument count
    let source = r#"
def add_numbers(x: int, y: int) -> int:
    return x + y

result = add_numbers(5)
"#;

    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));
    assert!(result.is_err());
    let errors = result.unwrap_err();
    assert!(
        errors
            .iter()
            .any(|err| err.contains("Function 'add_numbers' expects 2 arguments, got 1"))
    );
}

#[test]
fn test_function_argument_type_validation() {
    // Define a function and call it with correct types (int can be converted to float)
    let source = r#"
def process_number(x: float) -> float:
    return x * 2.0

result = process_number(5)
"#;

    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));
    assert!(
        result.is_ok(),
        "Function call with int->float conversion should succeed: {:?}",
        result
    );

    // Test calling function with incompatible type
    let source = r#"
def process_number(x: int) -> int:
    return x * 2

result = process_number("hello")
"#;

    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));
    assert!(result.is_err());
    let errors = result.unwrap_err();
    assert!(errors.iter().any(|err| {
        err.contains("Function 'process_number' argument 1 expects type")
            && err.contains("int")
            && err.contains("str")
    }));
}

#[test]
fn test_function_return_type_analysis() {
    // Test that function calls return the correct type
    let source = r#"
def get_name() -> str:
    return "Alice"

def get_age() -> int:
    return 25

name = get_name()
age = get_age()
"#;

    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));
    assert!(
        result.is_ok(),
        "Function calls with return types should succeed: {:?}",
        result
    );

    // Verify the types are correctly inferred
    let symbol_table = analyzer.get_symbol_table();

    if let Some(name_symbol) = symbol_table.lookup_symbol("name") {
        assert_eq!(
            name_symbol.symbol_type,
            SemanticType::Builtin(BuiltinType::Str)
        );
    }

    if let Some(age_symbol) = symbol_table.lookup_symbol("age") {
        assert_eq!(
            age_symbol.symbol_type,
            SemanticType::Builtin(BuiltinType::Int)
        );
    }
}

#[test]
fn test_undefined_function_error() {
    let source = "result = unknown_function(42)";

    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(result.is_err());
    let errors = result.unwrap_err();
    assert!(
        errors
            .iter()
            .any(|err| err.contains("Undefined function: unknown_function"))
    );
}

#[test]
fn test_variable_called_as_function_error() {
    let source = r#"
x = 42
result = x(10)
"#;

    let mut lexer = SharpyLexer::new(source);
    let tokens = lexer.tokenize_all().expect("Should tokenize");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Should parse");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(result.is_err());
    let errors = result.unwrap_err();
    assert!(
        errors
            .iter()
            .any(|err| err.contains("'x' is not a function"))
    );
}
