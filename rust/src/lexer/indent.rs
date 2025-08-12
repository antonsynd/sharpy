use crate::lexer::{error::LexerError, token::*};
use crate::utils::SourceLocation;
use std::collections::VecDeque;
use std::cmp::Ordering;

pub struct IndentationHandler {
    indent_stack: Vec<usize>,
    pending_tokens: VecDeque<Token>,
    was_space_indentation: bool,
    was_tab_indentation: bool,
    was_mixed_indentation: bool,
}

impl IndentationHandler {
    /// Creates a new indentation handler.
    #[must_use]
    pub fn new() -> Self {
        Self {
            indent_stack: vec![0], // Start with 0 indentation
            pending_tokens: VecDeque::new(),
            was_space_indentation: false,
            was_tab_indentation: false,
            was_mixed_indentation: false,
        }
    }

    /// Handles a newline token, potentially hiding it in implicit line joining contexts.
    ///
    /// # Errors
    /// Returns a lexer error if indentation processing fails.
    pub fn handle_newline(&mut self, opened_parens: usize, location: SourceLocation) -> Result<Vec<Token>, LexerError> {
        let mut tokens = vec![];

        // Create newline token
        let newline_token = Token::new(TokenType::Newline, "\n".to_string(), location);

        // If we're in implicit line joining (inside parens/brackets/braces), hide the newline
        if opened_parens > 0 {
            tokens.push(newline_token.with_channel(Channel::Hidden));
            return Ok(tokens);
        }

        tokens.push(newline_token);
        Ok(tokens)
    }

    /// Handles indentation at the beginning of a line.
    ///
    /// # Errors
    /// Returns a lexer error if indentation is inconsistent.
    pub fn handle_indentation(&mut self, indentation_text: &str, location: SourceLocation) -> Result<Vec<Token>, LexerError> {
        let indent_level = self.calculate_indentation_level(indentation_text)?;
        self.generate_indent_dedent_tokens(indent_level, location)
    }

    fn calculate_indentation_level(&mut self, text: &str) -> Result<usize, LexerError> {
        const TAB_LENGTH: usize = 8;
        let mut level = 0;

        for ch in text.chars() {
            match ch {
                ' ' => {
                    self.was_space_indentation = true;
                    level += 1;
                }
                '\t' => {
                    self.was_tab_indentation = true;
                    level += TAB_LENGTH - (level % TAB_LENGTH);
                }
                '\x0C' => { // Form feed
                    level = 0;
                }
                _ => break,
            }
        }

        // Check for mixed indentation
        if self.was_space_indentation && self.was_tab_indentation && !self.was_mixed_indentation {
            self.was_mixed_indentation = true;
            return Err(LexerError::InconsistentIndentation);
        }

        Ok(level)
    }

    fn generate_indent_dedent_tokens(&mut self, new_indent: usize, location: SourceLocation) -> Result<Vec<Token>, LexerError> {
        let mut tokens = vec![];
        let current_indent = *self.indent_stack.last().unwrap();

        match new_indent.cmp(&current_indent) {
            Ordering::Greater => {
                // Increase in indentation -> INDENT
                self.indent_stack.push(new_indent);
                tokens.push(Token::new(TokenType::Indent, String::new(), location));
            }
            Ordering::Less => {
                // Decrease in indentation -> DEDENT(s)
                while let Some(&stack_indent) = self.indent_stack.last() {
                    if stack_indent <= new_indent {
                        break;
                    }
                    self.indent_stack.pop();
                    tokens.push(Token::new(TokenType::Dedent, String::new(), location.clone()));
                }

                // Check for inconsistent dedent
                if self.indent_stack.last() != Some(&new_indent) {
                    return Err(LexerError::InconsistentDedent);
                }
            }
            Ordering::Equal => {
                // Same indentation level, no tokens needed
            }
        }

        Ok(tokens)
    }

    pub fn handle_eof(&mut self, location: &SourceLocation) -> Vec<Token> {
        let mut tokens = vec![];

        // Generate DEDENT tokens for all remaining indentation levels
        while self.indent_stack.len() > 1 {
            self.indent_stack.pop();
            tokens.push(Token::new(TokenType::Dedent, String::new(), location.clone()));
        }

        tokens
    }

    pub fn reset(&mut self) {
        self.indent_stack = vec![0];
        self.pending_tokens.clear();
        self.was_space_indentation = false;
        self.was_tab_indentation = false;
        self.was_mixed_indentation = false;
    }
}

impl Default for IndentationHandler {
    fn default() -> Self {
        Self::new()
    }
}
