use crate::ast::node::*;
use crate::lexer::token::*;
use crate::parser::error::ParseError;
use crate::utils::SourceLocation;

pub struct Parser {
    tokens: Vec<Token>,
    current: usize,
}

impl Parser {
    #[must_use]
    pub const fn new(tokens: Vec<Token>) -> Self {
        Self { tokens, current: 0 }
    }

    /// Parse tokens into AST nodes
    pub fn parse(&mut self) -> Result<Vec<Node>, ParseError> {
        let mut statements = Vec::new();

        while !self.is_at_end() {
            // Skip newlines and comments
            if self.match_token(&TokenType::Newline) || self.is_comment() {
                self.advance();
                continue;
            }

            let stmt = self.parse_statement()?;
            statements.push(stmt);
        }

        Ok(statements)
    }

    /// Parse a single statement
    fn parse_statement(&mut self) -> Result<Node, ParseError> {
        // For now, we only handle assignments
        self.parse_assignment()
    }

    /// Parse an assignment statement: name = value or name: type = value
    fn parse_assignment(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Parse the target (left-hand side)
        let target = self.parse_name()?;

        // Check if this is a typed assignment (name: type = value)
        let mut type_annotation = None;
        if self.match_token(&TokenType::Colon) {
            self.advance(); // consume ':'
            type_annotation = Some(Box::new(self.parse_type_annotation()?));
        }

        // Expect '=' for assignment
        if !self.match_token(&TokenType::Equal) {
            return Err(ParseError::UnexpectedToken {
                expected: "'='".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume '='

        // Parse the value (right-hand side)
        let value = self.parse_expression()?;

        let end_location = self.previous_location();
        let source = Some(NodeSource::from_source_location(
            &start_location,
            &end_location,
        ));

        // If there's a type annotation, create a typed assignment
        if let Some(_type_node) = type_annotation {
            // For typed assignments, we'll create an Assign node with the type info in the target
            // This is a simplification - in a real parser you might want a separate TypedAssign node
            Ok(Node::Assign(Assign {
                targets: vec![target],
                value: Box::new(value),
                source,
            }))
        } else {
            Ok(Node::Assign(Assign {
                targets: vec![target],
                value: Box::new(value),
                source,
            }))
        }
    }

    /// Parse a name (identifier)
    fn parse_name(&mut self) -> Result<Node, ParseError> {
        if let Some(token) = self.current_token()
            && let TokenType::Name(name_type) = &token.token_type
        {
            let location = token.location.clone();
            let name = name_type.name.clone();
            let source = Some(NodeSource::from_source_location(&location, &location));

            self.advance();
            return Ok(Node::Name(Name { id: name, source }));
        }

        Err(ParseError::UnexpectedToken {
            expected: "identifier".to_string(),
            found: self.current_token_string(),
            location: self.current_location(),
        })
    }

    /// Parse a type annotation
    fn parse_type_annotation(&mut self) -> Result<Node, ParseError> {
        // For now, just parse simple type names
        self.parse_name()
    }

    /// Parse an expression
    fn parse_expression(&mut self) -> Result<Node, ParseError> {
        self.parse_primary()
    }

    /// Parse primary expressions (constants, lists, etc.)
    fn parse_primary(&mut self) -> Result<Node, ParseError> {
        if let Some(token) = self.current_token() {
            match &token.token_type {
                // Constants
                TokenType::Number(num_type) => {
                    let location = token.location.clone();
                    let source = Some(NodeSource::from_source_location(&location, &location));

                    let value = match num_type {
                        NumberType::Integer(s) => {
                            let int_val =
                                s.parse::<i64>().map_err(|_| ParseError::InvalidSyntax {
                                    message: format!("Invalid integer: {s}"),
                                    location: location.clone(),
                                })?;
                            ConstantValue::Int(int_val)
                        }
                        NumberType::Float(s) => {
                            let float_val =
                                s.parse::<f64>().map_err(|_| ParseError::InvalidSyntax {
                                    message: format!("Invalid float: {s}"),
                                    location: location.clone(),
                                })?;
                            ConstantValue::Float(float_val)
                        }
                        NumberType::Imaginary(s) => {
                            // Parse imaginary number (remove 'j' or 'J' suffix)
                            let num_part = s.trim_end_matches(['j', 'J']);
                            let imag_val =
                                num_part
                                    .parse::<f64>()
                                    .map_err(|_| ParseError::InvalidSyntax {
                                        message: format!("Invalid imaginary number: {s}"),
                                        location: location.clone(),
                                    })?;
                            ConstantValue::Complex {
                                real: 0.0,
                                imag: imag_val,
                            }
                        }
                    };

                    self.advance();
                    Ok(Node::Constant(Constant { value, source }))
                }

                TokenType::String(string_type) => {
                    let location = token.location.clone();
                    let source = Some(NodeSource::from_source_location(&location, &location));

                    let value = match string_type {
                        StringType::Regular(s) | StringType::Raw(s) => {
                            ConstantValue::Str(s.clone())
                        }
                        StringType::Bytes(bytes) => ConstantValue::Bytes(bytes.clone()),
                    };

                    self.advance();
                    Ok(Node::Constant(Constant { value, source }))
                }

                // Boolean literals
                TokenType::True => {
                    let location = token.location.clone();
                    let source = Some(NodeSource::from_source_location(&location, &location));
                    self.advance();
                    Ok(Node::Constant(Constant {
                        value: ConstantValue::Bool(true),
                        source,
                    }))
                }

                TokenType::False => {
                    let location = token.location.clone();
                    let source = Some(NodeSource::from_source_location(&location, &location));
                    self.advance();
                    Ok(Node::Constant(Constant {
                        value: ConstantValue::Bool(false),
                        source,
                    }))
                }

                TokenType::None => {
                    let location = token.location.clone();
                    let source = Some(NodeSource::from_source_location(&location, &location));
                    self.advance();
                    Ok(Node::Constant(Constant {
                        value: ConstantValue::None,
                        source,
                    }))
                }

                // List literals
                TokenType::LeftBracket => self.parse_list(),

                // Names/identifiers
                TokenType::Name(_) => self.parse_name(),

                _ => Err(ParseError::UnexpectedToken {
                    expected: "expression".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                }),
            }
        } else {
            Err(ParseError::UnexpectedEof {
                expected: "expression".to_string(),
                location: self.current_location(),
            })
        }
    }

    /// Parse a list literal: [elem1, elem2, ...]
    fn parse_list(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Consume '['
        if !self.match_token(&TokenType::LeftBracket) {
            return Err(ParseError::UnexpectedToken {
                expected: "'['".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        let mut elements = Vec::new();

        // Handle empty list
        if self.match_token(&TokenType::RightBracket) {
            self.advance();
            let end_location = self.previous_location();
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));
            return Ok(Node::List(List { elements, source }));
        }

        // Parse list elements
        loop {
            let element = self.parse_expression()?;
            elements.push(element);

            if self.match_token(&TokenType::Comma) {
                self.advance(); // consume ','

                // Allow trailing comma
                if self.match_token(&TokenType::RightBracket) {
                    break;
                }
            } else if self.match_token(&TokenType::RightBracket) {
                break;
            } else {
                return Err(ParseError::UnexpectedToken {
                    expected: "',' or ']'".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                });
            }
        }

        // Consume ']'
        if !self.match_token(&TokenType::RightBracket) {
            return Err(ParseError::UnexpectedToken {
                expected: "']'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        let end_location = self.previous_location();
        let source = Some(NodeSource::from_source_location(
            &start_location,
            &end_location,
        ));

        Ok(Node::List(List { elements, source }))
    }

    // Helper methods

    fn current_token(&self) -> Option<&Token> {
        self.tokens.get(self.current)
    }

    fn advance(&mut self) -> Option<&Token> {
        if !self.is_at_end() {
            self.current += 1;
        }
        self.previous()
    }

    fn previous(&self) -> Option<&Token> {
        if self.current > 0 {
            self.tokens.get(self.current - 1)
        } else {
            None
        }
    }

    fn is_at_end(&self) -> bool {
        self.current >= self.tokens.len()
            || matches!(
                self.current_token().map(|t| &t.token_type),
                Some(TokenType::Eof)
            )
    }

    fn match_token(&self, token_type: &TokenType) -> bool {
        if let Some(token) = self.current_token() {
            std::mem::discriminant(&token.token_type) == std::mem::discriminant(token_type)
        } else {
            false
        }
    }

    fn is_comment(&self) -> bool {
        matches!(
            self.current_token().map(|t| &t.token_type),
            Some(TokenType::Comment(_))
        )
    }

    fn current_token_string(&self) -> String {
        if let Some(token) = self.current_token() {
            format!("{:?}", token.token_type)
        } else {
            "end of file".to_string()
        }
    }

    fn current_location(&self) -> SourceLocation {
        if let Some(token) = self.current_token() {
            token.location.clone()
        } else if let Some(last_token) = self.tokens.last() {
            last_token.location.clone()
        } else {
            SourceLocation::single_char(1, 1, 0)
        }
    }

    fn previous_location(&self) -> SourceLocation {
        if let Some(token) = self.previous() {
            token.location.clone()
        } else {
            self.current_location()
        }
    }
}
