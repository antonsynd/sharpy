use crate::ast::node::{
    Assign, CompOp, Compare, Constant, ConstantValue, If, List, Name, Node, NodeSource, Tuple,
    TypedName,
};
use crate::lexer::token::{NumberType, StringType, Token, TokenType};
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
    ///
    /// # Errors
    ///
    /// Returns `ParseError` if:
    /// - Unexpected tokens are encountered
    /// - Invalid syntax is found
    /// - Unexpected end of file is reached
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
        // Check for control flow statements first
        if self.match_token(&TokenType::If) {
            return self.parse_if_statement();
        }

        // Try to parse as assignment first, then fall back to expression
        self.parse_assignment_or_expression()
    }

    /// Parse either an assignment or a standalone expression
    fn parse_assignment_or_expression(&mut self) -> Result<Node, ParseError> {
        // First, we need to check if this is an assignment pattern
        // Look ahead to see if there's an '=' token in an assignment position
        if self.is_assignment_pattern() {
            self.parse_assignment()
        } else {
            // Parse as a standalone expression
            self.parse_expression()
        }
    }

    /// Check if the current position starts an assignment pattern
    fn is_assignment_pattern(&self) -> bool {
        let mut pos = self.current;

        // Skip over potential assignment targets (names, tuples, etc.)
        while pos < self.tokens.len() {
            if let Some(token) = self.tokens.get(pos) {
                match token.token_type {
                    TokenType::Equal => return true, // Found assignment operator
                    TokenType::Comma => pos += 1,    // Continue through tuple elements
                    TokenType::Colon => {
                        // Skip type annotation
                        pos += 1;
                        // Skip the type name
                        if pos < self.tokens.len()
                            && matches!(
                                self.tokens.get(pos).map(|t| &t.token_type),
                                Some(TokenType::Name(_))
                            )
                        {
                            pos += 1;
                        }
                    }
                    TokenType::Name(_) => pos += 1, // Continue through names
                    _ => break,                     // Not an assignment pattern
                }
            } else {
                break;
            }
        }

        false
    }

    /// Parse an assignment statement: name = value, name: type = value, or x, y = value
    fn parse_assignment(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Parse the target(s) (left-hand side)
        let target = self.parse_assignment_target()?;

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

        Ok(Node::Assign(Assign {
            target: Box::new(target),
            value: Box::new(value),
            source,
        }))
    }

    /// Parse assignment targets: name, name: type, or x, y (destructuring)
    fn parse_assignment_target(&mut self) -> Result<Node, ParseError> {
        let first = self.parse_target_element()?;

        // Check if this is a tuple assignment (multiple targets)
        if self.match_token(&TokenType::Comma) {
            let start_location = self.current_location();
            let mut elements = vec![first];

            while self.match_token(&TokenType::Comma) {
                self.advance(); // consume ','

                // Allow trailing comma
                if self.match_token(&TokenType::Equal) {
                    break;
                }

                elements.push(self.parse_target_element()?);
            }

            let end_location = self.previous_location();
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));

            Ok(Node::Tuple(Tuple { elements, source }))
        } else {
            // Single target
            Ok(first)
        }
    }

    /// Parse a single target element: name or name: type
    fn parse_target_element(&mut self) -> Result<Node, ParseError> {
        let name = self.parse_name()?;

        // Check if this has a type annotation
        if self.match_token(&TokenType::Colon) {
            self.advance(); // consume ':'
            let type_annotation = self.parse_type_annotation()?;

            if let Node::Name(Name { id, source }) = name {
                Ok(Node::TypedName(TypedName {
                    id,
                    type_: Box::new(type_annotation),
                    source,
                }))
            } else {
                Err(ParseError::InvalidSyntax {
                    message: "Expected identifier before type annotation".to_string(),
                    location: self.current_location(),
                })
            }
        } else {
            // Untyped target
            Ok(name)
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
        self.parse_comparison()
    }

    /// Parse comparison expressions: expr == expr, expr < expr, etc.
    fn parse_comparison(&mut self) -> Result<Node, ParseError> {
        let left = self.parse_primary()?;

        let mut ops = Vec::new();
        let mut comparators = Vec::new();

        while let Some(comp_op) = self.parse_comparison_operator() {
            ops.push(comp_op);
            let right = self.parse_primary()?;
            comparators.push(right);
        }

        if ops.is_empty() {
            // No comparison operators found, return the primary expression
            Ok(left)
        } else {
            // Create a Compare node
            let start_location = self.get_node_start_location(&left);
            let end_location = self.get_node_end_location(comparators.last().unwrap());
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));

            Ok(Node::Compare(Compare {
                left: Box::new(left),
                ops,
                comparators,
                source,
            }))
        }
    }

    /// Parse a comparison operator and return the corresponding `CompOp`
    fn parse_comparison_operator(&mut self) -> Option<CompOp> {
        if let Some(token) = self.current_token() {
            let comp_op = match token.token_type {
                TokenType::EqEqual => Some(CompOp::Eq),
                TokenType::NotEqual => Some(CompOp::NotEq),
                TokenType::Less => Some(CompOp::Lt),
                TokenType::LessEqual => Some(CompOp::LtE),
                TokenType::Greater => Some(CompOp::Gt),
                TokenType::GreaterEqual => Some(CompOp::GtE),
                TokenType::Is => Some(CompOp::Is),
                TokenType::In => Some(CompOp::In),
                // Handle "is not" and "not in" compound operators
                TokenType::Not => {
                    // Look ahead to see if this is "not in"
                    if let Some(next_token) = self.tokens.get(self.current + 1)
                        && matches!(next_token.token_type, TokenType::In)
                    {
                        self.advance(); // consume "not"
                        self.advance(); // consume "in"
                        return Some(CompOp::NotIn);
                    }
                    None
                }
                _ => None,
            };

            if comp_op.is_some() {
                // Handle "is not" compound operator
                if matches!(token.token_type, TokenType::Is)
                    && let Some(next_token) = self.tokens.get(self.current + 1)
                    && matches!(next_token.token_type, TokenType::Not)
                {
                    self.advance(); // consume "is"
                    self.advance(); // consume "not"
                    return Some(CompOp::IsNot);
                }

                self.advance(); // consume the operator
            }

            comp_op
        } else {
            None
        }
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

                // Tuple literals (parentheses)
                TokenType::LeftParen => self.parse_tuple_or_parenthesized_expr(),

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

    /// Parse a tuple literal or parenthesized expression: (expr1, expr2, ...) or (expr)
    fn parse_tuple_or_parenthesized_expr(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Consume '('
        if !self.match_token(&TokenType::LeftParen) {
            return Err(ParseError::UnexpectedToken {
                expected: "'('".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Handle empty tuple ()
        if self.match_token(&TokenType::RightParen) {
            self.advance();
            let end_location = self.previous_location();
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));
            return Ok(Node::Tuple(Tuple {
                elements: Vec::new(),
                source,
            }));
        }

        // Parse first element
        let first_element = self.parse_expression()?;

        // Check if this is a tuple (has comma) or just parenthesized expression
        if self.match_token(&TokenType::Comma) {
            // This is a tuple
            let mut elements = vec![first_element];

            while self.match_token(&TokenType::Comma) {
                self.advance(); // consume ','

                // Allow trailing comma before ')'
                if self.match_token(&TokenType::RightParen) {
                    break;
                }

                elements.push(self.parse_expression()?);
            }

            // Consume ')'
            if !self.match_token(&TokenType::RightParen) {
                return Err(ParseError::UnexpectedToken {
                    expected: "')'".to_string(),
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

            Ok(Node::Tuple(Tuple { elements, source }))
        } else {
            // This is a parenthesized expression, just return the inner expression
            if !self.match_token(&TokenType::RightParen) {
                return Err(ParseError::UnexpectedToken {
                    expected: "')'".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                });
            }
            self.advance();

            Ok(first_element)
        }
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
        self.current_token().is_some_and(|token| {
            std::mem::discriminant(&token.token_type) == std::mem::discriminant(token_type)
        })
    }

    fn is_comment(&self) -> bool {
        matches!(
            self.current_token().map(|t| &t.token_type),
            Some(TokenType::Comment(_))
        )
    }

    fn current_token_string(&self) -> String {
        self.current_token().map_or_else(
            || "end of file".to_string(),
            |token| format!("{:?}", token.token_type),
        )
    }

    fn current_location(&self) -> SourceLocation {
        self.current_token().map_or_else(
            || {
                self.tokens.last().map_or_else(
                    || SourceLocation::single_char(1, 1, 0),
                    |last_token| last_token.location.clone(),
                )
            },
            |token| token.location.clone(),
        )
    }

    fn previous_location(&self) -> SourceLocation {
        self.previous()
            .map_or_else(|| self.current_location(), |token| token.location.clone())
    }

    /// Get the start location of a node
    fn get_node_start_location(&self, _node: &Node) -> SourceLocation {
        // For now, use the current location as a fallback
        // In a more complete implementation, you'd extract location from the node
        self.current_location()
    }

    /// Get the end location of a node
    fn get_node_end_location(&self, _node: &Node) -> SourceLocation {
        // For now, use the current location as a fallback
        // In a more complete implementation, you'd extract location from the node
        self.current_location()
    }

    /// Parse an if statement: if condition: body [elif condition: body]* [else: body]
    fn parse_if_statement(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Consume 'if' keyword
        if !self.match_token(&TokenType::If) {
            return Err(ParseError::UnexpectedToken {
                expected: "'if'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Parse condition
        let test = self.parse_expression()?;

        // Expect ':'
        if !self.match_token(&TokenType::Colon) {
            return Err(ParseError::UnexpectedToken {
                expected: "':'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Parse if body
        let body = self.parse_block()?;

        // Consume the dedent that ends the if block
        if self.match_token(&TokenType::Dedent) {
            self.advance();
        }

        // Parse elif/else clauses
        let mut else_body = Vec::new();

        // Check for elif/else at the same indentation level
        while self.match_token(&TokenType::Elif) || self.match_token(&TokenType::Else) {
            if self.match_token(&TokenType::Elif) {
                self.advance(); // consume 'elif'

                // Parse elif condition
                let elif_test = self.parse_expression()?;

                // Expect ':'
                if !self.match_token(&TokenType::Colon) {
                    return Err(ParseError::UnexpectedToken {
                        expected: "':'".to_string(),
                        found: self.current_token_string(),
                        location: self.current_location(),
                    });
                }
                self.advance();

                // Parse elif body
                let elif_body = self.parse_block()?;

                // Consume dedent
                if self.match_token(&TokenType::Dedent) {
                    self.advance();
                }

                // Create a nested if statement for the elif
                let elif_start = self.current_location();
                let elif_end = self.previous_location();
                let elif_source = Some(NodeSource::from_source_location(&elif_start, &elif_end));

                let elif_if = Node::If(If {
                    test: Box::new(elif_test),
                    body: elif_body,
                    else_: Vec::new(), // Will be filled if there are more elif/else clauses
                    source: elif_source,
                });

                else_body = vec![elif_if];
                // Continue to check for more elif/else clauses
            } else if self.match_token(&TokenType::Else) {
                self.advance(); // consume 'else'

                // Expect ':'
                if !self.match_token(&TokenType::Colon) {
                    return Err(ParseError::UnexpectedToken {
                        expected: "':'".to_string(),
                        found: self.current_token_string(),
                        location: self.current_location(),
                    });
                }
                self.advance();

                // Parse else body
                else_body = self.parse_block()?;

                // Consume dedent
                if self.match_token(&TokenType::Dedent) {
                    self.advance();
                }

                break; // else clause ends the if statement
            }
        }

        let end_location = self.previous_location();
        let source = Some(NodeSource::from_source_location(
            &start_location,
            &end_location,
        ));

        Ok(Node::If(If {
            test: Box::new(test),
            body,
            else_: else_body,
            source,
        }))
    }

    /// Parse a block of statements (typically after ':')
    fn parse_block(&mut self) -> Result<Vec<Node>, ParseError> {
        let mut statements = Vec::new();

        // Expect newline after ':'
        if self.match_token(&TokenType::Newline) {
            self.advance();
        }

        // Expect indentation
        if !self.match_token(&TokenType::Indent) {
            return Err(ParseError::UnexpectedToken {
                expected: "indented block".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume indent

        // Parse statements until dedent
        while !self.is_at_end() && !self.match_token(&TokenType::Dedent) {
            // Skip newlines and comments
            if self.match_token(&TokenType::Newline) || self.is_comment() {
                self.advance();
                continue;
            }

            let stmt = self.parse_statement()?;
            statements.push(stmt);
        }

        // Note: Don't consume dedent here - let the calling function decide when to consume it

        if statements.is_empty() {
            return Err(ParseError::InvalidSyntax {
                message: "Expected at least one statement in block".to_string(),
                location: self.current_location(),
            });
        }

        Ok(statements)
    }
}
