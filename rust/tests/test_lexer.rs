use sharpy_compiler_toolchain::lexer::SharpyLexer;
use sharpy_compiler_toolchain::lexer::token::*;
use sharpy_compiler_toolchain::utils::SourceLocation;

#[test]
fn test_name() {
    let code = "x";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Name(NameType {
                    name: "x".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 1
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 2,
                    start: 1,
                    end: 2
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_typed_name() {
    let code = "x: int";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 4);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Name(NameType {
                    name: "x".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 1
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 2,
                    start: 1,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "int".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 4,
                    start: 3,
                    end: 6
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 7,
                    start: 6,
                    end: 7
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_typed_assignment() {
    let code = "x: int = 3";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_none() {
    let code = "None";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_true() {
    let code = "True";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_false() {
    let code = "False";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_ellipsis() {
    let code = "...";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_pass() {
    let code = "pass";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_integer() {
    let code = "42";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_float() {
    let code = "3.14";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_imaginary() {
    let code = "1j+23";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_empty_list_literal() {
    let code = "[]";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_list_literal() {
    let code = "[1, 2, 3]";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_tuple_literal() {
    let code = "(1, 2, 3)";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_set_literal() {
    let code = "{1, 2, 3}";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_dictionary_literal() {
    let code = r#"{"x": 10, "y": 20}"#;
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_double_quoted_string_literal() {
    let code = r#""Hello, World!""#;
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_single_quoted_string_literal() {
    let code = r#"'Hello, World!'"#;
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}

#[test]
fn test_sequential_string_literals() {
    let code = r#"Hello" "World!""#;
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
}
