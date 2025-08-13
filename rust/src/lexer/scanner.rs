use crate::lexer::{
    error::LexerError, keyword::KeywordMap, number_lexer::NumberLexer, string_lexer::StringLexer,
    token::*,
};
use crate::utils::{SourceLocation, is_id_continue, is_id_start};
use std::iter::Peekable;
use std::str::Chars;

pub struct Scanner<'a> {
    input: &'a str,
    chars: Peekable<Chars<'a>>,
    position: usize,
    line: usize,
    column: usize,
    pub current_char: Option<char>,
    keyword_map: KeywordMap,
}

impl<'a> Scanner<'a> {
    /// Creates a new scanner for the given input.
    #[must_use]
    pub fn new(input: &'a str) -> Self {
        let mut chars = input.chars().peekable();
        let current_char = chars.next();

        Self {
            input,
            chars,
            position: 0,
            line: 1,
            column: 1,
            current_char,
            keyword_map: KeywordMap::new(),
        }
    }

    pub fn advance(&mut self) {
        if let Some(ch) = self.current_char {
            self.position += ch.len_utf8();
            if ch == '\n' {
                self.line += 1;
                self.column = 1;
            } else {
                self.column += 1;
            }
        }
        self.current_char = self.chars.next();
    }

    pub fn peek_char(&mut self) -> Option<char> {
        self.chars.peek().copied()
    }

    pub fn peek_next_chars(&mut self, count: usize) -> String {
        let remaining: String = self.chars.clone().take(count).collect();
        remaining
    }

    /// Gets the current source location.
    #[must_use]
    pub const fn current_location(&self) -> SourceLocation {
        SourceLocation::single_char(self.line, self.column, self.position)
    }

    /// Creates a source location from a start position to the current position.
    #[must_use]
    pub const fn location_from_start(
        &self,
        start_line: usize,
        start_column: usize,
        start_pos: usize,
    ) -> SourceLocation {
        SourceLocation::new(start_line, start_column, start_pos, self.position)
    }

    pub fn skip_whitespace(&mut self) -> String {
        let mut whitespace = String::new();
        while let Some(ch) = self.current_char {
            if ch == ' ' || ch == '\x0C' {
                // space, form feed (no tabs allowed)
                whitespace.push(ch);
                self.advance();
            } else if ch == '\t' {
                // Tab found - this will be an error, but include it in the whitespace
                // for the indentation handler to process and report the error
                whitespace.push(ch);
                self.advance();
            } else {
                break;
            }
        }
        whitespace
    }

    /// Skips whitespace in non-indentation contexts (inside expressions).
    /// This allows tabs for compatibility in expressions, but they're still
    /// forbidden at the start of lines for indentation.
    pub fn skip_expression_whitespace(&mut self) {
        while let Some(ch) = self.current_char {
            if ch == ' ' || ch == '\t' || ch == '\x0C' {
                // Allow tabs in expressions
                self.advance();
            } else {
                break;
            }
        }
    }

    /// Scans an identifier token.
    ///
    /// # Errors
    /// Returns a lexer error if the identifier contains invalid Unicode.
    pub fn scan_identifier(&mut self) -> Result<Token, LexerError> {
        let start_pos = self.position;
        let start_line = self.line;
        let start_col = self.column;

        // Handle access modifiers and literal flag
        let access_modifier = self
            .scan_access_modifier()
            .unwrap_or(AccessModifier::Public);
        let literal_flag = self.scan_literal_flag();

        // Scan the actual identifier
        if !self.current_char.is_some_and(is_id_start) {
            return Err(LexerError::InvalidUnicodeIdentifier);
        }

        let mut identifier = String::new();
        while let Some(ch) = self.current_char {
            if is_id_continue(ch) {
                identifier.push(ch);
                self.advance();
            } else {
                break;
            }
        }

        // Create the full lexeme including modifiers
        let lexeme = self.input[start_pos..self.position].to_string();
        let location = self.location_from_start(start_line, start_col, start_pos);

        // Determine token type
        let token_type = if literal_flag {
            // Literal identifiers are never keywords
            TokenType::Name(NameType {
                name: identifier,
                literalness: NameLiteralness::Literal,
                access_modifier,
            })
        } else {
            self.keyword_or_identifier(&identifier, access_modifier)
        };

        Ok(Token::new(token_type, lexeme, location))
    }

    fn scan_access_modifier(&mut self) -> Option<AccessModifier> {
        match self.current_char {
            Some('$') => {
                if self.peek_char() == Some('$') {
                    self.advance(); // consume first $
                    self.advance(); // consume second $$
                    Some(AccessModifier::File)
                } else {
                    self.advance(); // consume $
                    Some(AccessModifier::Internal)
                }
            }
            Some('_') => {
                if self.peek_char() == Some('_') {
                    // Check if it's a dunder or just private
                    let peek_2 = self.peek_next_chars(2);
                    if peek_2.len() >= 2 && is_id_start(peek_2.chars().nth(1).unwrap()) {
                        self.advance(); // consume first _
                        self.advance(); // consume second _
                        Some(AccessModifier::Private)
                    } else if is_id_start('_') {
                        // Just protected
                        self.advance(); // consume _
                        Some(AccessModifier::Protected)
                    } else {
                        None
                    }
                } else if self.peek_char().is_some_and(is_id_start) {
                    self.advance(); // consume _
                    Some(AccessModifier::Protected)
                } else {
                    None
                }
            }
            _ => None,
        }
    }

    fn scan_literal_flag(&mut self) -> bool {
        if self.current_char == Some('`') {
            self.advance();
            true
        } else {
            false
        }
    }

    fn keyword_or_identifier(
        &self,
        identifier: &str,
        access_modifier: AccessModifier,
    ) -> TokenType {
        self.keyword_map.get_keyword(identifier).map_or_else(
            || {
                self.keyword_map.get_soft_keyword(identifier).map_or_else(
                    || {
                        TokenType::Name(NameType {
                            name: identifier.to_string(),
                            literalness: NameLiteralness::NotLiteral,
                            access_modifier,
                        })
                    },
                    std::clone::Clone::clone,
                )
            },
            std::clone::Clone::clone,
        )
    }

    /// Scans a number token.
    ///
    /// # Errors
    /// Returns a lexer error if the number format is invalid.
    pub fn scan_number(&mut self) -> Result<Token, LexerError> {
        let start_pos = self.position;
        let location = self.current_location();

        let (token, new_pos) = NumberLexer::scan_number(self.input, start_pos, location)?;

        // Update scanner position
        while self.position < new_pos {
            self.advance();
        }

        Ok(token)
    }

    /// Scans a string token.
    ///
    /// # Errors
    /// Returns a lexer error if the string is invalid or unterminated.
    pub fn scan_string(&mut self) -> Result<Token, LexerError> {
        let start_pos = self.position;
        let location = self.current_location();

        let (token, new_pos) = StringLexer::scan_string(self.input, start_pos, location)?;

        // Update scanner position
        while self.position < new_pos {
            self.advance();
        }

        Ok(token)
    }

    pub fn scan_comment(&mut self) -> Token {
        let start_pos = self.position;
        let start_line = self.line;
        let start_col = self.column;

        self.advance(); // Skip '#'

        let mut comment = String::new();
        while let Some(ch) = self.current_char {
            if ch == '\n' {
                break;
            }
            comment.push(ch);
            self.advance();
        }

        let lexeme = self.input[start_pos..self.position].to_string();
        let location = self.location_from_start(start_line, start_col, start_pos);

        Token::new(TokenType::Comment(comment), lexeme, location).with_channel(Channel::Hidden)
    }

    /// Scans an operator token.
    ///
    /// # Errors
    /// Returns a lexer error if an invalid character is encountered.
    ///
    /// # Panics
    /// Panics if called when `current_char` is None.
    pub fn scan_operator(&mut self) -> Result<Token, LexerError> {
        let start_pos = self.position;
        let start_line = self.line;
        let start_col = self.column;

        let ch = self.current_char.unwrap();
        let token_type = match ch {
            '+' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::PlusEqual
                } else {
                    TokenType::Plus
                }
            }
            '-' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::MinEqual
                } else if self.current_char == Some('>') {
                    self.advance();
                    TokenType::RArrow
                } else {
                    TokenType::Minus
                }
            }
            '*' => {
                self.advance();
                if self.current_char == Some('*') {
                    self.advance();
                    if self.current_char == Some('=') {
                        self.advance();
                        TokenType::DoubleStarEqual
                    } else {
                        TokenType::DoubleStar
                    }
                } else if self.current_char == Some('=') {
                    self.advance();
                    TokenType::StarEqual
                } else {
                    TokenType::Star
                }
            }
            '/' => {
                self.advance();
                if self.current_char == Some('/') {
                    self.advance();
                    if self.current_char == Some('=') {
                        self.advance();
                        TokenType::DoubleSlashEqual
                    } else {
                        TokenType::DoubleSlash
                    }
                } else if self.current_char == Some('=') {
                    self.advance();
                    TokenType::SlashEqual
                } else {
                    TokenType::Slash
                }
            }
            '%' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::PercentEqual
                } else {
                    TokenType::Percent
                }
            }
            '=' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::EqEqual
                } else {
                    TokenType::Equal
                }
            }
            '!' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::NotEqual
                } else {
                    TokenType::Exclamation
                }
            }
            '<' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::LessEqual
                } else if self.current_char == Some('<') {
                    self.advance();
                    if self.current_char == Some('=') {
                        self.advance();
                        TokenType::LeftShiftEqual
                    } else {
                        TokenType::LeftShift
                    }
                } else {
                    TokenType::Less
                }
            }
            '>' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::GreaterEqual
                } else if self.current_char == Some('>') {
                    self.advance();
                    if self.current_char == Some('=') {
                        self.advance();
                        TokenType::RightShiftEqual
                    } else {
                        TokenType::RightShift
                    }
                } else {
                    TokenType::Greater
                }
            }
            '&' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::AmperEqual
                } else {
                    TokenType::Amper
                }
            }
            '|' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::VbarEqual
                } else {
                    TokenType::Vbar
                }
            }
            '^' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::CircumflexEqual
                } else {
                    TokenType::Circumflex
                }
            }
            '~' => {
                self.advance();
                TokenType::Tilde
            }
            '@' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::AtEqual
                } else {
                    TokenType::At
                }
            }
            '?' => {
                self.advance();
                if self.current_char == Some('.') {
                    self.advance();
                    TokenType::QuestionDot
                } else if self.current_char == Some('?') {
                    self.advance();
                    TokenType::DoubleQuestion
                } else {
                    TokenType::Question
                }
            }
            '.' => {
                self.advance();
                if self.current_char == Some('.') && self.peek_char() == Some('.') {
                    self.advance();
                    self.advance();
                    TokenType::Ellipsis
                } else {
                    TokenType::Dot
                }
            }
            ':' => {
                self.advance();
                if self.current_char == Some('=') {
                    self.advance();
                    TokenType::ColonEqual
                } else {
                    TokenType::Colon
                }
            }
            '(' => {
                self.advance();
                TokenType::LeftParen
            }
            ')' => {
                self.advance();
                TokenType::RightParen
            }
            '[' => {
                self.advance();
                TokenType::LeftBracket
            }
            ']' => {
                self.advance();
                TokenType::RightBracket
            }
            '{' => {
                self.advance();
                TokenType::LeftBrace
            }
            '}' => {
                self.advance();
                TokenType::RightBrace
            }
            ',' => {
                self.advance();
                TokenType::Comma
            }
            ';' => {
                self.advance();
                TokenType::Semi
            }
            _ => return Err(LexerError::UnexpectedCharacter(ch)),
        };

        let lexeme = self.input[start_pos..self.position].to_string();
        let location = self.location_from_start(start_line, start_col, start_pos);

        Ok(Token::new(token_type, lexeme, location))
    }

    #[allow(clippy::unused_peekable)]
    pub fn is_string_prefix_followed_by_quote(&mut self) -> bool {
        // Look ahead to see if we have a string prefix followed by quotes
        let chars = self.chars.clone();
        let mut prefix_chars = 0;

        // Skip potential string prefix characters
        for ch in chars {
            match ch {
                'r' | 'R' | 'b' | 'B' | 'f' | 'F' | 'u' | 'U' => {
                    prefix_chars += 1;
                    // Prevent infinite lookahead
                    if prefix_chars > 3 {
                        return false;
                    }
                }
                '"' | '\'' => return true,
                _ => return false,
            }
        }
        false
    }
}
