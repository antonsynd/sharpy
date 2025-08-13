use sharpy_compiler_toolchain::lexer::{LexerError, SharpyLexer};

#[test]
fn test_valid_4_space_indentation() {
    let code = "if True:\n    x = 1\n    y = 2\nelse:\n    z = 3";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(
        result.is_ok(),
        "Valid 4-space indentation should be accepted"
    );
}

#[test]
fn test_valid_8_space_indentation() {
    let code = "if True:\n    if True:\n        x = 1\n    y = 2";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(
        result.is_ok(),
        "Valid 8-space indentation should be accepted"
    );
}

#[test]
fn test_tab_indentation_rejected() {
    let code = "if True:\n\tx = 1"; // Tab character for indentation
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_err(), "Tab indentation should be rejected");

    let errors = result.unwrap_err();
    assert!(!errors.is_empty());
    assert!(matches!(errors[0], LexerError::InvalidIndentation(_)));
}

#[test]
fn test_mixed_tab_space_indentation_rejected() {
    let code = "if True:\n \t  x = 1"; // Mixed spaces and tab
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(
        result.is_err(),
        "Mixed tab/space indentation should be rejected"
    );

    let errors = result.unwrap_err();
    assert!(!errors.is_empty());
    assert!(matches!(errors[0], LexerError::InvalidIndentation(_)));
}

#[test]
fn test_invalid_space_count_rejected() {
    let code = "if True:\n   x = 1"; // 3 spaces instead of 4
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(
        result.is_err(),
        "Non-4-space indentation should be rejected"
    );

    let errors = result.unwrap_err();
    assert!(!errors.is_empty());
    assert!(matches!(errors[0], LexerError::InvalidIndentation(_)));
}

#[test]
fn test_5_space_indentation_rejected() {
    let code = "if True:\n     x = 1"; // 5 spaces
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_err(), "5-space indentation should be rejected");

    let errors = result.unwrap_err();
    assert!(!errors.is_empty());
    assert!(matches!(errors[0], LexerError::InvalidIndentation(_)));
}

#[test]
fn test_zero_indentation_valid() {
    let code = "x = 1\ny = 2"; // No indentation
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok(), "Zero indentation should be valid");
}

#[test]
fn test_tabs_allowed_in_expressions() {
    let code = "x =\t\t1 +\t2"; // Tabs within expression (not at line start)
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok(), "Tabs within expressions should be allowed");
}

#[test]
fn test_detailed_error_message() {
    let code = "if True:\n   x = 1"; // 3 spaces
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_err());

    let errors = result.unwrap_err();
    if let LexerError::InvalidIndentation(msg) = &errors[0] {
        assert!(
            msg.contains("4 spaces"),
            "Error message should mention 4 spaces"
        );
        assert!(
            msg.contains('3'),
            "Error message should mention actual count"
        );
    } else {
        panic!("Expected InvalidIndentation error");
    }
}
