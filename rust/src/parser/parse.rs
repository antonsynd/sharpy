use crate::ast::node::{
    Arg, Arguments, Assign, Attribute, BinaryOp, BinaryOp_, Call, CompOp, Compare, Constant,
    ConstantValue, Dict, If, Lambda, List, Name, Node, NodeSource, Pass, Return, Set, Subscript,
    Tuple, TypedName, UnaryOp, UnaryOp_, While,
};
use crate::ast::types::{GenericType, OptionalType, QualifiedType, TypeName};
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

        if self.match_token(&TokenType::While) {
            return self.parse_while_statement();
        }

        // Check for simple statements
        if self.match_token(&TokenType::Pass) {
            return self.parse_pass_statement();
        }

        if self.match_token(&TokenType::Return) {
            return self.parse_return_statement();
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
                    TokenType::Comma | TokenType::Name(_) => pos += 1, // Continue through tuple elements and names
                    TokenType::Colon => {
                        // Skip type annotation - handle complex types
                        pos += 1;
                        pos = self.skip_type_expression(pos);
                    }
                    _ => break, // Not an assignment pattern
                }
            } else {
                break;
            }
        }

        false
    }

    /// Skip over a type expression starting at the given position
    fn skip_type_expression(&self, mut pos: usize) -> usize {
        // Skip base type name
        if pos < self.tokens.len()
            && matches!(
                self.tokens.get(pos).map(|t| &t.token_type),
                Some(TokenType::Name(_))
            )
        {
            pos += 1;

            // Handle qualified types (e.g., app.Config)
            while pos < self.tokens.len()
                && matches!(
                    self.tokens.get(pos).map(|t| &t.token_type),
                    Some(TokenType::Dot)
                )
            {
                pos += 1; // skip dot
                if pos < self.tokens.len()
                    && matches!(
                        self.tokens.get(pos).map(|t| &t.token_type),
                        Some(TokenType::Name(_))
                    )
                {
                    pos += 1; // skip name after dot
                } else {
                    break;
                }
            }

            // Handle generic types (e.g., List[str])
            if pos < self.tokens.len()
                && matches!(
                    self.tokens.get(pos).map(|t| &t.token_type),
                    Some(TokenType::LeftBracket)
                )
            {
                pos += 1; // skip [
                let mut bracket_depth = 1;

                while pos < self.tokens.len() && bracket_depth > 0 {
                    match self.tokens.get(pos).map(|t| &t.token_type) {
                        Some(TokenType::LeftBracket) => bracket_depth += 1,
                        Some(TokenType::RightBracket) => bracket_depth -= 1,
                        _ => {}
                    }
                    pos += 1;
                }
            }

            // Handle optional types (e.g., int?)
            if pos < self.tokens.len()
                && matches!(
                    self.tokens.get(pos).map(|t| &t.token_type),
                    Some(TokenType::Question)
                )
            {
                pos += 1;
            }
        }
        pos
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
        self.parse_type_expression()
    }

    /// Parse an expression
    pub fn parse_expression(&mut self) -> Result<Node, ParseError> {
        self.parse_comparison()
    }

    /// Parse comparison expressions: expr == expr, expr < expr, etc.
    fn parse_comparison(&mut self) -> Result<Node, ParseError> {
        let left = self.parse_bitwise_or()?;

        let mut ops = Vec::new();
        let mut comparators = Vec::new();

        while let Some(comp_op) = self.parse_comparison_operator() {
            ops.push(comp_op);
            let right = self.parse_bitwise_or()?;
            comparators.push(right);
        }

        if ops.is_empty() {
            // No comparison operators found, return the bitwise expression
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

    /// Parse bitwise OR expressions: expr | expr
    fn parse_bitwise_or(&mut self) -> Result<Node, ParseError> {
        let mut left = self.parse_bitwise_xor()?;

        while let Some(token) = self.current_token()
            && matches!(token.token_type, TokenType::Vbar)
        {
            let start_location = self.get_node_start_location(&left);
            self.advance(); // consume '|'
            let right = self.parse_bitwise_xor()?;
            let end_location = self.get_node_end_location(&right);

            left = Node::BinaryOp(BinaryOp_ {
                left: Box::new(left),
                op: BinaryOp::BitwiseOr,
                right: Box::new(right),
                source: Some(NodeSource::from_source_location(
                    &start_location,
                    &end_location,
                )),
            });
        }

        Ok(left)
    }

    /// Parse bitwise XOR expressions: expr ^ expr
    fn parse_bitwise_xor(&mut self) -> Result<Node, ParseError> {
        let mut left = self.parse_bitwise_and()?;

        while let Some(token) = self.current_token()
            && matches!(token.token_type, TokenType::Circumflex)
        {
            let start_location = self.get_node_start_location(&left);
            self.advance(); // consume '^'
            let right = self.parse_bitwise_and()?;
            let end_location = self.get_node_end_location(&right);

            left = Node::BinaryOp(BinaryOp_ {
                left: Box::new(left),
                op: BinaryOp::BitwiseXor,
                right: Box::new(right),
                source: Some(NodeSource::from_source_location(
                    &start_location,
                    &end_location,
                )),
            });
        }

        Ok(left)
    }

    /// Parse bitwise AND expressions: expr & expr
    fn parse_bitwise_and(&mut self) -> Result<Node, ParseError> {
        let mut left = self.parse_shift()?;

        while let Some(token) = self.current_token()
            && matches!(token.token_type, TokenType::Amper)
        {
            let start_location = self.get_node_start_location(&left);
            self.advance(); // consume '&'
            let right = self.parse_shift()?;
            let end_location = self.get_node_end_location(&right);

            left = Node::BinaryOp(BinaryOp_ {
                left: Box::new(left),
                op: BinaryOp::BitwiseAnd,
                right: Box::new(right),
                source: Some(NodeSource::from_source_location(
                    &start_location,
                    &end_location,
                )),
            });
        }

        Ok(left)
    }

    /// Parse shift expressions: expr << expr, expr >> expr
    fn parse_shift(&mut self) -> Result<Node, ParseError> {
        let mut left = self.parse_addition()?;

        while let Some(token) = self.current_token() {
            let op = match token.token_type {
                TokenType::LeftShift => BinaryOp::LShift,
                TokenType::RightShift => BinaryOp::RShift,
                _ => break,
            };

            let start_location = self.get_node_start_location(&left);
            self.advance(); // consume the operator
            let right = self.parse_addition()?;
            let end_location = self.get_node_end_location(&right);

            left = Node::BinaryOp(BinaryOp_ {
                left: Box::new(left),
                op,
                right: Box::new(right),
                source: Some(NodeSource::from_source_location(
                    &start_location,
                    &end_location,
                )),
            });
        }

        Ok(left)
    }

    /// Parse addition and subtraction expressions: expr + expr, expr - expr
    fn parse_addition(&mut self) -> Result<Node, ParseError> {
        let mut left = self.parse_multiplication()?;

        while let Some(token) = self.current_token() {
            let op = match token.token_type {
                TokenType::Plus => BinaryOp::Add,
                TokenType::Minus => BinaryOp::Sub,
                _ => break,
            };

            let start_location = self.get_node_start_location(&left);
            self.advance(); // consume the operator
            let right = self.parse_multiplication()?;
            let end_location = self.get_node_end_location(&right);

            left = Node::BinaryOp(BinaryOp_ {
                left: Box::new(left),
                op,
                right: Box::new(right),
                source: Some(NodeSource::from_source_location(
                    &start_location,
                    &end_location,
                )),
            });
        }

        Ok(left)
    }

    /// Parse multiplication, division, and modulo expressions: expr * expr, expr / expr, expr % expr
    fn parse_multiplication(&mut self) -> Result<Node, ParseError> {
        let mut left = self.parse_power()?;

        while let Some(token) = self.current_token() {
            let op = match token.token_type {
                TokenType::Star => BinaryOp::Mult,
                TokenType::Slash => BinaryOp::Div,
                TokenType::DoubleSlash => BinaryOp::FloorDiv,
                TokenType::Percent => BinaryOp::Mod,
                TokenType::At => BinaryOp::MatMult,
                _ => break,
            };

            let start_location = self.get_node_start_location(&left);
            self.advance(); // consume the operator
            let right = self.parse_power()?;
            let end_location = self.get_node_end_location(&right);

            left = Node::BinaryOp(BinaryOp_ {
                left: Box::new(left),
                op,
                right: Box::new(right),
                source: Some(NodeSource::from_source_location(
                    &start_location,
                    &end_location,
                )),
            });
        }

        Ok(left)
    }

    /// Parse power expressions: expr ** expr (right-associative)
    fn parse_power(&mut self) -> Result<Node, ParseError> {
        let left = self.parse_postfix()?;

        if let Some(token) = self.current_token()
            && matches!(token.token_type, TokenType::DoubleStar)
        {
            let start_location = self.get_node_start_location(&left);
            self.advance(); // consume '**'
            let right = self.parse_power()?; // right-associative
            let end_location = self.get_node_end_location(&right);

            return Ok(Node::BinaryOp(BinaryOp_ {
                left: Box::new(left),
                op: BinaryOp::Pow,
                right: Box::new(right),
                source: Some(NodeSource::from_source_location(
                    &start_location,
                    &end_location,
                )),
            }));
        }

        Ok(left)
    }

    /// Parse postfix expressions: expr(args), expr.attr, expr[index]
    fn parse_postfix(&mut self) -> Result<Node, ParseError> {
        let mut expr = self.parse_unary()?;

        while let Some(token) = self.current_token() {
            match token.token_type {
                TokenType::LeftParen => {
                    // Function call: expr(args...)
                    expr = self.parse_function_call(expr)?;
                }
                TokenType::Dot => {
                    // Attribute access: expr.attr
                    expr = self.parse_attribute_access(expr)?;
                }
                TokenType::LeftBracket => {
                    // Subscript: expr[index]
                    expr = self.parse_subscript(expr)?;
                }
                _ => break,
            }
        }

        Ok(expr)
    }

    /// Parse a function call: function(arg1, arg2, key=value, ...)
    fn parse_function_call(&mut self, function: Node) -> Result<Node, ParseError> {
        let start_location = self.get_node_start_location(&function);

        // Consume '('
        if !self.match_token(&TokenType::LeftParen) {
            return Err(ParseError::UnexpectedToken {
                expected: "'('".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume '('

        let mut positional_args = Vec::new();
        let mut keyword_args = Vec::new();

        // Handle empty function call: func()
        if self.match_token(&TokenType::RightParen) {
            self.advance(); // consume ')'
            let end_location = self.previous_location();
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));

            return Ok(Node::Call(Call {
                function: Box::new(function),
                positional_args,
                keyword_args,
                source,
            }));
        }

        // Parse arguments
        loop {
            // Check if this looks like a keyword argument (name=value)
            if self.is_keyword_argument() {
                let keyword_arg = self.parse_keyword_argument()?;
                keyword_args.push(keyword_arg);
            } else {
                // Positional argument
                let arg = self.parse_expression()?;
                positional_args.push(arg);
            }

            if self.match_token(&TokenType::Comma) {
                self.advance(); // consume ','

                // Allow trailing comma
                if self.match_token(&TokenType::RightParen) {
                    break;
                }
            } else if self.match_token(&TokenType::RightParen) {
                break;
            } else {
                return Err(ParseError::UnexpectedToken {
                    expected: "',' or ')'".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                });
            }
        }

        // Consume ')'
        if !self.match_token(&TokenType::RightParen) {
            return Err(ParseError::UnexpectedToken {
                expected: "')'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume ')'

        let end_location = self.previous_location();
        let source = Some(NodeSource::from_source_location(
            &start_location,
            &end_location,
        ));

        Ok(Node::Call(Call {
            function: Box::new(function),
            positional_args,
            keyword_args,
            source,
        }))
    }

    /// Parse attribute access: expr.attr
    fn parse_attribute_access(&mut self, value: Node) -> Result<Node, ParseError> {
        let start_location = self.get_node_start_location(&value);

        // Consume '.'
        if !self.match_token(&TokenType::Dot) {
            return Err(ParseError::UnexpectedToken {
                expected: "'.'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume '.'

        // Expect an attribute name
        if let Some(token) = self.current_token() {
            if let TokenType::Name(name_type) = &token.token_type {
                let attr = name_type.name.clone();
                let end_location = token.location.clone();
                self.advance(); // consume attribute name

                let source = Some(NodeSource::from_source_location(
                    &start_location,
                    &end_location,
                ));

                Ok(Node::Attribute(Attribute {
                    value: Box::new(value),
                    attr,
                    source,
                }))
            } else {
                Err(ParseError::UnexpectedToken {
                    expected: "attribute name".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                })
            }
        } else {
            Err(ParseError::UnexpectedEof {
                expected: "attribute name".to_string(),
                location: self.current_location(),
            })
        }
    }

    /// Parse subscript: expr[index]
    fn parse_subscript(&mut self, value: Node) -> Result<Node, ParseError> {
        let start_location = self.get_node_start_location(&value);

        // Consume '['
        if !self.match_token(&TokenType::LeftBracket) {
            return Err(ParseError::UnexpectedToken {
                expected: "'['".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume '['

        // Parse the index expression
        let slice = self.parse_expression()?;

        // Consume ']'
        if !self.match_token(&TokenType::RightBracket) {
            return Err(ParseError::UnexpectedToken {
                expected: "']'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        let end_location = self.current_location();
        self.advance(); // consume ']'

        let source = Some(NodeSource::from_source_location(
            &start_location,
            &end_location,
        ));

        Ok(Node::Subscript(Subscript {
            value: Box::new(value),
            slice: Box::new(slice),
            source,
        }))
    }

    /// Parse dict or set literal: {key: value, ...} or {elem1, elem2, ...}
    fn parse_dict_or_set(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Consume '{'
        if !self.match_token(&TokenType::LeftBrace) {
            return Err(ParseError::UnexpectedToken {
                expected: "'{'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume '{'

        // Handle empty dict/set: {}
        if self.match_token(&TokenType::RightBrace) {
            let end_location = self.current_location();
            self.advance(); // consume '}'
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));
            // Empty {} is interpreted as an empty dict by default
            return Ok(Node::Dict(Dict {
                keys: vec![],
                values: vec![],
                source,
            }));
        }

        // Parse first element to determine if it's a dict or set
        let first_expr = self.parse_expression()?;

        // Check if this is a dict (has ':') or set (has ',' or '}')
        if self.match_token(&TokenType::Colon) {
            // It's a dict: {key: value, ...}
            self.advance(); // consume ':'
            let first_value = self.parse_expression()?;

            let mut keys = vec![Some(first_expr)];
            let mut values = vec![first_value];

            // Parse additional key-value pairs
            while self.match_token(&TokenType::Comma) {
                self.advance(); // consume ','

                // Allow trailing comma
                if self.match_token(&TokenType::RightBrace) {
                    break;
                }

                let key = self.parse_expression()?;

                if !self.match_token(&TokenType::Colon) {
                    return Err(ParseError::UnexpectedToken {
                        expected: "':'".to_string(),
                        found: self.current_token_string(),
                        location: self.current_location(),
                    });
                }
                self.advance(); // consume ':'

                let value = self.parse_expression()?;
                keys.push(Some(key));
                values.push(value);
            }

            // Consume '}'
            if !self.match_token(&TokenType::RightBrace) {
                return Err(ParseError::UnexpectedToken {
                    expected: "'}'".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                });
            }
            let end_location = self.current_location();
            self.advance(); // consume '}'

            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));

            Ok(Node::Dict(Dict {
                keys,
                values,
                source,
            }))
        } else {
            // It's a set: {elem1, elem2, ...}
            let mut elements = vec![first_expr];

            // Parse additional elements
            while self.match_token(&TokenType::Comma) {
                self.advance(); // consume ','

                // Allow trailing comma
                if self.match_token(&TokenType::RightBrace) {
                    break;
                }

                let element = self.parse_expression()?;
                elements.push(element);
            }

            // Consume '}'
            if !self.match_token(&TokenType::RightBrace) {
                return Err(ParseError::UnexpectedToken {
                    expected: "'}'".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                });
            }
            let end_location = self.current_location();
            self.advance(); // consume '}'

            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));

            Ok(Node::Set(Set { elements, source }))
        }
    }

    /// Parse lambda expression: lambda args: body
    fn parse_lambda(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Consume 'lambda'
        if !self.match_token(&TokenType::Lambda) {
            return Err(ParseError::UnexpectedToken {
                expected: "'lambda'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume 'lambda'

        // Parse arguments (optional)
        let args = if self.match_token(&TokenType::Colon) {
            // No arguments: lambda: body
            Arguments {
                vararg: None,
                args: vec![],
            }
        } else {
            self.parse_lambda_arguments()?
        };

        // Consume ':'
        if !self.match_token(&TokenType::Colon) {
            return Err(ParseError::UnexpectedToken {
                expected: "':'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume ':'

        // Parse optional return type annotation
        let return_type = if self.match_token(&TokenType::RArrow) {
            self.advance(); // consume '->'
            Some(Box::new(self.parse_type_expression()?))
        } else {
            None
        };

        // Parse body expression
        let body = self.parse_expression()?;
        let end_location = self.get_node_end_location(&body);

        let source = Some(NodeSource::from_source_location(
            &start_location,
            &end_location,
        ));

        Ok(Node::Lambda(Lambda {
            args,
            return_type,
            body: Box::new(body),
            source,
        }))
    }

    /// Parse lambda arguments: arg1, arg2, arg3 (untyped for now)
    fn parse_lambda_arguments(&mut self) -> Result<Arguments, ParseError> {
        let mut args = Vec::new();

        // Parse first argument
        if let Some(token) = self.current_token() {
            if let TokenType::Name(name_type) = &token.token_type {
                let arg_name = name_type.name.clone();
                let token_location = token.location.clone();
                self.advance(); // consume argument name

                args.push(Arg {
                    name: arg_name,
                    type_: None,   // No type annotations for lambda args yet
                    default: None, // No default values for lambda args yet
                    source: Some(NodeSource::from_source_location(
                        &token_location,
                        &token_location,
                    )),
                });
            } else {
                return Err(ParseError::UnexpectedToken {
                    expected: "argument name".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                });
            }
        } else {
            return Err(ParseError::UnexpectedEof {
                expected: "argument name".to_string(),
                location: self.current_location(),
            });
        }

        // Parse additional arguments
        while self.match_token(&TokenType::Comma) {
            self.advance(); // consume ','

            // Check if we've reached the colon (end of args)
            if self.match_token(&TokenType::Colon) {
                break;
            }

            if let Some(token) = self.current_token() {
                if let TokenType::Name(name_type) = &token.token_type {
                    let arg_name = name_type.name.clone();
                    let token_location = token.location.clone();
                    self.advance(); // consume argument name

                    args.push(Arg {
                        name: arg_name,
                        type_: None,
                        default: None,
                        source: Some(NodeSource::from_source_location(
                            &token_location,
                            &token_location,
                        )),
                    });
                } else {
                    return Err(ParseError::UnexpectedToken {
                        expected: "argument name".to_string(),
                        found: self.current_token_string(),
                        location: self.current_location(),
                    });
                }
            } else {
                return Err(ParseError::UnexpectedEof {
                    expected: "argument name".to_string(),
                    location: self.current_location(),
                });
            }
        }

        Ok(Arguments {
            vararg: None, // No varargs support for lambdas yet
            args,
        })
    }

    /// Check if the current position looks like a keyword argument (name=value)
    fn is_keyword_argument(&self) -> bool {
        if let Some(token) = self.current_token()
            && matches!(token.token_type, TokenType::Name(_))
        {
            // Look ahead for '='
            if let Some(next_token) = self.tokens.get(self.current + 1)
                && matches!(next_token.token_type, TokenType::Equal)
            {
                return true;
            }
        }
        false
    }

    /// Parse a keyword argument: name=value
    fn parse_keyword_argument(&mut self) -> Result<Node, ParseError> {
        // For now, let's store keyword arguments as a special structure
        // This is a temporary solution - in a full implementation you might want
        // to modify the Call AST node to handle keyword arguments differently

        // Parse the argument name
        let _arg_name = if let Some(token) = self.current_token()
            && let TokenType::Name(name_type) = &token.token_type
        {
            let arg_name = name_type.name.clone();
            self.advance(); // consume name
            arg_name
        } else {
            return Err(ParseError::UnexpectedToken {
                expected: "argument name".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        };

        // Expect '='
        if !self.match_token(&TokenType::Equal) {
            return Err(ParseError::UnexpectedToken {
                expected: "'='".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume '='

        // Parse the value
        let value = self.parse_expression()?;

        // For now, just return the value
        // TODO: Properly handle keyword argument names
        Ok(value)
    }

    /// Parse unary expressions: +expr, -expr, not expr, ~expr
    fn parse_unary(&mut self) -> Result<Node, ParseError> {
        if let Some(token) = self.current_token() {
            let unary_op = match token.token_type {
                TokenType::Plus => Some(UnaryOp::UnaryAdd),
                TokenType::Minus => Some(UnaryOp::UnarySub),
                TokenType::Not => Some(UnaryOp::Not),
                TokenType::Tilde => Some(UnaryOp::Invert),
                _ => None,
            };

            if let Some(op) = unary_op {
                let start_location = token.location.clone();
                self.advance(); // consume the unary operator
                let operand = self.parse_unary()?; // right-associative
                let end_location = self.get_node_end_location(&operand);

                return Ok(Node::UnaryOp(UnaryOp_ {
                    op,
                    operand: Box::new(operand),
                    source: Some(NodeSource::from_source_location(
                        &start_location,
                        &end_location,
                    )),
                }));
            }
        }

        // No unary operator found, parse as primary
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

                // Dict or Set literals
                TokenType::LeftBrace => self.parse_dict_or_set(),

                // Tuple literals (parentheses)
                TokenType::LeftParen => self.parse_tuple_or_parenthesized_expr(),

                // Lambda expressions
                TokenType::Lambda => self.parse_lambda(),

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

    /// Parse a while statement: while condition: body [else: body]
    fn parse_while_statement(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Consume 'while' keyword
        if !self.match_token(&TokenType::While) {
            return Err(ParseError::UnexpectedToken {
                expected: "'while'".to_string(),
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

        // Parse while body
        let body = self.parse_block()?;

        // Consume the dedent that ends the while block
        if self.match_token(&TokenType::Dedent) {
            self.advance();
        }

        // Parse optional else clause
        let mut else_body = Vec::new();

        // Check for else at the same indentation level
        if self.match_token(&TokenType::Else) {
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
        }

        let end_location = self.previous_location();
        let source = Some(NodeSource::from_source_location(
            &start_location,
            &end_location,
        ));

        Ok(Node::While(While {
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

    /// Parse a pass statement, e.g., `pass`
    fn parse_pass_statement(&mut self) -> Result<Node, ParseError> {
        // Get the location before consuming the token
        let location = self.current_location();

        // Consume 'pass' keyword
        if !self.match_token(&TokenType::Pass) {
            return Err(ParseError::UnexpectedToken {
                expected: "'pass'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        Ok(Node::Pass(Pass {
            source: Some(NodeSource::from_source_location(&location, &location)),
        }))
    }

    /// Parse a return statement, e.g., `return` or `return expr`
    fn parse_return_statement(&mut self) -> Result<Node, ParseError> {
        // Get the location before consuming the token
        let start_location = self.current_location();

        // Consume 'return' keyword
        if !self.match_token(&TokenType::Return) {
            return Err(ParseError::UnexpectedToken {
                expected: "'return'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Check if there's an expression to return
        let (value, end_location) = if self.is_at_end() {
            (None, start_location.clone())
        } else if let Some(token) = self.current_token() {
            match token.token_type {
                TokenType::Newline | TokenType::Dedent => (None, start_location.clone()),
                _ => {
                    let expr = self.parse_expression()?;
                    let end_loc = self.current_location();
                    (Some(Box::new(expr)), end_loc)
                }
            }
        } else {
            (None, start_location.clone())
        };

        Ok(Node::Return(Return {
            value,
            source: Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            )),
        }))
    }

    /// Parse a type expression, e.g., `int`, `list[str]`, `collections.defaultdict[str, int]`, `int?`
    fn parse_type_expression(&mut self) -> Result<Node, ParseError> {
        let base_type = self.parse_base_type()?;

        // Check for generic parameters: type[arg1, arg2, ...]
        if self.match_token(&TokenType::LeftBracket) {
            self.advance(); // consume '['

            let mut type_args = Vec::new();

            // Parse first type argument
            if !self.match_token(&TokenType::RightBracket) {
                type_args.push(self.parse_type_expression()?);

                // Parse remaining type arguments
                while self.match_token(&TokenType::Comma) {
                    self.advance(); // consume ','
                    if self.match_token(&TokenType::RightBracket) {
                        break; // trailing comma
                    }
                    type_args.push(self.parse_type_expression()?);
                }
            }

            // Expect closing bracket
            if !self.match_token(&TokenType::RightBracket) {
                return Err(ParseError::UnexpectedToken {
                    expected: "']'".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                });
            }
            self.advance(); // consume ']'

            let end_location = self.current_location();
            let generic_type = Node::GenericType(GenericType::with_source(
                Box::new(base_type),
                type_args,
                NodeSource::from_source_location(&self.current_location(), &end_location),
            ));

            return Ok(self.maybe_parse_optional(generic_type));
        }

        // Check for optional: type?
        Ok(self.maybe_parse_optional(base_type))
    }

    /// Parse a base type (`TypeName` or `QualifiedType`)
    fn parse_base_type(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Parse the first component
        let first_name = self.parse_name_token()?;
        let mut components = vec![first_name];

        // Parse additional components separated by dots
        while self.match_token(&TokenType::Dot) {
            self.advance(); // consume '.'
            let name = self.parse_name_token()?;
            components.push(name);
        }

        let end_location = self.current_location();

        if components.len() == 1 {
            // Simple type name
            Ok(Node::TypeName(TypeName::with_source(
                components.into_iter().next().unwrap(),
                NodeSource::from_source_location(&start_location, &end_location),
            )))
        } else {
            // Qualified type name
            let name = components.pop().unwrap(); // Last component is the type name
            Ok(Node::QualifiedType(QualifiedType::with_source(
                components, // Remaining components are the module path
                name,
                NodeSource::from_source_location(&start_location, &end_location),
            )))
        }
    }

    /// Parse a name token and return the string
    fn parse_name_token(&mut self) -> Result<String, ParseError> {
        if let Some(token) = self.current_token()
            && let TokenType::Name(name_type) = &token.token_type
        {
            let name = name_type.name.clone();
            self.advance();
            return Ok(name);
        }

        Err(ParseError::UnexpectedToken {
            expected: "identifier".to_string(),
            found: self.current_token_string(),
            location: self.current_location(),
        })
    }

    /// Check for optional suffix and wrap the type if found
    fn maybe_parse_optional(&mut self, base_type: Node) -> Node {
        if self.match_token(&TokenType::Question) {
            let start_location = self.get_node_start_location(&base_type);
            self.advance(); // consume '?'
            let end_location = self.current_location();

            Node::OptionalType(OptionalType::with_source(
                Box::new(base_type),
                NodeSource::from_source_location(&start_location, &end_location),
            ))
        } else {
            base_type
        }
    }
}
