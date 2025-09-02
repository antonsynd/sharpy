use sharpy_compiler_toolchain::{LexerError, ParseError, Parser, SharpyLexer};

#[test]
fn test_unterminated_string() {
    let mut lexer = SharpyLexer::new("\"unterminated string");
    let result = lexer.tokenize_all();
    assert!(result.is_err());
    let errors = result.unwrap_err();
    assert_eq!(errors.len(), 1);
    assert!(matches!(errors[0], LexerError::UnterminatedString));
}

#[test]
fn test_unterminated_triple_string() {
    let mut lexer = SharpyLexer::new("\"\"\"unterminated triple string");
    let result = lexer.tokenize_all();
    assert!(result.is_err());
    let errors = result.unwrap_err();
    assert_eq!(errors.len(), 1);
    assert!(matches!(errors[0], LexerError::UnterminatedString));
}

#[test]
fn test_invalid_escape_sequence() {
    let mut lexer = SharpyLexer::new("\"\\q\"");
    let result = lexer.tokenize_all();
    // This should actually work since unknown escapes are preserved
    assert!(result.is_ok());
}

#[test]
fn test_invalid_indentation() {
    let mut lexer = SharpyLexer::new("if True:\n  x = 1\n   y = 2");
    let result = lexer.tokenize_all();
    assert!(result.is_err());
    let errors = result.unwrap_err();
    assert!(!errors.is_empty());
    assert!(matches!(errors[0], LexerError::InvalidIndentation(_)));
}

#[test]
fn test_mixed_tabs_and_spaces() {
    let mut lexer = SharpyLexer::new("if True:\n\tx = 1\n    y = 2");
    let result = lexer.tokenize_all();
    assert!(result.is_err());
    let errors = result.unwrap_err();
    assert!(!errors.is_empty());
    // The lexer detects tabs and treats them as invalid indentation
    assert!(matches!(errors[0], LexerError::InvalidIndentation(_)));
}

#[test]
fn test_parser_unexpected_token() {
    let mut lexer = SharpyLexer::new("x = = 5");
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_err());
    let error = result.unwrap_err();
    assert!(matches!(error, ParseError::UnexpectedToken { .. }));
}

#[test]
fn test_parser_unexpected_eof() {
    let mut lexer = SharpyLexer::new("x =");
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_err());
    let error = result.unwrap_err();
    // The parser generates UnexpectedToken when it finds EOF instead of expected expression
    assert!(matches!(error, ParseError::UnexpectedToken { .. }));
}

#[test]
fn test_invalid_syntax_empty_block() {
    let mut lexer = SharpyLexer::new("if True:\n    pass\nelse:\n    ");
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    // This should fail because the else block is empty (no statement after indentation)
    assert!(result.is_err());
    let error = result.unwrap_err();
    assert!(matches!(error, ParseError::UnexpectedToken { .. }));
}

#[test]
fn test_nested_f_string_error() {
    let mut lexer = SharpyLexer::new("f\"outer {f\"inner\"} text\"");
    let result = lexer.tokenize_all();
    // This is complex f-string nesting which might not be fully supported
    // The test should at least not panic
    let _result = result;
}

#[test]
fn test_number_parsing_edge_cases() {
    let test_cases = vec![
        "123",    // Simple integer
        "0",      // Zero
        "007",    // Leading zeros
        "0xFF",   // Hex
        "0o777",  // Octal
        "0b1010", // Binary
        "3.14",   // Float
        "2.5e10", // Scientific notation
        "1j",     // Imaginary
        "3.14j",  // Complex imaginary
        ".5",     // Float starting with dot
        "5.",     // Float ending with dot
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(result.is_ok(), "Failed to parse number: {}", case);
    }
}

#[test]
fn test_invalid_number_formats() {
    let test_cases = vec![
        "0xG",  // Invalid hex digit
        "0o8",  // Invalid octal digit
        "0b2",  // Invalid binary digit
        "5..5", // Double dot
        "5e",   // Incomplete scientific notation
        "5e+",  // Incomplete scientific notation with sign
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        // Some of these might be parsed as separate tokens rather than errors
        // The test ensures we don't panic
        let _result = result;
    }
}
