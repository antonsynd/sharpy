use crate::lexer::{error::LexerError, token::*};
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
    #[allow(dead_code)]
    mode_stack: Vec<LexerMode>,
    #[allow(dead_code)]
    brace_expression_stack: Vec<String>,
    #[allow(dead_code)]
    paren_bracket_stack: Vec<usize>,
}

impl StringLexer {
    /// Creates a new string lexer.
    #[must_use]
    pub const fn new() -> Self {
        Self {
            mode_stack: vec![],
            brace_expression_stack: vec![],
            paren_bracket_stack: vec![],
        }
    }

    /// Scans a string literal token.
    ///
    /// # Errors
    /// Returns a lexer error if the string is invalid or unterminated.
    pub fn scan_string(
        input: &str,
        start_pos: usize,
        location: SourceLocation,
    ) -> Result<(Token, usize), LexerError> {
        let chars: Vec<char> = input.chars().collect();
        let mut pos = start_pos;

        // Scan string prefix
        let (prefix, new_pos) = Self::scan_string_prefix(&chars, pos);
        pos = new_pos;

        // Determine if it's an f-string
        if prefix.to_lowercase().contains('f') {
            return Self::scan_fstring_start(&chars, start_pos, pos, &prefix, location);
        }

        // Regular string or bytes
        Self::scan_regular_string(&chars, start_pos, pos, &prefix, location)
    }

    fn scan_string_prefix(chars: &[char], start_pos: usize) -> (String, usize) {
        let mut pos = start_pos;
        let mut prefix = String::new();

        // Scan for string prefixes: r, u, b, f (and combinations)
        while pos < chars.len() {
            match chars[pos].to_ascii_lowercase() {
                'r' | 'u' | 'b' | 'f' => {
                    prefix.push(chars[pos]);
                    pos += 1;
                }
                _ => break,
            }
        }

        (prefix, pos)
    }

    fn scan_quote_style(chars: &[char], pos: usize) -> Result<(String, usize, bool), LexerError> {
        if pos >= chars.len() {
            return Err(LexerError::UnexpectedEof);
        }

        let quote_char = chars[pos];
        if quote_char != '"' && quote_char != '\'' {
            return Err(LexerError::UnexpectedCharacter(quote_char));
        }

        // Check for triple quotes
        if pos + 2 < chars.len() && chars[pos + 1] == quote_char && chars[pos + 2] == quote_char {
            let quote_style = quote_char.to_string().repeat(3);
            Ok((quote_style, pos + 3, true))
        } else {
            Ok((quote_char.to_string(), pos + 1, false))
        }
    }

    fn scan_fstring_start(
        chars: &[char],
        start_pos: usize,
        pos: usize,
        _prefix: &str,
        location: SourceLocation,
    ) -> Result<(Token, usize), LexerError> {
        let (_quote_style, new_pos, _is_triple) = Self::scan_quote_style(chars, pos)?;

        let lexeme: String = chars[start_pos..new_pos].iter().collect();
        let token = Token::new(
            TokenType::FString(FStringPart::Start(lexeme.clone())),
            lexeme,
            location,
        );

        Ok((token, new_pos))
    }

    fn scan_regular_string(
        chars: &[char],
        start_pos: usize,
        pos: usize,
        prefix: &str,
        location: SourceLocation,
    ) -> Result<(Token, usize), LexerError> {
        let (quote_style, mut current_pos, is_triple) = Self::scan_quote_style(chars, pos)?;
        let quote_char = quote_style.chars().next().unwrap();
        let is_raw = prefix.to_lowercase().contains('r');
        let is_bytes = prefix.to_lowercase().contains('b');

        let mut content = String::new();

        while current_pos < chars.len() {
            let ch = chars[current_pos];

            // Check for end quote(s)
            if ch == quote_char {
                if is_triple {
                    // Check for triple quote end
                    if current_pos + 2 < chars.len()
                        && chars[current_pos + 1] == quote_char
                        && chars[current_pos + 2] == quote_char
                    {
                        current_pos += 3;
                        break;
                    }
                    content.push(ch);
                    current_pos += 1;
                } else {
                    current_pos += 1;
                    break;
                }
            } else if ch == '\\' && !is_raw {
                // Handle escape sequences
                let (escaped, new_pos) = Self::scan_escape_sequence(chars, current_pos)?;
                content.push_str(&escaped);
                current_pos = new_pos;
            } else if ch == '\n' && !is_triple {
                return Err(LexerError::UnterminatedString);
            }
            content.push(ch);
            current_pos += 1;
        }

        let lexeme: String = chars[start_pos..current_pos].iter().collect();

        let token_type = if is_bytes {
            TokenType::String(StringType::Bytes(content.into_bytes()))
        } else if is_raw {
            TokenType::String(StringType::Raw(content))
        } else {
            TokenType::String(StringType::Regular(content))
        };

        let token = Token::new(token_type, lexeme, location);
        Ok((token, current_pos))
    }

    fn scan_escape_sequence(
        chars: &[char],
        start_pos: usize,
    ) -> Result<(String, usize), LexerError> {
        if start_pos + 1 >= chars.len() {
            return Err(LexerError::InvalidEscapeSequence(
                "incomplete escape sequence".to_string(),
            ));
        }

        let escape_char = chars[start_pos + 1];
        let mut pos = start_pos + 2;

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
                if pos + 1 < chars.len() {
                    let hex_chars: String = chars[pos..pos + 2].iter().collect();
                    if let Ok(value) = u8::from_str_radix(&hex_chars, 16) {
                        pos += 2;
                        (value as char).to_string()
                    } else {
                        return Err(LexerError::InvalidEscapeSequence(format!("\\x{hex_chars}")));
                    }
                } else {
                    return Err(LexerError::InvalidEscapeSequence(
                        "incomplete \\x escape".to_string(),
                    ));
                }
            }
            'u' => {
                // Unicode escape \uxxxx
                if pos + 3 < chars.len() {
                    let hex_chars: String = chars[pos..pos + 4].iter().collect();
                    if let Ok(value) = u32::from_str_radix(&hex_chars, 16) {
                        if let Some(unicode_char) = char::from_u32(value) {
                            pos += 4;
                            unicode_char.to_string()
                        } else {
                            return Err(LexerError::InvalidEscapeSequence(format!(
                                "invalid unicode \\u{hex_chars}"
                            )));
                        }
                    } else {
                        return Err(LexerError::InvalidEscapeSequence(format!("\\u{hex_chars}")));
                    }
                } else {
                    return Err(LexerError::InvalidEscapeSequence(
                        "incomplete \\u escape".to_string(),
                    ));
                }
            }
            'U' => {
                // Unicode escape \Uxxxxxxxx
                if pos + 7 < chars.len() {
                    let hex_chars: String = chars[pos..pos + 8].iter().collect();
                    if let Ok(value) = u32::from_str_radix(&hex_chars, 16) {
                        if let Some(unicode_char) = char::from_u32(value) {
                            pos += 8;
                            unicode_char.to_string()
                        } else {
                            return Err(LexerError::InvalidEscapeSequence(format!(
                                "invalid unicode \\U{hex_chars}"
                            )));
                        }
                    } else {
                        return Err(LexerError::InvalidEscapeSequence(format!("\\U{hex_chars}")));
                    }
                } else {
                    return Err(LexerError::InvalidEscapeSequence(
                        "incomplete \\U escape".to_string(),
                    ));
                }
            }
            'N' => {
                // Named unicode escape \N{name}
                if pos < chars.len() && chars[pos] == '{' {
                    pos += 1;
                    let start_name = pos;
                    while pos < chars.len() && chars[pos] != '}' {
                        pos += 1;
                    }
                    if pos < chars.len() {
                        pos += 1; // Skip closing }
                        let name: String = chars[start_name..pos - 1].iter().collect();
                        // For now, just return the escape sequence as-is
                        // In a full implementation, you'd look up the Unicode name
                        format!("\\N{{{name}}}")
                    } else {
                        return Err(LexerError::InvalidEscapeSequence(
                            "incomplete \\N{} escape".to_string(),
                        ));
                    }
                } else {
                    return Err(LexerError::InvalidEscapeSequence(
                        "invalid \\N escape".to_string(),
                    ));
                }
            }
            '0'..='7' => {
                // Octal escape \ooo
                let mut octal_value = (escape_char as u8) - b'0';
                let mut octal_pos = pos;

                for _ in 0..2 {
                    if octal_pos < chars.len() && chars[octal_pos].is_ascii_digit() {
                        let digit = (chars[octal_pos] as u8) - b'0';
                        if digit < 8 {
                            octal_value = octal_value * 8 + digit;
                            octal_pos += 1;
                        } else {
                            break;
                        }
                    } else {
                        break;
                    }
                }

                pos = octal_pos;
                (octal_value as char).to_string()
            }
            _ => {
                // Unknown escape sequence, return as-is
                format!("\\{escape_char}")
            }
        };

        Ok((result, pos))
    }
}

impl Default for StringLexer {
    fn default() -> Self {
        Self::new()
    }
}
