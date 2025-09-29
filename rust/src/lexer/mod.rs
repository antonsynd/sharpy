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
use string_lexer::StringLexer;
pub use token::*;

pub struct SharpyLexer<'a> {
    scanner: Scanner<'a>,
    indent_handler: IndentationHandler,
    string_lexer: StringLexer,
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
            string_lexer: StringLexer::new(),
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
            let current_pos = self.scanner.position();
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
                    // Ensure we advance past the problematic character to avoid infinite loops
                    if self.scanner.position() == current_pos {
                        if self.scanner.current_char().is_some() {
                            self.scanner.advance();
                        } else {
                            // We're at EOF, create an EOF token and break
                            let location = self.scanner.current_location();
                            tokens.push(Token::new(TokenType::Eof, location));
                            break;
                        }
                    }
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
        // Handle f-string continuation if we're in f-string mode
        if self.string_lexer.is_in_fstring() {
            return self.handle_fstring_continuation();
        }

        // Skip whitespace at the beginning of a line for indentation
        if self.at_line_start {
            let whitespace = self.scanner.skip_whitespace();
            self.at_line_start = false; // Reset flag immediately
            // Always handle indentation at line start to check for dedents
            if !self.is_at_eof() && !self.is_comment_or_newline() {
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
        } else {
            // Skip whitespace in the middle of lines (allows tabs in expressions)
            self.scanner.skip_expression_whitespace();
        }

        match self.scanner.current_char() {
            None => {
                // Handle EOF
                let location = self.scanner.current_location();
                let eof_tokens = self.indent_handler.handle_eof(&location);
                for token in eof_tokens {
                    self.pending_tokens.push_back(token);
                }

                self.pending_tokens
                    .pop_front()
                    .map_or_else(|| Ok(Token::new(TokenType::Eof, location)), Ok)
            }
            Some('\n') => self.handle_newline(),
            Some('#') => Ok(self.scanner.scan_comment()),
            Some(ch) if ch.is_ascii_digit() => self.scanner.scan_number(),
            Some('.') if self.scanner.peek_char().is_some_and(|c| c.is_ascii_digit()) => {
                // Float starting with decimal point (.5, .123, etc.)
                self.scanner.scan_number()
            }
            Some(ch) if is_string_start(ch) => self.scan_string(),
            Some(ch) if Self::could_be_string_with_prefix(ch) => {
                // Check if it's actually a string with prefix or just an identifier
                if self.scanner.is_string_prefix_followed_by_quote() {
                    self.scan_string()
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
        self.scanner.current_char().is_none()
    }

    const fn is_comment_or_newline(&self) -> bool {
        matches!(self.scanner.current_char(), Some('#' | '\n'))
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

    fn handle_fstring_continuation(&mut self) -> Result<Token, LexerError> {
        // Skip whitespace within f-strings (but preserve for content)
        if self.string_lexer.is_in_fstring_expression() {
            // We're in an expression within an f-string
            match self.scanner.current_char() {
                Some('}') => {
                    // End of expression
                    self.string_lexer.exit_fstring_expression();
                    let location = self.scanner.current_location();
                    self.scanner.advance();
                    return Ok(Token::new(TokenType::RightBrace, location));
                }
                Some('{') => {
                    // Nested braces
                    self.string_lexer.enter_fstring_expression();
                    let location = self.scanner.current_location();
                    self.scanner.advance();
                    return Ok(Token::new(TokenType::LeftBrace, location));
                }
                _ => {
                    // Regular expression token - fall through to normal lexing
                }
            }
        } else {
            // We're in f-string content, not in an expression
            match self.scanner.current_char() {
                Some('{') => {
                    // Start of expression
                    self.string_lexer.enter_fstring_expression();
                    let location = self.scanner.current_location();
                    self.scanner.advance();
                    return Ok(Token::new(TokenType::LeftBrace, location));
                }
                Some('"' | '\'') => {
                    // Potential end of f-string
                    if self.is_fstring_end() {
                        let token = self.string_lexer.scan_fstring_end(&mut self.scanner)?;
                        self.string_lexer.end_fstring();
                        return Ok(token);
                    }
                    // Part of f-string content
                    return self.string_lexer.scan_fstring_middle(&mut self.scanner);
                }
                Some(_) => {
                    // F-string content
                    return self.string_lexer.scan_fstring_middle(&mut self.scanner);
                }
                None => {
                    return Err(LexerError::UnterminatedString);
                }
            }
        }

        // If we get here, we're in an expression - use normal lexing but skip whitespace differently
        self.scanner.skip_expression_whitespace();

        match self.scanner.current_char() {
            None => Err(LexerError::UnterminatedString),
            Some('\n') => self.handle_newline(),
            Some('#') => Ok(self.scanner.scan_comment()),
            Some(ch) if ch.is_ascii_digit() => self.scanner.scan_number(),
            Some(ch) if is_string_start(ch) => StringLexer::scan_string(&mut self.scanner),
            Some(ch) if Self::could_be_string_with_prefix(ch) => {
                if self.scanner.is_string_prefix_followed_by_quote() {
                    StringLexer::scan_string(&mut self.scanner)
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

    fn is_fstring_end(&mut self) -> bool {
        // Check if the current quote character matches the f-string terminator
        if let Some(mode) = self.string_lexer.current_fstring_mode() {
            let (quote_char, is_triple) =
                match mode {
                    string_lexer::LexerMode::FStringSQ1
                    | string_lexer::LexerMode::FStringSQ1Raw => ('\'', false),
                    string_lexer::LexerMode::FStringDQ1
                    | string_lexer::LexerMode::FStringDQ1Raw => ('"', false),
                    string_lexer::LexerMode::FStringSQ3
                    | string_lexer::LexerMode::FStringSQ3Raw => ('\'', true),
                    string_lexer::LexerMode::FStringDQ3
                    | string_lexer::LexerMode::FStringDQ3Raw => ('"', true),
                    _ => return false,
                };

            if let Some(current_char) = self.scanner.current_char() {
                if current_char == quote_char {
                    if is_triple {
                        // Check for triple quote
                        let peek_next = self.scanner.peek_next_chars(2);
                        peek_next.len() >= 2
                            && peek_next.chars().nth(0) == Some(quote_char)
                            && peek_next.chars().nth(1) == Some(quote_char)
                    } else {
                        true
                    }
                } else {
                    false
                }
            } else {
                false
            }
        } else {
            false
        }
    }

    fn scan_string(&mut self) -> Result<Token, LexerError> {
        // First, peek at the prefix without consuming it
        let prefix = self.peek_string_prefix();

        // Determine if it's an f-string
        if prefix.to_lowercase().contains('f') {
            let token = StringLexer::scan_string(&mut self.scanner)?;
            // After creating the f-string start token, set up the state
            if let TokenType::FString(FStringPart::Start(_)) = &token.token_type {
                // We need to get the quote style to set up the mode properly
                // Since scan_string already consumed the prefix and quote, we need to reconstruct the info
                let quote_style = SharpyLexer::determine_quote_style_from_start_token(&token);
                let is_triple = quote_style.len() == 3;
                self.string_lexer
                    .start_fstring(&prefix, &quote_style, is_triple);
            }
            Ok(token)
        } else {
            // Regular string - use static method
            StringLexer::scan_string(&mut self.scanner)
        }
    }

    fn peek_string_prefix(&mut self) -> String {
        let mut prefix = String::new();

        // First, check the current character
        if let Some(current_ch) = self.scanner.current_char() {
            match current_ch.to_ascii_lowercase() {
                'r' | 'u' | 'b' | 'f' => {
                    prefix.push(current_ch);
                }
                _ => {
                    return prefix;
                }
            }
        }

        // Then look ahead for more prefix characters
        let lookahead = self.scanner.peek_next_chars(3); // Look ahead for 3 more characters

        for ch in lookahead.chars() {
            match ch.to_ascii_lowercase() {
                'r' | 'u' | 'b' | 'f' => {
                    prefix.push(ch);
                }
                _ => break,
            }
        }

        prefix
    }
    fn determine_quote_style_from_start_token(token: &Token) -> String {
        if let TokenType::FString(FStringPart::Start(lexeme)) = &token.token_type {
            // Extract the quote style from the lexeme (e.g., f" -> ", f''' -> ''')
            if lexeme.ends_with(r#"""""#) {
                r#"""""#.to_string()
            } else if lexeme.ends_with("'''") {
                "'''".to_string()
            } else if lexeme.ends_with('"') {
                "\"".to_string()
            } else if lexeme.ends_with('\'') {
                "'".to_string()
            } else {
                "\"".to_string() // default
            }
        } else {
            "\"".to_string() // default
        }
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
        }

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
        let mut lexer = SharpyLexer::new("_protected __private public internal");

        let tokens = lexer.tokenize_all().unwrap();

        // Filter out EOF and other non-content tokens
        let content_tokens: Vec<_> = tokens
            .iter()
            .filter(|t| !matches!(t.token_type, TokenType::Eof | TokenType::Newline))
            .collect();

        // Check that we get the expected tokens
        assert_eq!(content_tokens.len(), 4);

        // _protected should be a name with Protected access modifier
        if let TokenType::Name(name) = &content_tokens[0].token_type {
            assert_eq!(name.name, "protected");
            assert_eq!(
                name.access_modifier,
                crate::lexer::token::AccessModifier::Protected
            );
            assert!(!name.is_literal);
        } else {
            panic!(
                "Expected Name token for _protected, got {:?}",
                content_tokens[0].token_type
            );
        }

        // __private should be a name with Private access modifier
        if let TokenType::Name(name) = &content_tokens[1].token_type {
            assert_eq!(name.name, "private");
            assert_eq!(
                name.access_modifier,
                crate::lexer::token::AccessModifier::Private
            );
            assert!(!name.is_literal);
        } else {
            panic!("Expected Name token for __private");
        }

        // public should be a regular name token (for use in decorators)
        if let TokenType::Name(name) = &content_tokens[2].token_type {
            assert_eq!(name.name, "public");
            assert_eq!(
                name.access_modifier,
                crate::lexer::token::AccessModifier::Public
            );
            assert!(!name.is_literal);
        } else {
            panic!(
                "Expected Name token for public, got {:?}",
                content_tokens[2].token_type
            );
        }

        // internal should be a regular name token (for use in decorators)
        if let TokenType::Name(name) = &content_tokens[3].token_type {
            assert_eq!(name.name, "internal");
            assert_eq!(
                name.access_modifier,
                crate::lexer::token::AccessModifier::Public
            );
            assert!(!name.is_literal);
        } else {
            panic!(
                "Expected Name token for internal, got {:?}",
                content_tokens[3].token_type
            );
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
                // TODO
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
