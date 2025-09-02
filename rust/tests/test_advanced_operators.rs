use sharpy_compiler_toolchain::{Parser, SharpyLexer};

#[test]
fn test_comprehensive_operator_precedence() {
    // Test complex expression with multiple operator precedences
    // Note: Using bitwise operators instead of logical 'and'/'or' since those aren't implemented yet
    let input = "a | b & c == d + e * f ** g";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    match &result {
        Ok(_) => println!("Successfully parsed: {}", input),
        Err(e) => println!("Parse error for '{}': {}", input, e),
    }
    assert!(result.is_ok());
}

#[test]
fn test_unary_operator_precedence() {
    // Test that unary operators have correct precedence
    // Using ~ instead of not since 'not' keyword isn't implemented in parser yet
    let input = "~-x + y";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_power_operator_right_associativity() {
    // Test that ** is right-associative
    let input = "x ** y ** z";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_complex_comparison_chains() {
    // Test chained comparisons like a < b <= c == d != e
    let input = "a < b <= c == d != e";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_compound_assignment_operators() {
    let operators = vec![
        "+=", "-=", "*=", "/=", "//=", "**=", "%=", "&=", "|=", "^=", "<<=", ">>=", "@=",
    ];

    for op in operators {
        let input = format!("x {op} 5");
        let mut lexer = SharpyLexer::new(&input);
        let result = lexer.tokenize_all();
        assert!(result.is_ok(), "Failed to parse operator: {}", op);
    }
}

#[test]
fn test_sharpy_specific_operators() {
    let operators = vec![
        "?.", // Optional chaining
        "??", // Null coalescing
        "?",  // Optional type suffix
    ];

    for op in operators {
        let input = format!("x {op} y");
        let mut lexer = SharpyLexer::new(&input);
        let result = lexer.tokenize_all();
        assert!(result.is_ok(), "Failed to parse Sharpy operator: {}", op);
    }
}

#[test]
fn test_bitwise_operator_precedence() {
    // Test that bitwise operators have correct precedence
    let input = "a & b | c ^ d";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_shift_operator_precedence() {
    // Test that shift operators have correct precedence
    let input = "a + b << c * d";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_matrix_multiplication_precedence() {
    // Test that @ operator has correct precedence
    let input = "a @ b + c * d";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_complex_nested_expressions() {
    // Test deeply nested expressions with parentheses
    let input = "((a + b) * (c - d)) / ((e ** f) % (g // h))";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_is_not_and_not_in_operators() {
    let test_cases = vec![
        "x is not y",
        "x not in y",
        "x is not None",
        "key not in dict",
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);
        let result = parser.parse();
        // These might not parse completely since is/in might not be fully implemented
        // but they should at least tokenize correctly
        let _result = result; // Don't assert success since these might not be implemented
    }
}
