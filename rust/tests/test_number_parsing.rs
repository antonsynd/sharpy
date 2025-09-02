use sharpy_compiler_toolchain::lexer::NumberType;
use sharpy_compiler_toolchain::{LexerError, SharpyLexer, TokenType};

#[test]
fn test_number_edge_cases() {
    let test_cases = vec![
        // Valid cases
        ("123", NumberType::Integer("123".to_string())),
        ("0", NumberType::Integer("0".to_string())),
        ("3.14", NumberType::Float("3.14".to_string())),
        ("2.5e10", NumberType::Float("2.5e10".to_string())),
        ("1j", NumberType::Imaginary("1j".to_string())),
        ("0xFF", NumberType::Integer("0xFF".to_string())),
        ("0o777", NumberType::Integer("0o777".to_string())),
        ("0b1010", NumberType::Integer("0b1010".to_string())),
        ("1_000_000", NumberType::Integer("1_000_000".to_string())),
        ("3.14_159", NumberType::Float("3.14_159".to_string())),
        (".5", NumberType::Float(".5".to_string())),
        ("5.", NumberType::Float("5.".to_string())),
        ("1e5", NumberType::Float("1e5".to_string())),
        ("1E-5", NumberType::Float("1E-5".to_string())),
        ("2.5e+10", NumberType::Float("2.5e+10".to_string())),
        ("3.14j", NumberType::Imaginary("3.14j".to_string())),
        ("2e3j", NumberType::Imaginary("2e3j".to_string())),
    ];

    for (input, expected) in test_cases {
        let mut lexer = SharpyLexer::new(input);
        let tokens = lexer.tokenize_all().unwrap();

        assert!(!tokens.is_empty(), "No tokens for input: {}", input);

        if let TokenType::Number(number_type) = &tokens[0].token_type {
            assert_eq!(number_type, &expected, "Failed for input: {}", input);
        } else {
            panic!(
                "Expected Number token for input: {}, got: {:?}",
                input, tokens[0].token_type
            );
        }
    }
}

#[test]
fn test_invalid_number_formats() {
    let invalid_cases = vec![
        "0b",  // Binary without digits
        "0o",  // Octal without digits
        "0x",  // Hex without digits
        "1e",  // Exponent without digits
        "1e+", // Exponent sign without digits
        "1e-", // Exponent sign without digits
    ];

    for case in invalid_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_err(),
            "Expected error for invalid number: {}",
            case
        );

        let errors = result.unwrap_err();
        assert!(!errors.is_empty());
        assert!(matches!(errors[0], LexerError::InvalidNumber(_)));
    }
}

#[test]
fn test_number_followed_by_identifier() {
    // These should be parsed as separate tokens
    let input = "123abc";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();

    assert_eq!(tokens.len(), 3); // number, identifier, EOF
    assert!(matches!(tokens[0].token_type, TokenType::Number(_)));
    assert!(matches!(tokens[1].token_type, TokenType::Name(_)));
}

#[test]
fn test_float_vs_method_call() {
    // This should be parsed as: number(5), dot, identifier(abs), parentheses
    let input = "5.abs()";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();

    // Should have: Number(5), Dot, Name(abs), LeftParen, RightParen, EOF
    assert!(tokens.len() >= 5);
    assert!(matches!(
        tokens[0].token_type,
        TokenType::Number(NumberType::Integer(_))
    ));
    assert_eq!(tokens[1].token_type, TokenType::Dot);
    assert!(matches!(tokens[2].token_type, TokenType::Name(_)));
}

#[test]
fn test_ellipsis_vs_float() {
    // Test that "..." is parsed as ellipsis, not as malformed float
    let input = "...";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();

    assert!(!tokens.is_empty());
    assert_eq!(tokens[0].token_type, TokenType::Ellipsis);
}

#[test]
fn test_complex_number_expressions() {
    let test_cases = vec![
        "1 + 2j",      // Real + imaginary
        "3.14 - 1.5j", // Float real + imaginary
        "2j + 3j",     // Imaginary + imaginary
        "0j",          // Zero imaginary
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(result.is_ok(), "Failed to tokenize: {}", case);
    }
}

#[test]
fn test_scientific_notation_edge_cases() {
    let test_cases = vec![
        "1e0",    // Zero exponent
        "1e+0",   // Positive zero exponent
        "1e-0",   // Negative zero exponent
        "1.0e10", // Float with exponent
        "1E10",   // Capital E
        "1e999",  // Large exponent (might overflow but should tokenize)
        "1e-999", // Large negative exponent
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Failed to tokenize scientific notation: {}",
            case
        );

        let tokens = result.unwrap();
        assert!(!tokens.is_empty());
        assert!(matches!(
            tokens[0].token_type,
            TokenType::Number(NumberType::Float(_))
        ));
    }
}

#[test]
fn test_underscore_separators() {
    let test_cases = vec![
        "1_000",       // Thousands separator
        "1_000_000",   // Multiple separators
        "0xFF_FF_FF",  // Hex with separators
        "0b1010_1010", // Binary with separators
        "0o777_777",   // Octal with separators
        "3.14_159",    // Float with separators
        "1_000.5_5",   // Both parts have separators
        "1e1_000",     // Exponent with separators
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Failed to tokenize number with underscores: {}",
            case
        );
    }
}
