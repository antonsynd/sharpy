use sharpy_compiler_toolchain::{SharpyLexer, TokenType};

#[test]
fn test_simple_comments() {
    let test_cases = vec![
        "# Simple comment",
        "x = 1 # Inline comment",
        "# Comment with special chars: @$%^&*()",
        "####### Multiple hashes",
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(result.is_ok(), "Failed to parse comment: {}", case);

        let tokens = result.unwrap();
        // Should have at least an EOF token
        assert!(!tokens.is_empty());

        // Check if comment token exists (may be filtered out)
        let has_comment = tokens
            .iter()
            .any(|t| matches!(t.token_type, TokenType::Comment(_)));
        println!("Case: {} - Has comment token: {}", case, has_comment);
    }
}

#[test]
fn test_comment_edge_cases() {
    let test_cases = vec![
        ("x = 1#", "Empty comment"),
        ("# Comment at end", "Comment at end of file"),
        (
            "x = \"string\" # Comment after string",
            "Comment after string literal",
        ),
    ];

    for (case, description) in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(result.is_ok(), "Failed to parse {}: {}", description, case);
    }
}

#[test]
fn test_multiline_strings_basic() {
    let test_cases = vec![
        r#""""Simple triple quoted string""""#,
        r#"'''Single triple quotes'''#,
        r#""""""""#, // Empty triple quoted
        r#"''''''"#, // Empty triple single quoted (6 quotes = two empty strings)
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(result.is_ok(), "Failed to parse multiline string: {}", case);

        let tokens = result.unwrap();
        assert!(!tokens.is_empty());

        // Should have a string token
        let has_string = tokens
            .iter()
            .any(|t| matches!(t.token_type, TokenType::String(_)));
        assert!(has_string, "No string token found for: {}", case);
    }
}

#[test]
fn test_multiline_strings_with_newlines() {
    let multiline_cases = vec![
        "\"\"\"
Multiline string
with newlines
\"\"\"",
        "'''
Multiline with
single quotes
'''",
        "\"\"\"
    Indented
        content
\"\"\"",
    ];

    for case in multiline_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Failed to parse multiline string with newlines: {:?}",
            case
        );

        let tokens = result.unwrap();
        assert!(!tokens.is_empty());

        // Should have a string token
        let has_string = tokens
            .iter()
            .any(|t| matches!(t.token_type, TokenType::String(_)));
        assert!(has_string, "No string token found for multiline string");
    }
}

#[test]
fn test_multiline_strings_with_quotes() {
    let test_cases = vec![
        "\"\"\"Triple quoted with \"inner quotes\" \"\"\"",
        "\"\"\"Triple quoted with 'mixed quotes' \"\"\"",
        "'''Triple single with \"double quotes\" '''",
        "'''Triple single with 'inner singles' '''",
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Failed to parse multiline string with inner quotes: {}",
            case
        );

        let tokens = result.unwrap();
        assert!(!tokens.is_empty());

        // Should have a string token
        let has_string = tokens
            .iter()
            .any(|t| matches!(t.token_type, TokenType::String(_)));
        assert!(has_string, "No string token found for: {}", case);
    }
}

#[test]
fn test_unterminated_multiline_strings() {
    let bad_cases = vec![
        "\"\"\"Unterminated triple quote",
        "'''Unterminated single triple",
        "\"\"\"Only two quotes\"\"",
        "'''Only two quotes''",
    ];

    for case in bad_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_err(),
            "Should fail for unterminated multiline string: {}",
            case
        );
    }
}

#[test]
fn test_comments_with_multiline_strings() {
    let case1 = r#""""
String with content
"""  # Comment after multiline string"#;

    let case2 = r#"# Comment before
"""
Multiline string
""""#;

    let case3 = r#"x = """
Value
"""  # Inline comment"#;

    let combined_cases = vec![case1, case2, case3];

    for case in combined_cases {
        let mut lexer = SharpyLexer::new(case);
        let result = lexer.tokenize_all();
        assert!(
            result.is_ok(),
            "Failed to parse combined comments and multiline strings: {:?}",
            case
        );
    }
}
