use crate::lexer::{error::LexerError, scanner::Scanner, token::*};
use crate::utils::SourceLocation;

pub struct NumberLexer;

impl NumberLexer {
    /// Scans a numeric literal token.
    ///
    /// # Errors
    /// Returns a lexer error if the number format is invalid.
    pub fn scan_number(scanner: &mut Scanner) -> Result<Token, LexerError> {
        let start_pos = scanner.position();
        let location = scanner.current_location();

        if scanner.current_char().is_none() {
            return Err(LexerError::UnexpectedEof);
        }

        // Check for different number formats
        if scanner.current_char() == Some('0')
            && let Some(next_char) = scanner.peek_char()
        {
            match next_char {
                'b' | 'B' => return Self::scan_binary_number(scanner, start_pos, location),
                'o' | 'O' => return Self::scan_octal_number(scanner, start_pos, location),
                'x' | 'X' => return Self::scan_hex_number(scanner, start_pos, location),
                _ => {}
            }
        }

        // Decimal number (integer or float)
        Self::scan_decimal_number(scanner, start_pos, location)
    }

    fn scan_binary_number(
        scanner: &mut Scanner,
        start_pos: usize,
        mut location: SourceLocation,
    ) -> Result<Token, LexerError> {
        scanner.advance(); // Skip '0'
        scanner.advance(); // Skip 'b' or 'B'
        let mut has_digits = false;

        while let Some(ch) = scanner.current_char() {
            match ch {
                '0' | '1' => {
                    has_digits = true;
                    scanner.advance();
                }
                '_' => {
                    scanner.advance(); // Skip underscore separators
                }
                _ => break,
            }
        }

        if !has_digits {
            return Err(LexerError::InvalidNumber(
                "binary number without digits".to_string(),
            ));
        }

        let lexeme = scanner.lexeme_from(start_pos);
        location.end = scanner.position();
        let token = Token::new(TokenType::Number(NumberType::Integer(lexeme)), location);
        Ok(token)
    }

    fn scan_octal_number(
        scanner: &mut Scanner,
        start_pos: usize,
        mut location: SourceLocation,
    ) -> Result<Token, LexerError> {
        scanner.advance(); // Skip '0'
        scanner.advance(); // Skip 'o' or 'O'
        let mut has_digits = false;

        while let Some(ch) = scanner.current_char() {
            match ch {
                '0'..='7' => {
                    has_digits = true;
                    scanner.advance();
                }
                '_' => {
                    scanner.advance(); // Skip underscore separators
                }
                _ => break,
            }
        }

        if !has_digits {
            return Err(LexerError::InvalidNumber(
                "octal number without digits".to_string(),
            ));
        }

        let lexeme = scanner.lexeme_from(start_pos);
        location.end = scanner.position();
        let token = Token::new(TokenType::Number(NumberType::Integer(lexeme)), location);
        Ok(token)
    }

    fn scan_hex_number(
        scanner: &mut Scanner,
        start_pos: usize,
        mut location: SourceLocation,
    ) -> Result<Token, LexerError> {
        scanner.advance(); // Skip '0'
        scanner.advance(); // Skip 'x' or 'X'
        let mut has_digits = false;

        while let Some(ch) = scanner.current_char() {
            match ch {
                '0'..='9' | 'a'..='f' | 'A'..='F' => {
                    has_digits = true;
                    scanner.advance();
                }
                '_' => {
                    scanner.advance(); // Skip underscore separators
                }
                _ => break,
            }
        }

        if !has_digits {
            return Err(LexerError::InvalidNumber(
                "hexadecimal number without digits".to_string(),
            ));
        }

        let lexeme = scanner.lexeme_from(start_pos);
        location.end = scanner.position();
        let token = Token::new(TokenType::Number(NumberType::Integer(lexeme)), location);
        Ok(token)
    }

    fn scan_decimal_number(
        scanner: &mut Scanner,
        start_pos: usize,
        mut location: SourceLocation,
    ) -> Result<Token, LexerError> {
        let mut is_float = false;

        // Scan integer part
        while let Some(ch) = scanner.current_char() {
            if ch.is_ascii_digit() || ch == '_' {
                scanner.advance();
            } else {
                break;
            }
        }

        // Check for decimal point
        if scanner.current_char() == Some('.') {
            // Look ahead to make sure it's not an ellipsis or method call
            if let Some(next_char) = scanner.peek_char()
                && next_char.is_ascii_digit()
            {
                is_float = true;
                scanner.advance(); // Skip '.'

                // Scan fractional part
                while let Some(ch) = scanner.current_char() {
                    if ch.is_ascii_digit() || ch == '_' {
                        scanner.advance();
                    } else {
                        break;
                    }
                }
            }
        }

        // Check for exponent
        if let Some(ch) = scanner.current_char()
            && (ch == 'e' || ch == 'E')
        {
            is_float = true;
            scanner.advance();

            // Optional sign
            if let Some(sign_ch) = scanner.current_char()
                && (sign_ch == '+' || sign_ch == '-')
            {
                scanner.advance();
            }

            // Exponent digits
            let exp_start = scanner.position();
            while let Some(exp_ch) = scanner.current_char() {
                if exp_ch.is_ascii_digit() || exp_ch == '_' {
                    scanner.advance();
                } else {
                    break;
                }
            }

            if scanner.position() == exp_start {
                return Err(LexerError::InvalidNumber(
                    "exponent without digits".to_string(),
                ));
            }
        }

        // Check for imaginary suffix
        let is_imaginary = if let Some(ch) = scanner.current_char()
            && (ch == 'j' || ch == 'J')
        {
            scanner.advance(); // Consume the 'j' or 'J'
            true
        } else {
            false
        };

        let lexeme = scanner.lexeme_from(start_pos);
        location.end = scanner.position();

        let number_type = if is_imaginary {
            NumberType::Imaginary(lexeme)
        } else if is_float {
            NumberType::Float(lexeme)
        } else {
            NumberType::Integer(lexeme)
        };

        let token = Token::new(TokenType::Number(number_type), location);
        Ok(token)
    }
}
