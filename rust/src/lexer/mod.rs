pub mod error;
pub mod indent;
pub mod keyword;
pub mod number_lexer;
pub mod scanner;
pub mod string_lexer;
pub mod token;

use crate::utils::is_id_start;
pub use error::LexerError;
use indent::IndentationHandler;
use scanner::Scanner;
use std::collections::VecDeque;
pub use token::*;

pub struct SharpyLexer<'a> {
    scanner: Scanner<'a>,
    indent_handler: IndentationHandler,
    pending_tokens: VecDeque<Token>,
    opened_parens: usize,
    at_line_start: bool,
    errors: Vec<LexerError>,
}

impl<'a> SharpyLexer<'a> {
    /// Creates a new lexer for the given input string.
    #[must_use]
    pub fn new(input: &'a str) -> Self {
        Self {
            scanner: Scanner::new(input),
            indent_handler: IndentationHandler::new(),
            pending_tokens: VecDeque::new(),
            opened_parens: 0,
            at_line_start: true,
            errors: Vec::new(),
        }
    }

    /// Gets the next token from the input.
    ///
    /// # Errors
    /// Returns a lexer error if the input contains invalid syntax.
    pub fn next_token(&mut self) -> Result<Token, LexerError> {
        // Return pending tokens first
        if let Some(token) = self.pending_tokens.pop_front() {
            return Ok(token);
        }

        self.scan_next_token()
    }

    /// Tokenizes the entire input and returns all tokens.
    ///
    /// # Errors
    /// Returns a vector of lexer errors if the input contains invalid syntax.
    pub fn tokenize_all(&mut self) -> Result<Vec<Token>, Vec<LexerError>> {
        let mut tokens = Vec::new();

        loop {
            match self.next_token() {
                Ok(token) => {
                    let is_eof = matches!(token.token_type, TokenType::Eof);
                    tokens.push(token);
                    if is_eof {
                        break;
                    }
                }
                Err(err) => {
                    self.errors.push(err);
                }
            }
        }

        if self.errors.is_empty() {
            Ok(tokens)
        } else {
            Err(self.errors.clone())
        }
    }

    fn scan_next_token(&mut self) -> Result<Token, LexerError> {
        // Skip whitespace at the beginning of a line for indentation
        if self.at_line_start {
            let whitespace = self.scanner.skip_whitespace();
            if !whitespace.is_empty() && !self.is_at_eof() && !self.is_comment_or_newline() {
                // Handle indentation
                let location = self.scanner.current_location();
                let indent_tokens = self
                    .indent_handler
                    .handle_indentation(&whitespace, location)?;
                for token in indent_tokens {
                    self.pending_tokens.push_back(token);
                }
                if let Some(token) = self.pending_tokens.pop_front() {
                    return Ok(token);
                }
            }
            self.at_line_start = false;
        } else {
            // Skip whitespace in the middle of lines (allows tabs in expressions)
            self.scanner.skip_expression_whitespace();
        }

        match self.scanner.current_char {
            None => {
                // Handle EOF
                let location = self.scanner.current_location();
                let eof_tokens = self.indent_handler.handle_eof(&location);
                for token in eof_tokens {
                    self.pending_tokens.push_back(token);
                }

                self.pending_tokens.pop_front().map_or_else(
                    || Ok(Token::new(TokenType::Eof, String::new(), location)),
                    Ok,
                )
            }
            Some('\n') => self.handle_newline(),
            Some('#') => Ok(self.scanner.scan_comment()),
            Some(ch) if ch.is_ascii_digit() => self.scanner.scan_number(),
            Some(ch) if is_string_start(ch) => self.scanner.scan_string(),
            Some(ch) if Self::could_be_string_with_prefix(ch) => {
                // Check if it's actually a string with prefix or just an identifier
                if self.scanner.is_string_prefix_followed_by_quote() {
                    self.scanner.scan_string()
                } else {
                    self.scanner.scan_identifier()
                }
            }
            Some(ch) if is_id_start(ch) || ch == '_' || ch == '`' || ch == '$' => {
                self.scanner.scan_identifier()
            }
            Some(_) => self.scanner.scan_operator(),
        }
    }

    fn handle_newline(&mut self) -> Result<Token, LexerError> {
        let location = self.scanner.current_location();
        self.scanner.advance(); // consume '\n'
        self.at_line_start = true;

        let newline_tokens = self
            .indent_handler
            .handle_newline(self.opened_parens, location)?;

        for token in newline_tokens {
            self.pending_tokens.push_back(token);
        }

        self.pending_tokens
            .pop_front()
            .map_or_else(|| self.scan_next_token(), Ok)
    }

    const fn is_at_eof(&self) -> bool {
        self.scanner.current_char.is_none()
    }

    const fn is_comment_or_newline(&self) -> bool {
        matches!(self.scanner.current_char, Some('#' | '\n'))
    }

    const fn could_be_string_with_prefix(ch: char) -> bool {
        matches!(ch, 'r' | 'R' | 'b' | 'B' | 'f' | 'F' | 'u' | 'U')
    }

    /// Returns accumulated lexer errors.
    #[must_use]
    pub fn get_errors(&self) -> &[LexerError] {
        &self.errors
    }

    pub fn reset(&mut self) {
        self.pending_tokens.clear();
        self.indent_handler.reset();
        self.opened_parens = 0;
        self.at_line_start = true;
        self.errors.clear();
    }
}

const fn is_string_start(ch: char) -> bool {
    // Only direct quote characters are definitely string starts
    // String prefixes need lookahead to confirm they're followed by quotes
    matches!(ch, '"' | '\'')
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_basic_tokens() {
        let mut lexer = SharpyLexer::new("x = 42");

        let token1 = lexer.next_token().unwrap();
        if let TokenType::Name(name) = token1.token_type {
            assert_eq!(
                name,
                NameType {
                    name: "x".to_string(),
                    is_literal: false,
                    access_modifier: AccessModifier::Public,
                }
            );
        } else {
            panic!("Expected Name token, found {:?}", token1.token_type);
        };

        let token2 = lexer.next_token().unwrap();
        assert_eq!(token2.token_type, TokenType::Equal);

        let token3 = lexer.next_token().unwrap();
        assert!(matches!(token3.token_type, TokenType::Number(_)));

        let token4 = lexer.next_token().unwrap();
        assert_eq!(token4.token_type, TokenType::Eof);
    }

    #[test]
    fn test_keywords() {
        let mut lexer = SharpyLexer::new("if True else False");

        let tokens = lexer.tokenize_all().unwrap();
        assert_eq!(tokens[0].token_type, TokenType::If);
        assert_eq!(tokens[1].token_type, TokenType::True);
        assert_eq!(tokens[2].token_type, TokenType::Else);
        assert_eq!(tokens[3].token_type, TokenType::False);
    }

    #[test]
    fn test_access_modifiers() {
        let mut lexer = SharpyLexer::new("_protected __private $internal $file");

        let tokens = lexer.tokenize_all().unwrap();
        for token in &tokens {
            if let TokenType::Name(name) = &token.token_type {
                // Access modifiers are included in the lexeme
                assert!(token.lexeme.starts_with('_') || token.lexeme.starts_with('$'));
                assert_eq!(name.is_literal, false);
            }
        }
    }

    #[test]
    fn test_operators() {
        let mut lexer = SharpyLexer::new("?. ?? + - * / ** // = == != < > <= >=");

        let tokens = lexer.tokenize_all().unwrap();
        assert_eq!(tokens[0].token_type, TokenType::QuestionDot);
        assert_eq!(tokens[1].token_type, TokenType::DoubleQuestion);
        assert_eq!(tokens[2].token_type, TokenType::Plus);
        assert_eq!(tokens[3].token_type, TokenType::Minus);
    }

    #[test]
    fn test_numbers() {
        let mut lexer = SharpyLexer::new("42 3.14 2.5e10 0b1010 0o755 0xFF 1j");

        let tokens = lexer.tokenize_all().unwrap();
        for token in &tokens {
            if let TokenType::Number(_) = token.token_type {
                assert!(!token.lexeme.is_empty());
            }
        }
    }

    #[test]
    fn test_indentation() {
        let mut lexer = SharpyLexer::new("if True:\n    x = 1\nelse:\n    y = 2");

        let tokens = lexer.tokenize_all().unwrap();

        // Should find INDENT and DEDENT tokens
        let has_indent = tokens.iter().any(|t| t.token_type == TokenType::Indent);
        let has_dedent = tokens.iter().any(|t| t.token_type == TokenType::Dedent);

        assert!(has_indent);
        assert!(has_dedent);
    }
}
