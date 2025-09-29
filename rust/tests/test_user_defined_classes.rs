use sharpy_compiler_toolchain::{BuiltinType, Parser, SemanticAnalyzer, SemanticType, SharpyLexer};

#[test]
fn test_user_defined_method_calls() {
    let code = r#"
class Calculator:
    def add(self, x: int, y: int) -> int:
        return x + y

    def get_zero() -> int:
        return 0

calc = Calculator()
result = calc.add(5, 3)
zero = Calculator.get_zero()
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Tokenization failed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing failed");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Analysis should succeed: {:?}",
        result.err()
    );

    let symbol_table = analyzer.get_symbol_table();

    // Check that result has the correct return type (int)
    let result_symbol = symbol_table
        .lookup_symbol("result")
        .expect("result should be in symbol table");
    assert_eq!(
        result_symbol.symbol_type,
        SemanticType::Builtin(BuiltinType::Int)
    );

    // Check that zero has the correct return type (int)
    let zero_symbol = symbol_table
        .lookup_symbol("zero")
        .expect("zero should be in symbol table");
    assert_eq!(
        zero_symbol.symbol_type,
        SemanticType::Builtin(BuiltinType::Int)
    );
}

#[test]
fn test_constructor_validation() {
    let code = r#"
class Person:
    def __init__(self, name: str, age: int):
        pass

person1 = Person("Alice", 25)
person2 = Person("Bob")
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Tokenization failed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing failed");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    // Should fail due to wrong argument count in constructor
    assert!(
        result.is_err(),
        "Analysis should fail for invalid constructor call"
    );
    let error_msg = format!("{:?}", result.err().unwrap());
    assert!(
        error_msg.contains("expects")
            && (error_msg.contains("arguments") || error_msg.contains("parameters"))
    );
}

#[test]
fn test_static_vs_instance_methods() {
    let code = r#"
class MathUtils:
    @static
    def multiply(x: int, y: int) -> int:
        return x * y

    def add_to_self(self, x: int) -> int:
        return x

utils = MathUtils()
static_result = MathUtils.multiply(3, 4)
instance_result = utils.add_to_self(5)
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Tokenization failed");
    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing failed");
    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze_module(&ast, Some("test_module".to_string()));

    assert!(
        result.is_ok(),
        "Analysis should succeed: {:?}",
        result.err()
    );

    let symbol_table = analyzer.get_symbol_table();

    // Both results should be int
    let static_result_symbol = symbol_table
        .lookup_symbol("static_result")
        .expect("static_result should be in symbol table");
    assert_eq!(
        static_result_symbol.symbol_type,
        SemanticType::Builtin(BuiltinType::Int)
    );

    let instance_result_symbol = symbol_table
        .lookup_symbol("instance_result")
        .expect("instance_result should be in symbol table");
    assert_eq!(
        instance_result_symbol.symbol_type,
        SemanticType::Builtin(BuiltinType::Int)
    );
}
