use crate::lexer::{error::LexerError, scanner::Scanner, token::*};
use crate::utils::SourceLocation;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum LexerMode {
    Default,
    // F-string modes
    FStringSQ1,    // f'...'
    FStringDQ1,    // f"..."
    FStringSQ3,    // f'''...'''
    FStringDQ3,    // f"""..."""
    FStringSQ1Raw, // rf'...' or fr'...'
    FStringDQ1Raw, // rf"..." or fr"..."
    FStringSQ3Raw, // rf'''...''' or fr'''...'''
    FStringDQ3Raw, // rf"""...""" or fr"""..."""

    // Format specification modes (after : in f-strings)
    FormatSpecSQ1,
    FormatSpecDQ1,
    FormatSpecSQ3,
    FormatSpecDQ3,
    FormatSpecSQ1Raw,
    FormatSpecDQ1Raw,
    FormatSpecSQ3Raw,
    FormatSpecDQ3Raw,
}

#[allow(clippy::struct_field_names)]
pub struct StringLexer {
    mode_stack: Vec<LexerMode>,
    brace_depth: usize,
}

impl StringLexer {
    /// Creates a new string lexer.
    #[must_use]
    pub const fn new() -> Self {
        Self {
            mode_stack: vec![],
            brace_depth: 0,
        }
    }

    /// Check if we're currently in an f-string mode
    #[must_use]
    pub fn is_in_fstring(&self) -> bool {
        self.mode_stack.iter().any(|mode| {
            matches!(
                mode,
                LexerMode::FStringSQ1
                    | LexerMode::FStringDQ1
                    | LexerMode::FStringSQ3
                    | LexerMode::FStringDQ3
                    | LexerMode::FStringSQ1Raw
                    | LexerMode::FStringDQ1Raw
                    | LexerMode::FStringSQ3Raw
                    | LexerMode::FStringDQ3Raw
            )
        })
    }

    /// Get the current f-string mode if we're in one
    #[must_use]
    pub fn current_fstring_mode(&self) -> Option<&LexerMode> {
        self.mode_stack.iter().rev().find(|mode| {
            matches!(
                mode,
                LexerMode::FStringSQ1
                    | LexerMode::FStringDQ1
                    | LexerMode::FStringSQ3
                    | LexerMode::FStringDQ3
                    | LexerMode::FStringSQ1Raw
                    | LexerMode::FStringDQ1Raw
                    | LexerMode::FStringSQ3Raw
                    | LexerMode::FStringDQ3Raw
            )
        })
    }

    /// Check if we're currently in an expression within an f-string
    #[must_use]
    pub fn is_in_fstring_expression(&self) -> bool {
        self.is_in_fstring() && self.brace_depth > 0
    }

    /// Start scanning an f-string and push the appropriate mode
    pub fn start_fstring(&mut self, prefix: &str, quote_style: &str, is_triple: bool) {
        let is_raw = prefix.to_lowercase().contains('r');
        let mode = match (quote_style.chars().next(), is_triple, is_raw) {
            (Some('\''), false, false) => LexerMode::FStringSQ1,
            (Some('"'), false, false) => LexerMode::FStringDQ1,
            (Some('\''), true, false) => LexerMode::FStringSQ3,
            (Some('"'), true, false) => LexerMode::FStringDQ3,
            (Some('\''), false, true) => LexerMode::FStringSQ1Raw,
            (Some('"'), false, true) => LexerMode::FStringDQ1Raw,
            (Some('\''), true, true) => LexerMode::FStringSQ3Raw,
            (Some('"'), true, true) => LexerMode::FStringDQ3Raw,
            _ => unreachable!(),
        };
        self.mode_stack.push(mode);
        self.brace_depth = 0;
    }

    /// Handle entering an f-string expression (when we encounter '{')
    pub const fn enter_fstring_expression(&mut self) {
        self.brace_depth += 1;
    }

    /// Handle exiting an f-string expression (when we encounter '}')
    pub const fn exit_fstring_expression(&mut self) {
        if self.brace_depth > 0 {
            self.brace_depth -= 1;
        }
    }

    /// End the current f-string mode
    pub fn end_fstring(&mut self) {
        // Remove the f-string mode from the stack
        if let Some(pos) = self.mode_stack.iter().rposition(|mode| {
            matches!(
                mode,
                LexerMode::FStringSQ1
                    | LexerMode::FStringDQ1
                    | LexerMode::FStringSQ3
                    | LexerMode::FStringDQ3
                    | LexerMode::FStringSQ1Raw
                    | LexerMode::FStringDQ1Raw
                    | LexerMode::FStringSQ3Raw
                    | LexerMode::FStringDQ3Raw
            )
        }) {
            self.mode_stack.remove(pos);
        }
        self.brace_depth = 0;
    }

    /// Scan f-string content (text between expressions)
    ///
    /// # Errors
    /// Errors if the f-string is unterminated or contains invalid escape
    /// sequences.
    pub fn scan_fstring_middle(&self, scanner: &mut Scanner) -> Result<Token, LexerError> {
        let mut location = scanner.current_location();
        let mut content = String::new();

        // Get the quote character and style for the current f-string
        let mode = self
            .current_fstring_mode()
            .ok_or(LexerError::UnexpectedCharacter('f'))?;
        let (quote_char, is_triple, is_raw) = match mode {
            LexerMode::FStringSQ1 => ('\'', false, false),
            LexerMode::FStringDQ1 => ('"', false, false),
            LexerMode::FStringSQ3 => ('\'', true, false),
            LexerMode::FStringDQ3 => ('"', true, false),
            LexerMode::FStringSQ1Raw => ('\'', false, true),
            LexerMode::FStringDQ1Raw => ('"', false, true),
            LexerMode::FStringSQ3Raw => ('\'', true, true),
            LexerMode::FStringDQ3Raw => ('"', true, true),
            _ => return Err(LexerError::UnexpectedCharacter('f')),
        };

        while let Some(ch) = scanner.current_char() {
            // Check for end quote(s)
            if ch == quote_char {
                if is_triple {
                    // Check for triple quote end
                    if scanner.peek_char() == Some(quote_char) {
                        let peek_next = scanner.peek_next_chars(2);
                        if peek_next.len() >= 2 && peek_next.chars().nth(1) == Some(quote_char) {
                            // Found end of f-string
                            break;
                        }
                    }
                    content.push(ch);
                    scanner.advance();
                } else {
                    // Single quote - end of f-string
                    break;
                }
            } else if ch == '{' {
                // Start of expression - stop here
                break;
            } else if ch == '}' {
                // Unmatched closing brace - error or escaped
                if scanner.peek_char() == Some('}') {
                    // Escaped closing brace {{
                    content.push(ch);
                    scanner.advance(); // consume first }
                    scanner.advance(); // consume second }
                } else {
                    return Err(LexerError::UnexpectedCharacter(ch));
                }
            } else if ch == '\\' && !is_raw {
                // Handle escape sequences
                let escaped = Self::scan_escape_sequence(scanner)?;
                content.push_str(&escaped);
            } else if ch == '\n' && !is_triple {
                return Err(LexerError::UnterminatedString);
            } else {
                content.push(ch);
                scanner.advance();
            }
        }

        location.end = scanner.position();
        Ok(Token::new(
            TokenType::FString(FStringPart::Middle(content)),
            location,
        ))
    }

    /// Scan the end of an f-string
    ///
    /// # Errors
    /// Errors if the f-string is unterminated.
    pub fn scan_fstring_end(&self, scanner: &mut Scanner) -> Result<Token, LexerError> {
        let start_pos = scanner.position();
        let mut location = scanner.current_location();

        // Get the quote character and style for the current f-string
        let mode = self
            .current_fstring_mode()
            .ok_or(LexerError::UnexpectedCharacter('f'))?;
        let (quote_char, is_triple) = match mode {
            LexerMode::FStringSQ1 | LexerMode::FStringSQ1Raw => ('\'', false),
            LexerMode::FStringDQ1 | LexerMode::FStringDQ1Raw => ('"', false),
            LexerMode::FStringSQ3 | LexerMode::FStringSQ3Raw => ('\'', true),
            LexerMode::FStringDQ3 | LexerMode::FStringDQ3Raw => ('"', true),
            _ => return Err(LexerError::UnexpectedCharacter('f')),
        };

        if is_triple {
            // Consume triple quotes
            for _ in 0..3 {
                if scanner.current_char() == Some(quote_char) {
                    scanner.advance();
                } else {
                    return Err(LexerError::UnterminatedString);
                }
            }
        } else {
            // Consume single quote
            if scanner.current_char() == Some(quote_char) {
                scanner.advance();
            } else {
                return Err(LexerError::UnterminatedString);
            }
        }

        let lexeme = scanner.lexeme_from(start_pos);
        location.end = scanner.position();
        Ok(Token::new(
            TokenType::FString(FStringPart::End(lexeme)),
            location,
        ))
    }

    /// Scans a string literal token.
    ///
    /// # Errors
    /// Returns a lexer error if the string is invalid or unterminated.
    pub fn scan_string(scanner: &mut Scanner) -> Result<Token, LexerError> {
        let start_pos = scanner.position();
        let location = scanner.current_location();

        // Scan string prefix
        let prefix = Self::scan_string_prefix(scanner);

        // Determine if it's an f-string
        if prefix.to_lowercase().contains('f') {
            return Self::scan_fstring_start(scanner, start_pos, location);
        }

        // Regular string or bytes
        Self::scan_regular_string(scanner, &prefix, location)
    }

    fn scan_string_prefix(scanner: &mut Scanner) -> String {
        let mut prefix = String::new();

        // Scan for string prefixes: r, u, b, f (and combinations)
        while let Some(ch) = scanner.current_char() {
            match ch.to_ascii_lowercase() {
                'r' | 'u' | 'b' | 'f' => {
                    prefix.push(ch);
                    scanner.advance();
                }
                _ => break,
            }
        }

        prefix
    }

    fn scan_quote_style(scanner: &mut Scanner) -> Result<(String, bool), LexerError> {
        if scanner.current_char().is_none() {
            return Err(LexerError::UnexpectedEof);
        }

        let quote_char = scanner.current_char().unwrap();
        if quote_char != '"' && quote_char != '\'' {
            return Err(LexerError::UnexpectedCharacter(quote_char));
        }

        scanner.advance(); // consume first quote

        // Check for triple quotes
        if scanner.current_char() == Some(quote_char)
            && let Some(next_char) = scanner.peek_char()
            && next_char == quote_char
        {
            // Triple quotes
            scanner.advance(); // consume second quote
            scanner.advance(); // consume third quote
            let quote_style = quote_char.to_string().repeat(3);
            return Ok((quote_style, true));
        }

        // Single quote
        Ok((quote_char.to_string(), false))
    }

    fn scan_fstring_start(
        scanner: &mut Scanner,
        start_pos: usize,
        mut location: SourceLocation,
    ) -> Result<Token, LexerError> {
        let (_quote_style, _is_triple) = Self::scan_quote_style(scanner)?;

        let lexeme = scanner.lexeme_from(start_pos);
        location.end = scanner.position();
        let token = Token::new(TokenType::FString(FStringPart::Start(lexeme)), location);

        Ok(token)
    }

    fn scan_regular_string(
        scanner: &mut Scanner,
        prefix: &str,
        mut location: SourceLocation,
    ) -> Result<Token, LexerError> {
        let (quote_style, is_triple) = Self::scan_quote_style(scanner)?;
        let quote_char = quote_style.chars().next().unwrap();
        let is_raw = prefix.to_lowercase().contains('r');
        let is_bytes = prefix.to_lowercase().contains('b');

        let mut content = String::new();

        while let Some(ch) = scanner.current_char() {
            // Check for end quote(s)
            if ch == quote_char {
                if is_triple {
                    // Check for triple quote end
                    if scanner.peek_char() == Some(quote_char) {
                        let peek_next = scanner.peek_next_chars(2);
                        if peek_next.len() >= 2 && peek_next.chars().nth(1) == Some(quote_char) {
                            scanner.advance(); // consume first quote
                            scanner.advance(); // consume second quote
                            scanner.advance(); // consume third quote
                            break;
                        }
                    }
                    content.push(ch);
                    scanner.advance();
                } else {
                    scanner.advance();
                    break;
                }
            } else if ch == '\\' && !is_raw {
                // Handle escape sequences
                let escaped = Self::scan_escape_sequence(scanner)?;
                content.push_str(&escaped);
            } else if ch == '\n' && !is_triple {
                return Err(LexerError::UnterminatedString);
            } else {
                content.push(ch);
                scanner.advance();
            }
        }

        let token_type = if is_bytes {
            TokenType::String(StringType::Bytes(content.into_bytes()))
        } else if is_raw {
            TokenType::String(StringType::Raw(content))
        } else {
            TokenType::String(StringType::Regular(content))
        };

        location.end = scanner.position();
        let token = Token::new(token_type, location);
        Ok(token)
    }

    fn scan_escape_sequence(scanner: &mut Scanner) -> Result<String, LexerError> {
        // We're currently at the backslash, advance to the escape character
        scanner.advance();

        if scanner.current_char().is_none() {
            return Err(LexerError::InvalidEscapeSequence(
                "incomplete escape sequence".to_string(),
            ));
        }

        let escape_char = scanner.current_char().unwrap();
        scanner.advance(); // consume the escape character

        let result = match escape_char {
            '\\' => "\\".to_string(),
            '\'' => "'".to_string(),
            '"' => "\"".to_string(),
            'a' => "\x07".to_string(), // Bell
            'b' => "\x08".to_string(), // Backspace
            'f' => "\x0C".to_string(), // Form feed
            'n' => "\n".to_string(),   // Newline
            'r' => "\r".to_string(),   // Carriage return
            't' => "\t".to_string(),   // Tab
            'v' => "\x0B".to_string(), // Vertical tab
            '0' => "\0".to_string(),   // Null
            '\n' => {
                // Line continuation
                String::new()
            }
            'x' => {
                // Hexadecimal escape \xhh
                let mut hex_chars = String::new();
                for _ in 0..2 {
                    if let Some(ch) = scanner.current_char() {
                        if ch.is_ascii_hexdigit() {
                            hex_chars.push(ch);
                            scanner.advance();
                        } else {
                            break;
                        }
                    } else {
                        break;
                    }
                }

                if hex_chars.len() != 2 {
                    return Err(LexerError::InvalidEscapeSequence(format!(
                        "incomplete \\x{hex_chars} escape"
                    )));
                }

                if let Ok(value) = u8::from_str_radix(&hex_chars, 16) {
                    (value as char).to_string()
                } else {
                    return Err(LexerError::InvalidEscapeSequence(format!("\\x{hex_chars}")));
                }
            }
            'u' => {
                // Unicode escape \uxxxx
                let mut hex_chars = String::new();
                for _ in 0..4 {
                    if let Some(ch) = scanner.current_char() {
                        if ch.is_ascii_hexdigit() {
                            hex_chars.push(ch);
                            scanner.advance();
                        } else {
                            break;
                        }
                    } else {
                        break;
                    }
                }

                if hex_chars.len() != 4 {
                    return Err(LexerError::InvalidEscapeSequence(format!(
                        "incomplete \\u{hex_chars} escape"
                    )));
                }

                if let Ok(value) = u32::from_str_radix(&hex_chars, 16) {
                    if let Some(unicode_char) = char::from_u32(value) {
                        unicode_char.to_string()
                    } else {
                        return Err(LexerError::InvalidEscapeSequence(format!(
                            "invalid unicode \\u{hex_chars}"
                        )));
                    }
                } else {
                    return Err(LexerError::InvalidEscapeSequence(format!("\\u{hex_chars}")));
                }
            }
            'U' => {
                // Unicode escape \Uxxxxxxxx
                let mut hex_chars = String::new();
                for _ in 0..8 {
                    if let Some(ch) = scanner.current_char() {
                        if ch.is_ascii_hexdigit() {
                            hex_chars.push(ch);
                            scanner.advance();
                        } else {
                            break;
                        }
                    } else {
                        break;
                    }
                }

                if hex_chars.len() != 8 {
                    return Err(LexerError::InvalidEscapeSequence(format!(
                        "incomplete \\U{hex_chars} escape"
                    )));
                }

                if let Ok(value) = u32::from_str_radix(&hex_chars, 16) {
                    if let Some(unicode_char) = char::from_u32(value) {
                        unicode_char.to_string()
                    } else {
                        return Err(LexerError::InvalidEscapeSequence(format!(
                            "invalid unicode \\U{hex_chars}"
                        )));
                    }
                } else {
                    return Err(LexerError::InvalidEscapeSequence(format!("\\U{hex_chars}")));
                }
            }
            'N' => {
                // Named unicode escape \N{name}
                if scanner.current_char() == Some('{') {
                    scanner.advance(); // consume '{'
                    let mut name = String::new();
                    let mut found_closing = false;

                    while let Some(ch) = scanner.current_char() {
                        if ch == '}' {
                            scanner.advance(); // consume '}'
                            found_closing = true;
                            break;
                        }
                        name.push(ch);
                        scanner.advance();
                    }

                    if name.is_empty() || !found_closing {
                        return Err(LexerError::InvalidEscapeSequence(
                            "incomplete \\N{} escape".to_string(),
                        ));
                    }

                    // For now, just return the escape sequence as-is
                    // In a full implementation, you'd look up the Unicode name
                    format!("\\N{{{name}}}")
                } else {
                    return Err(LexerError::InvalidEscapeSequence(
                        "invalid \\N escape".to_string(),
                    ));
                }
            }
            '0'..='7' => {
                // Octal escape \ooo
                let mut octal_value = (escape_char as u8) - b'0';

                for _ in 0..2 {
                    if let Some(ch) = scanner.current_char() {
                        if ch.is_ascii_digit() && (ch as u8) < b'8' {
                            let digit = (ch as u8) - b'0';
                            octal_value = octal_value * 8 + digit;
                            scanner.advance();
                        } else {
                            break;
                        }
                    } else {
                        break;
                    }
                }

                (octal_value as char).to_string()
            }
            _ => {
                // Unknown escape sequence, return as-is
                format!("\\{escape_char}")
            }
        };

        Ok(result)
    }
}

impl Default for StringLexer {
    fn default() -> Self {
        Self::new()
    }
}
