use crate::lexer::{error::LexerError, token::*};
use crate::utils::SourceLocation;

pub struct NumberLexer;

impl NumberLexer {
    /// Scans a numeric literal token.
    ///
    /// # Errors
    /// Returns a lexer error if the number format is invalid.
    pub fn scan_number(input: &str, start_pos: usize, location: SourceLocation) -> Result<(Token, usize), LexerError> {
        let chars: Vec<char> = input.chars().collect();
        let pos = start_pos;

        if pos >= chars.len() {
            return Err(LexerError::UnexpectedEof);
        }

        // Check for different number formats
        if chars[pos] == '0' && pos + 1 < chars.len() {
            match chars[pos + 1] {
                'b' | 'B' => return Self::scan_binary_number(&chars, pos, location),
                'o' | 'O' => return Self::scan_octal_number(&chars, pos, location),
                'x' | 'X' => return Self::scan_hex_number(&chars, pos, location),
                _ => {}
            }
        }

        // Decimal number (integer or float)
        Self::scan_decimal_number(&chars, pos, location)
    }

    fn scan_binary_number(chars: &[char], start_pos: usize, location: SourceLocation) -> Result<(Token, usize), LexerError> {
        let mut pos = start_pos + 2; // Skip '0b' or '0B'
        let mut has_digits = false;

        while pos < chars.len() {
            match chars[pos] {
                '0' | '1' => {
                    has_digits = true;
                    pos += 1;
                }
                '_' => {
                    pos += 1; // Skip underscore separators
                }
                _ => break,
            }
        }

        if !has_digits {
            return Err(LexerError::InvalidNumber("binary number without digits".to_string()));
        }

        let lexeme: String = chars[start_pos..pos].iter().collect();
        let token = Token::new(TokenType::Number(NumberType::Integer(lexeme.clone())), lexeme, location);
        Ok((token, pos))
    }

    fn scan_octal_number(chars: &[char], start_pos: usize, location: SourceLocation) -> Result<(Token, usize), LexerError> {
        let mut pos = start_pos + 2; // Skip '0o' or '0O'
        let mut has_digits = false;

        while pos < chars.len() {
            match chars[pos] {
                '0'..='7' => {
                    has_digits = true;
                    pos += 1;
                }
                '_' => {
                    pos += 1; // Skip underscore separators
                }
                _ => break,
            }
        }

        if !has_digits {
            return Err(LexerError::InvalidNumber("octal number without digits".to_string()));
        }

        let lexeme: String = chars[start_pos..pos].iter().collect();
        let token = Token::new(TokenType::Number(NumberType::Integer(lexeme.clone())), lexeme, location);
        Ok((token, pos))
    }

    fn scan_hex_number(chars: &[char], start_pos: usize, location: SourceLocation) -> Result<(Token, usize), LexerError> {
        let mut pos = start_pos + 2; // Skip '0x' or '0X'
        let mut has_digits = false;

        while pos < chars.len() {
            match chars[pos] {
                '0'..='9' | 'a'..='f' | 'A'..='F' => {
                    has_digits = true;
                    pos += 1;
                }
                '_' => {
                    pos += 1; // Skip underscore separators
                }
                _ => break,
            }
        }

        if !has_digits {
            return Err(LexerError::InvalidNumber("hexadecimal number without digits".to_string()));
        }

        let lexeme: String = chars[start_pos..pos].iter().collect();
        let token = Token::new(TokenType::Number(NumberType::Integer(lexeme.clone())), lexeme, location);
        Ok((token, pos))
    }

    fn scan_decimal_number(chars: &[char], start_pos: usize, location: SourceLocation) -> Result<(Token, usize), LexerError> {
        let mut pos = start_pos;
        let mut is_float = false;
        let mut _has_exponent = false;

        // Scan integer part
        while pos < chars.len() && (chars[pos].is_ascii_digit() || chars[pos] == '_') {
            pos += 1;
        }

        // Check for decimal point
        if pos < chars.len() && chars[pos] == '.' {
            // Look ahead to make sure it's not an ellipsis or method call
            if pos + 1 < chars.len() && chars[pos + 1].is_ascii_digit() {
                is_float = true;
                pos += 1; // Skip '.'

                // Scan fractional part
                while pos < chars.len() && (chars[pos].is_ascii_digit() || chars[pos] == '_') {
                    pos += 1;
                }
            }
        }

        // Check for exponent
        if pos < chars.len() && (chars[pos] == 'e' || chars[pos] == 'E') {
            _has_exponent = true;
            is_float = true;
            pos += 1;

            // Optional sign
            if pos < chars.len() && (chars[pos] == '+' || chars[pos] == '-') {
                pos += 1;
            }

            // Exponent digits
            let exp_start = pos;
            while pos < chars.len() && (chars[pos].is_ascii_digit() || chars[pos] == '_') {
                pos += 1;
            }

            if pos == exp_start {
                return Err(LexerError::InvalidNumber("exponent without digits".to_string()));
            }
        }

        // Check for imaginary suffix
        let is_imaginary = pos < chars.len() && (chars[pos] == 'j' || chars[pos] == 'J');
        if is_imaginary {
            pos += 1;
        }

        let lexeme: String = chars[start_pos..pos].iter().collect();

        let number_type = if is_imaginary {
            NumberType::Imaginary(lexeme.clone())
        } else if is_float {
            NumberType::Float(lexeme.clone())
        } else {
            NumberType::Integer(lexeme.clone())
        };

        let token = Token::new(TokenType::Number(number_type), lexeme, location);
        Ok((token, pos))
    }
}
