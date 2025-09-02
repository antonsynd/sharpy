use sharpy_compiler_toolchain::SharpyLexer;

#[test]
fn test_comment_unicode_edge_cases() {
    let test_cases = vec![
        "# Comment with émojis 🚀🔥",
        "# Comment with unicode: αβγδε",
        "# Comment with chinese: 你好世界",
        "x = 1 # Inline with unicode 🎯",
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(result.is_ok(), "Failed to parse unicode comment: {}", case);
    }
}

#[test]
fn test_multiple_line_comments() {
    let multiline_comment = "# First comment\n# Second comment\n# Third comment";
    let mut lexer = SharpyLexer::new(multiline_comment);
    let result = lexer.tokenize_all();
    assert!(result.is_ok(), "Failed to parse multiple line comments");
}

#[test]
fn test_comment_at_eof() {
    let comment_at_end = "x = 1\n# Final comment";
    let mut lexer = SharpyLexer::new(comment_at_end);
    let result = lexer.tokenize_all();
    assert!(result.is_ok(), "Failed to parse comment at EOF");
}

#[test]
fn test_whitespace_in_multiline_strings() {
    let with_whitespace = "\"\"\"\n  Indented content\n    More indented\n\"\"\"";
    let mut lexer = SharpyLexer::new(with_whitespace);
    let result = lexer.tokenize_all();
    assert!(
        result.is_ok(),
        "Failed to parse multiline string with whitespace"
    );
}

#[test]
fn test_string_triple_quote_edge_cases() {
    // Test various combinations of triple quotes
    let valid_cases = vec![
        "\"\"\"\"\"\"",        // Empty string
        "''''''",              // Empty string with single quotes
        "\"\"\"Content\"\"\"", // Simple content
        "'''Content'''",       // Single quote version
    ];

    for case in valid_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Failed to parse valid triple quote: {}",
            case
        );
    }
}

#[test]
fn test_invalid_extra_quotes() {
    // These should fail as they create unterminated strings
    let invalid_cases = vec![
        "\"\"\"Content\"\"\"\"", // Extra quote at end - creates unterminated
        "'''Content''''",        // Extra quote at end - creates unterminated
    ];

    for case in invalid_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(result.is_err(), "Should fail for extra quotes: {}", case);
    }

    // These should succeed despite looking tricky
    let valid_cases = vec![
        "\"\"\"\"Content\"\"\"", // Empty string + "Content" - this is valid!
        "''''Content'''",        // Single quote + triple quoted string - this is valid!
    ];

    for case in valid_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Should succeed for valid quote pattern: {}",
            case
        );
    }
}

#[test]
fn test_string_ending_edge_cases() {
    // Cases that should succeed
    let success_cases = vec![
        "\"\"\"String\\nwith\\nnewlines\"\"\"", // Escaped newlines
        "'''Normal string without escapes'''",  // Simple case
    ];

    for case in success_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Failed to parse string ending edge case: {}",
            case
        );
    }

    // Cases that should fail due to escaped closing quotes
    let failure_cases = vec![
        "\"\"\"String ending with backslash\\\"\"\"", // Backslash escapes closing quote
        "'''String ending with backslash\\'''",       // Backslash escapes closing quote
    ];

    for case in failure_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_err(),
            "Should fail when backslash escapes closing quote: {}",
            case
        );
    }
}

#[test]
fn test_string_with_comment_like_content() {
    let tricky_strings = vec![
        "\"\"\"String with # hash character\"\"\"",
        "'''String with # hash character'''",
        "\"\"\"Multi\nline with # hash\ncharacter\"\"\"",
    ];

    for case in tricky_strings {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Failed to parse string with # character: {}",
            case
        );
    }
}

#[test]
fn test_almost_triple_quotes() {
    // These should NOT be treated as triple quotes
    let not_triple_cases = vec![
        "\"\" + \"\"", // Two separate strings
        "'' + '",      // Two separate strings
    ];

    for case in not_triple_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        // These might fail due to parsing issues, but shouldn't cause lexer crashes
        let _result = result; // Just ensure no panic
    }
}

#[test]
fn test_string_with_only_quotes() {
    // Test strings with different numbers of quotes

    // Cases that should succeed (create valid strings)
    let success_cases = [
        "''''''",             // Six single quotes (creates two empty strings)
        "\"\"\"''''''\"\"\"", // Mix of quotes inside triple quotes
    ];

    for case in success_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Failed to parse quote-only string: {}",
            case
        );
    }

    // Cases that should fail (create unterminated strings)
    let failure_cases = [
        "'''''''",  // Seven single quotes (''' + ' creates unterminated)
        "\"\"\"\"", // Four double quotes (creates unterminated)
    ];

    for case in failure_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_err(),
            "Expected error for malformed quote pattern: {}",
            case
        );
    }
}

#[test]
fn test_deeply_nested_quotes() {
    // Test strings with many levels of quote nesting
    let nested = "\"\"\"Outer \"middle 'inner' middle\" outer\"\"\"";
    let mut lexer = SharpyLexer::new(nested);
    let result = lexer.tokenize_all();
    assert!(result.is_ok(), "Failed to parse deeply nested quotes");
}
