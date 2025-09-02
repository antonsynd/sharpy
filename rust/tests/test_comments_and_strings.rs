use sharpy_compiler_toolchain::{SharpyLexer, TokenType};

#[test]
fn test_simple_comments() {
    let test_cases = vec![
        "# Simple comment",
        "x = 1 # Inline comment",
        "####### Multiple hashes",
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(result.is_ok(), "Failed to parse comment: {}", case);
    }
}

#[test]
fn test_comment_edge_cases() {
    let test_cases = vec![
        "x = 1#",  // Empty comment
        "x = 1# ", // Comment with just space
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Failed to parse comment edge case: {}",
            case
        );
    }
}

#[test]
fn test_basic_multiline_strings() {
    // Test with escaped quotes to avoid syntax issues
    let simple_triple = "\"\"\"Simple triple quoted string\"\"\"";
    let mut lexer = SharpyLexer::new(simple_triple);
    let result = lexer.tokenize_all();
    assert!(
        result.is_ok(),
        "Failed to parse simple triple quoted string"
    );

    let empty_triple = "\"\"\"\"\"\"";
    let mut lexer = SharpyLexer::new(empty_triple);
    let result = lexer.tokenize_all();
    assert!(result.is_ok(), "Failed to parse empty triple quoted string");
}

#[test]
fn test_single_triple_quotes() {
    let simple_single_triple = "'''Simple single triple quotes'''";
    let mut lexer = SharpyLexer::new(simple_single_triple);
    let result = lexer.tokenize_all();
    assert!(
        result.is_ok(),
        "Failed to parse simple single triple quoted string"
    );

    let empty_single_triple = "''''''";
    let mut lexer = SharpyLexer::new(empty_single_triple);
    let result = lexer.tokenize_all();
    assert!(
        result.is_ok(),
        "Failed to parse empty single triple quoted string"
    );
}

#[test]
fn test_multiline_with_actual_newlines() {
    // Test a string that actually spans multiple lines
    let multiline_content = "\"\"\"Line 1\nLine 2\nLine 3\"\"\"";
    let mut lexer = SharpyLexer::new(multiline_content);
    let result = lexer.tokenize_all();
    assert!(
        result.is_ok(),
        "Failed to parse multiline string with newlines"
    );

    let tokens = result.unwrap();
    // Should have at least string and EOF tokens
    assert!(tokens.len() >= 2);

    // First token should be a string
    if let TokenType::String(_) = &tokens[0].token_type {
        // Good
    } else {
        panic!("Expected string token, got: {:?}", tokens[0].token_type);
    }
}

#[test]
fn test_unterminated_strings_should_fail() {
    let bad_cases = vec![
        "\"\"\"Unterminated",
        "'''Also unterminated",
        "\"\"\"Two quotes only\"\"",
        "'''Two quotes only''",
    ];

    for case in bad_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_err(),
            "Should fail for unterminated string: {}",
            case
        );
    }
}

#[test]
fn test_comments_and_strings_together() {
    let combined = "x = \"\"\"string\"\"\" # comment";
    let mut lexer = SharpyLexer::new(combined);
    let result = lexer.tokenize_all();
    assert!(result.is_ok(), "Failed to parse string with comment");

    // Test comment before string
    let comment_first = "# comment\nx = \"\"\"string\"\"\"";
    let mut lexer = SharpyLexer::new(comment_first);
    let result = lexer.tokenize_all();
    assert!(result.is_ok(), "Failed to parse comment before string");
}

#[test]
fn test_string_content_edge_cases() {
    // Test strings with quotes inside
    let with_inner_quotes = "\"\"\"String with \"inner\" quotes\"\"\"";
    let mut lexer = SharpyLexer::new(with_inner_quotes);
    let result = lexer.tokenize_all();
    assert!(result.is_ok(), "Failed to parse string with inner quotes");

    // Test mixed quote types
    let mixed_quotes = "\"\"\"String with 'single' quotes\"\"\"";
    let mut lexer = SharpyLexer::new(mixed_quotes);
    let result = lexer.tokenize_all();
    assert!(result.is_ok(), "Failed to parse string with mixed quotes");
}
