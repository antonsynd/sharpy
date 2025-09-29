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
fn test_longer_name() {
    let code = "xyz";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Name(NameType {
                    name: "xyz".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 3
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 4,
                    start: 3,
                    end: 4
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_literal_name() {
    let code = "`xyz`";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Name(NameType {
                    name: "xyz".to_string(),
                    is_literal: true,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 6,
                    start: 5,
                    end: 6
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_protected_name() {
    let code = "_xyz";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Name(NameType {
                    name: "xyz".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Protected
                }),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 4
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_private_name() {
    let code = "__xyz";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Name(NameType {
                    name: "xyz".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Private
                }),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 6,
                    start: 5,
                    end: 6
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_internal_name() {
    // Test that the old $ prefix syntax is no longer supported
    let code = "$xyz";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    // Should fail because $ is not a valid identifier character
    assert!(result.is_err());
}

#[test]
fn test_file_name() {
    // Test that the old $$ prefix syntax is no longer supported
    let code = "$$xyz";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    // Should fail because $ is not a valid identifier character
    assert!(result.is_err());
}

#[test]
fn test_complex_name() {
    let code = "__`__xyz`";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Name(NameType {
                    name: "__xyz".to_string(),
                    is_literal: true,
                    access_modifier: AccessModifier::Private
                }),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 9
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 10
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_incomplete_literal_name() {
    // Test that a backtick without closing backtick is treated as regular identifier
    let code = "`xyz";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Name(NameType {
                    name: "xyz".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 4
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
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
    assert_eq!(result.as_ref().unwrap().len(), 6);
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
                token_type: TokenType::Equal,
                location: SourceLocation {
                    line: 1,
                    column: 8,
                    start: 7,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("3".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 10
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 11,
                    start: 10,
                    end: 11
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_none() {
    let code = "None";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::None,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 4
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_true() {
    let code = "True";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::True,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 4
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_false() {
    let code = "False";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::False,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 6,
                    start: 5,
                    end: 6
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_ellipsis() {
    let code = "...";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Ellipsis,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 3
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 4,
                    start: 3,
                    end: 4
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_pass() {
    let code = "pass";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Pass,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 4
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_integer() {
    let code = "42";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Number(NumberType::Integer("42".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 3
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_float() {
    let code = "3.14";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Number(NumberType::Float("3.14".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 4
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_imaginary() {
    let code = "1j+23";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 4);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Number(NumberType::Imaginary("1j".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Plus,
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 3
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("23".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 4,
                    start: 3,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 6,
                    start: 5,
                    end: 6
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_imaginary_complex_expression() {
    let code = "3j-4";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 4);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Number(NumberType::Imaginary("3j".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Minus,
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 3
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("4".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 4,
                    start: 3,
                    end: 4
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_imaginary_float() {
    let code = "3.14j";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Number(NumberType::Imaginary("3.14j".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 6,
                    start: 5,
                    end: 6
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_imaginary_capital_j() {
    let code = "2J";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Number(NumberType::Imaginary("2J".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 3
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_imaginary_scientific_notation() {
    let code = "1.5e2j";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Number(NumberType::Imaginary("1.5e2j".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
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
fn test_empty_list_literal() {
    let code = "[]";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 3);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::LeftBracket,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 1
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightBracket,
                location: SourceLocation {
                    line: 1,
                    column: 2,
                    start: 1,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 3
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_list_literal() {
    let code = "[1, 2, 3]";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 8);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::LeftBracket,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 1
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("1".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 2,
                    start: 1,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Comma,
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 3
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("2".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Comma,
                location: SourceLocation {
                    line: 1,
                    column: 6,
                    start: 5,
                    end: 6
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("3".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 8,
                    start: 7,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightBracket,
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 9
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 10
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_tuple_literal() {
    let code = "(1, 2, 3)";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 8);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::LeftParen,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 1
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("1".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 2,
                    start: 1,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Comma,
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 3
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("2".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Comma,
                location: SourceLocation {
                    line: 1,
                    column: 6,
                    start: 5,
                    end: 6
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("3".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 8,
                    start: 7,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightParen,
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 9
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 10
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_set_literal() {
    let code = "{1, 2, 3}";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 8);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::LeftBrace,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 1
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("1".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 2,
                    start: 1,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Comma,
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 3
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("2".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Comma,
                location: SourceLocation {
                    line: 1,
                    column: 6,
                    start: 5,
                    end: 6
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("3".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 8,
                    start: 7,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightBrace,
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 9
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 10
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_dictionary_literal() {
    let code = r#"{"x": 10, "y": 20}"#;
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 10);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::LeftBrace,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 1
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::String(StringType::Regular("x".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 2,
                    start: 1,
                    end: 4
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("10".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 7,
                    start: 6,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Comma,
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 9
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::String(StringType::Regular("y".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 11,
                    start: 10,
                    end: 13
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 14,
                    start: 13,
                    end: 14
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("20".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 16,
                    start: 15,
                    end: 17
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightBrace,
                location: SourceLocation {
                    line: 1,
                    column: 18,
                    start: 17,
                    end: 18
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 19,
                    start: 18,
                    end: 19
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_double_quoted_string_literal() {
    let code = r#""Hello, World!""#;
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::String(StringType::Regular("Hello, World!".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 15
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 16,
                    start: 15,
                    end: 16
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_single_quoted_string_literal() {
    let code = r"'Hello, World!'";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 2);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::String(StringType::Regular("Hello, World!".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 15
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 16,
                    start: 15,
                    end: 16
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_sequential_string_literals() {
    let code = r#""Hello" "World!""#;
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 3);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::String(StringType::Regular("Hello".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 7
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::String(StringType::Regular("World!".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 16
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 17,
                    start: 16,
                    end: 17
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_class_definition() {
    let code = "class Person:";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 4);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Class,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "Person".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 7,
                    start: 6,
                    end: 12
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 13,
                    start: 12,
                    end: 13
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 14,
                    start: 13,
                    end: 14
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_class_inheritance() {
    let code = "class Student(Person):";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 7);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Class,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "Student".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 7,
                    start: 6,
                    end: 13
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::LeftParen,
                location: SourceLocation {
                    line: 1,
                    column: 14,
                    start: 13,
                    end: 14
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "Person".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 15,
                    start: 14,
                    end: 20
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightParen,
                location: SourceLocation {
                    line: 1,
                    column: 21,
                    start: 20,
                    end: 21
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 22,
                    start: 21,
                    end: 22
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 23,
                    start: 22,
                    end: 23
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_struct_definition() {
    let code = "struct Point:";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 4);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Struct,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 6
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "Point".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 8,
                    start: 7,
                    end: 12
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 13,
                    start: 12,
                    end: 13
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 14,
                    start: 13,
                    end: 14
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_function_definition() {
    let code = "def add(x: int, y: int) -> int:";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 15);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Def,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 3
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "add".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 7
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::LeftParen,
                location: SourceLocation {
                    line: 1,
                    column: 8,
                    start: 7,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "x".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 9
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 10
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
                    column: 12,
                    start: 11,
                    end: 14
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Comma,
                location: SourceLocation {
                    line: 1,
                    column: 15,
                    start: 14,
                    end: 15
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "y".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 17,
                    start: 16,
                    end: 17
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 18,
                    start: 17,
                    end: 18
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
                    column: 20,
                    start: 19,
                    end: 22
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightParen,
                location: SourceLocation {
                    line: 1,
                    column: 23,
                    start: 22,
                    end: 23
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RArrow,
                location: SourceLocation {
                    line: 1,
                    column: 25,
                    start: 24,
                    end: 26
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
                    column: 28,
                    start: 27,
                    end: 30
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 31,
                    start: 30,
                    end: 31
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 32,
                    start: 31,
                    end: 32
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_function_call() {
    let code = "result = add(5, 10)";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 9);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Name(NameType {
                    name: "result".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 6
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Equal,
                location: SourceLocation {
                    line: 1,
                    column: 8,
                    start: 7,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "add".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 12
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::LeftParen,
                location: SourceLocation {
                    line: 1,
                    column: 13,
                    start: 12,
                    end: 13
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("5".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 14,
                    start: 13,
                    end: 14
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Comma,
                location: SourceLocation {
                    line: 1,
                    column: 15,
                    start: 14,
                    end: 15
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("10".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 17,
                    start: 16,
                    end: 18
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightParen,
                location: SourceLocation {
                    line: 1,
                    column: 19,
                    start: 18,
                    end: 19
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 20,
                    start: 19,
                    end: 20
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_if_statement() {
    let code = "if x > 0:";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 6);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::If,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "x".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 4,
                    start: 3,
                    end: 4
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Greater,
                location: SourceLocation {
                    line: 1,
                    column: 6,
                    start: 5,
                    end: 6
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Number(NumberType::Integer("0".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 8,
                    start: 7,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 9
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 10
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_for_loop() {
    let code = "for item in items:";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 6);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::For,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 3
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "item".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 5,
                    start: 4,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::In,
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 11
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "items".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 13,
                    start: 12,
                    end: 17
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 18,
                    start: 17,
                    end: 18
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 19,
                    start: 18,
                    end: 19
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_while_loop() {
    let code = "while condition:";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 4);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::While,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 5
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "condition".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 7,
                    start: 6,
                    end: 15
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 16,
                    start: 15,
                    end: 16
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 17,
                    start: 16,
                    end: 17
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_lambda_expression() {
    let code = "lambda x, y: x + y";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 9);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::Lambda,
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 6
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "x".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 8,
                    start: 7,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Comma,
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 9
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "y".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 11,
                    start: 10,
                    end: 11
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Colon,
                location: SourceLocation {
                    line: 1,
                    column: 12,
                    start: 11,
                    end: 12
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "x".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 14,
                    start: 13,
                    end: 14
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Plus,
                location: SourceLocation {
                    line: 1,
                    column: 16,
                    start: 15,
                    end: 16
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "y".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 18,
                    start: 17,
                    end: 18
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 19,
                    start: 18,
                    end: 19
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_fstring_simple() {
    let code = r#"f"hello {name}""#;
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 7);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::FString(FStringPart::Start("f\"".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::FString(FStringPart::Middle("hello ".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::LeftBrace,
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 9
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "name".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 13
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightBrace,
                location: SourceLocation {
                    line: 1,
                    column: 14,
                    start: 13,
                    end: 14
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::FString(FStringPart::End("\"".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 15,
                    start: 14,
                    end: 15
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 16,
                    start: 15,
                    end: 16
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_fstring_with_nested_string() {
    let code = r#"f"text {'inner'} more""#;
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 8);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::FString(FStringPart::Start("f\"".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::FString(FStringPart::Middle("text ".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 7
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::LeftBrace,
                location: SourceLocation {
                    line: 1,
                    column: 8,
                    start: 7,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::String(StringType::Regular("inner".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 15
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightBrace,
                location: SourceLocation {
                    line: 1,
                    column: 16,
                    start: 15,
                    end: 16
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::FString(FStringPart::Middle(" more".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 17,
                    start: 16,
                    end: 21
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::FString(FStringPart::End("\"".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 22,
                    start: 21,
                    end: 22
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 23,
                    start: 22,
                    end: 23
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_fstring_single_quotes() {
    let code = r"f'hello {world}'";
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert_eq!(result.as_ref().unwrap().len(), 7);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::FString(FStringPart::Start("f'".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::FString(FStringPart::Middle("hello ".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 8
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::LeftBrace,
                location: SourceLocation {
                    line: 1,
                    column: 9,
                    start: 8,
                    end: 9
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Name(NameType {
                    name: "world".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public
                }),
                location: SourceLocation {
                    line: 1,
                    column: 10,
                    start: 9,
                    end: 14
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::RightBrace,
                location: SourceLocation {
                    line: 1,
                    column: 15,
                    start: 14,
                    end: 15
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::FString(FStringPart::End("'".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 16,
                    start: 15,
                    end: 16
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 17,
                    start: 16,
                    end: 17
                },
                channel: Channel::Default
            }
        ]
    );
}

#[test]
fn test_fstring_no_expressions() {
    let code = r#"f"just text""#;
    let mut lexer = SharpyLexer::new(code);

    let result = lexer.tokenize_all();
    assert!(result.is_ok());
    assert_eq!(result.as_ref().unwrap().len(), 4);
    assert_eq!(
        result.unwrap(),
        vec![
            Token {
                token_type: TokenType::FString(FStringPart::Start("f\"".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 1,
                    start: 0,
                    end: 2
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::FString(FStringPart::Middle("just text".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 3,
                    start: 2,
                    end: 11
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::FString(FStringPart::End("\"".to_string())),
                location: SourceLocation {
                    line: 1,
                    column: 12,
                    start: 11,
                    end: 12
                },
                channel: Channel::Default
            },
            Token {
                token_type: TokenType::Eof,
                location: SourceLocation {
                    line: 1,
                    column: 13,
                    start: 12,
                    end: 13
                },
                channel: Channel::Default
            }
        ]
    );
}
