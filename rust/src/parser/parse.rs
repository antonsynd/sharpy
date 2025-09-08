use crate::ast::node::{
    Alias, Arg, Arguments, Assign, Attribute, BinaryOp, BinaryOp_, BoolOp, BoolOp_, Call, ClassDef,
    CompOp, Compare, Constant, ConstantValue, Dict, ExceptHandler, For, FunctionDef, If, IfExp,
    Import, ImportFrom, Lambda, List, Name, NamedExpr, Node, NodeSource, Pass, PropertyDef,
    ProtocolDef, Return, Set, StructDef, Subscript, Try, Tuple, TypedName, UnaryOp, UnaryOp_,
    While,
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
        // Check for import statements first
        if self.match_token(&TokenType::Import) {
            self.advance(); // consume 'import'
            return self.parse_import_statement();
        }

        if self.match_token(&TokenType::From) {
            self.advance(); // consume 'from'
            return self.parse_import_from_statement();
        }

        // Check for type definitions
        if self.match_token(&TokenType::Class) {
            self.advance(); // consume 'class'
            return self.parse_class_definition();
        }

        if self.match_token(&TokenType::Struct) {
            self.advance(); // consume 'struct'
            return self.parse_struct_definition();
        }

        if self.match_token(&TokenType::Protocol) {
            self.advance(); // consume 'protocol'
            return self.parse_protocol_definition();
        }

        // Check for property definitions (contextual keywords)
        if self.is_property_definition() {
            return self.parse_property_definition();
        }

        // Check for function definitions
        if self.match_token(&TokenType::Def) {
            self.advance(); // consume 'def'
            return self.parse_function_definition();
        }

        // Check for control flow statements
        if self.match_token(&TokenType::If) {
            return self.parse_if_statement();
        }

        if self.match_token(&TokenType::While) {
            return self.parse_while_statement();
        }

        if self.match_token(&TokenType::For) {
            return self.parse_for_statement();
        }

        if self.match_token(&TokenType::Try) {
            return self.parse_try_statement();
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

        // Skip over potential assignment targets (names, subscripts, attributes, tuples, etc.)
        while pos < self.tokens.len() {
            if let Some(token) = self.tokens.get(pos) {
                match token.token_type {
                    TokenType::Equal => return true, // Found assignment operator
                    TokenType::Comma | TokenType::Name(_) => pos += 1, // Continue through tuple elements and names
                    TokenType::LeftBracket => {
                        // Skip over subscript: name[index]
                        pos += 1; // skip '['
                        pos = self.skip_expression(pos); // skip the index expression
                        if pos < self.tokens.len()
                            && matches!(
                                self.tokens.get(pos).map(|t| &t.token_type),
                                Some(TokenType::RightBracket)
                            )
                        {
                            pos += 1; // skip ']'
                        }
                    }
                    TokenType::Dot => {
                        // Skip over attribute access: name.attr
                        pos += 1; // skip '.'
                        if pos < self.tokens.len()
                            && matches!(
                                self.tokens.get(pos).map(|t| &t.token_type),
                                Some(TokenType::Name(_))
                            )
                        {
                            pos += 1; // skip attribute name
                        }
                    }
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

    /// Skip over an expression (simple implementation for lookahead)
    fn skip_expression(&self, mut pos: usize) -> usize {
        let mut paren_depth = 0;
        let mut bracket_depth = 0;
        let mut brace_depth = 0;

        while pos < self.tokens.len() {
            if let Some(token) = self.tokens.get(pos) {
                match token.token_type {
                    TokenType::LeftParen => paren_depth += 1,
                    TokenType::RightParen => {
                        if paren_depth > 0 {
                            paren_depth -= 1;
                        } else {
                            break; // Unmatched closing paren
                        }
                    }
                    TokenType::LeftBracket => bracket_depth += 1,
                    TokenType::RightBracket => {
                        if bracket_depth > 0 {
                            bracket_depth -= 1;
                        } else {
                            break; // Unmatched closing bracket
                        }
                    }
                    TokenType::LeftBrace => brace_depth += 1,
                    TokenType::RightBrace => {
                        if brace_depth > 0 {
                            brace_depth -= 1;
                        } else {
                            break; // Unmatched closing brace
                        }
                    }
                    TokenType::Comma | TokenType::Equal => {
                        if paren_depth == 0 && bracket_depth == 0 && brace_depth == 0 {
                            break; // End of expression
                        }
                    }
                    _ => {}
                }
                pos += 1;
            } else {
                break;
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

    /// Parse a single target element: name, name[index], or name: type
    fn parse_target_element(&mut self) -> Result<Node, ParseError> {
        // Parse a postfix expression to handle subscripts like name[index]
        let target = self.parse_postfix()?;

        // Validate that this is a valid assignment target
        match &target {
            Node::Name(_) | Node::Subscript(_) | Node::Attribute(_) => {
                // These are valid assignment targets
            }
            _ => {
                return Err(ParseError::InvalidSyntax {
                    message: "Invalid assignment target".to_string(),
                    location: self.current_location(),
                });
            }
        }

        // Check if this has a type annotation (only valid for simple names)
        if self.match_token(&TokenType::Colon) {
            self.advance(); // consume ':'
            let type_annotation = self.parse_type_annotation()?;

            if let Node::Name(Name { id, source }) = target {
                Ok(Node::TypedName(TypedName {
                    id,
                    type_: Box::new(type_annotation),
                    source,
                }))
            } else {
                Err(ParseError::InvalidSyntax {
                    message: "Type annotations are only allowed on simple names".to_string(),
                    location: self.current_location(),
                })
            }
        } else {
            // Untyped target
            Ok(target)
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
    ///
    /// # Errors
    /// Returns `ParseError` if:
    /// - The input contains invalid syntax
    /// - Unexpected tokens are encountered
    /// - The expression is malformed
    pub fn parse_expression(&mut self) -> Result<Node, ParseError> {
        self.parse_ternary()
    }

    /// Parse ternary conditional expressions: expr if test else expr
    fn parse_ternary(&mut self) -> Result<Node, ParseError> {
        let body = self.parse_or()?;

        if self.match_token(&TokenType::If) {
            let start_location = self.current_location();
            self.advance(); // consume 'if'

            let test = self.parse_or()?;

            if !self.match_token(&TokenType::Else) {
                return Err(ParseError::InvalidSyntax {
                    message: "Expected 'else' in ternary expression".to_string(),
                    location: self.current_location(),
                });
            }
            self.advance(); // consume 'else'

            let else_ = self.parse_ternary()?; // Right-associative

            let end_location = self.previous_location();
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));

            Ok(Node::IfExp(IfExp {
                test: Box::new(test),
                body: Box::new(body),
                else_: Box::new(else_),
                source,
            }))
        } else {
            Ok(body)
        }
    }

    /// Parse boolean OR expressions: expr or expr
    fn parse_or(&mut self) -> Result<Node, ParseError> {
        let mut expr = self.parse_and()?;

        if self.match_token(&TokenType::Or) {
            let start_location = self.current_location();
            let mut values = vec![expr];

            while self.match_token(&TokenType::Or) {
                self.advance(); // consume 'or'
                values.push(self.parse_and()?);
            }

            let end_location = self.previous_location();
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));

            expr = Node::BoolOp(BoolOp_ {
                op: BoolOp::Or,
                values,
                source,
            });
        }

        Ok(expr)
    }

    /// Parse boolean AND expressions: expr and expr
    fn parse_and(&mut self) -> Result<Node, ParseError> {
        let mut expr = self.parse_not()?;

        if self.match_token(&TokenType::And) {
            let start_location = self.current_location();
            let mut values = vec![expr];

            while self.match_token(&TokenType::And) {
                self.advance(); // consume 'and'
                values.push(self.parse_not()?);
            }

            let end_location = self.previous_location();
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));

            expr = Node::BoolOp(BoolOp_ {
                op: BoolOp::And,
                values,
                source,
            });
        }

        Ok(expr)
    }

    /// Parse boolean NOT expressions: not expr
    fn parse_not(&mut self) -> Result<Node, ParseError> {
        if self.match_token(&TokenType::Not) {
            let start_location = self.current_location();
            self.advance(); // consume 'not'

            let operand = self.parse_not()?; // Allow chaining: not not x
            let end_location = self.previous_location();
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));

            Ok(Node::UnaryOp(UnaryOp_ {
                op: UnaryOp::Not,
                operand: Box::new(operand),
                source,
            }))
        } else {
            self.parse_named_expression()
        }
    }

    /// Parse named expressions (walrus operator): name := expr
    fn parse_named_expression(&mut self) -> Result<Node, ParseError> {
        let expr = self.parse_comparison()?;

        if self.match_token(&TokenType::ColonEqual) {
            let start_location = self.current_location();
            self.advance(); // consume ':='

            let value = self.parse_named_expression()?; // Right-associative
            let end_location = self.previous_location();
            let source = Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            ));

            Ok(Node::NamedExpr(NamedExpr {
                target: Box::new(expr),
                value: Box::new(value),
                source,
            }))
        } else {
            Ok(expr)
        }
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
            Arguments { args: vec![] }
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

        Ok(Arguments { args })
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

    /// Parse a for statement: for target in iterable:
    fn parse_for_statement(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Consume 'for' keyword
        if !self.match_token(&TokenType::For) {
            return Err(ParseError::UnexpectedToken {
                expected: "'for'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Parse target (variable or destructuring pattern)
        let target = self.parse_assignment_target()?;

        // Expect 'in' keyword
        if !self.match_token(&TokenType::In) {
            return Err(ParseError::UnexpectedToken {
                expected: "'in'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Parse iterable expression
        let iter = self.parse_expression()?;

        // Expect ':'
        if !self.match_token(&TokenType::Colon) {
            return Err(ParseError::UnexpectedToken {
                expected: "':'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Parse for body
        let body = self.parse_block()?;

        // Consume the dedent that ends the for block
        if self.match_token(&TokenType::Dedent) {
            self.advance();
        }

        // Check for optional else clause
        let mut else_body = Vec::new();
        if self.match_token(&TokenType::Else) {
            self.advance();

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

        Ok(Node::For(For {
            target: Box::new(target),
            iter: Box::new(iter),
            body,
            else_: else_body,
            source,
        }))
    }

    /// Parse a try statement: try: ... except: ... finally: ...
    fn parse_try_statement(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Consume 'try' keyword
        if !self.match_token(&TokenType::Try) {
            return Err(ParseError::UnexpectedToken {
                expected: "'try'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Expect ':'
        if !self.match_token(&TokenType::Colon) {
            return Err(ParseError::UnexpectedToken {
                expected: "':'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Parse try body
        let body = self.parse_block()?;

        // Consume the dedent that ends the try block
        if self.match_token(&TokenType::Dedent) {
            self.advance();
        }

        // Parse exception handlers
        let mut handlers = Vec::new();
        while self.match_token(&TokenType::Except) {
            handlers.push(self.parse_except_handler()?);
        }

        // Parse optional else clause
        let mut else_body = Vec::new();
        if self.match_token(&TokenType::Else) {
            self.advance();

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

        // Parse optional finally clause
        let mut finalbody = Vec::new();
        if self.match_token(&TokenType::Finally) {
            self.advance();

            // Expect ':'
            if !self.match_token(&TokenType::Colon) {
                return Err(ParseError::UnexpectedToken {
                    expected: "':'".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                });
            }
            self.advance();

            // Parse finally body
            finalbody = self.parse_block()?;

            // Consume dedent
            if self.match_token(&TokenType::Dedent) {
                self.advance();
            }
        }

        // Validate that we have at least one handler or a finally clause
        if handlers.is_empty() && finalbody.is_empty() {
            return Err(ParseError::InvalidSyntax {
                message: "try statement must have at least one except or finally clause"
                    .to_string(),
                location: self.current_location(),
            });
        }

        let end_location = self.previous_location();
        let source = Some(NodeSource::from_source_location(
            &start_location,
            &end_location,
        ));

        Ok(Node::Try(Try {
            body,
            handlers,
            else_: else_body,
            finalbody,
            source,
        }))
    }

    /// Parse an exception handler: except [type [as name]]:
    fn parse_except_handler(&mut self) -> Result<ExceptHandler, ParseError> {
        let start_location = self.current_location();

        // Consume 'except' keyword
        if !self.match_token(&TokenType::Except) {
            return Err(ParseError::UnexpectedToken {
                expected: "'except'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Parse optional exception type
        let mut type_ = None;
        let mut name = None;

        // Check if there's an exception type specified
        if !self.match_token(&TokenType::Colon) {
            type_ = Some(Box::new(self.parse_expression()?));

            // Check for optional 'as name' clause
            if self.match_token(&TokenType::As) {
                self.advance();

                // Expect identifier
                if let Some(token) = self.current_token() {
                    if let TokenType::Name(name_type) = &token.token_type {
                        name = Some(name_type.name.clone());
                        self.advance();
                    } else {
                        return Err(ParseError::UnexpectedToken {
                            expected: "identifier".to_string(),
                            found: self.current_token_string(),
                            location: self.current_location(),
                        });
                    }
                } else {
                    return Err(ParseError::UnexpectedEof {
                        expected: "identifier".to_string(),
                        location: self.current_location(),
                    });
                }
            }
        }

        // Expect ':'
        if !self.match_token(&TokenType::Colon) {
            return Err(ParseError::UnexpectedToken {
                expected: "':'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance();

        // Parse handler body
        let body = self.parse_block()?;

        // Consume the dedent that ends the except block
        if self.match_token(&TokenType::Dedent) {
            self.advance();
        }

        let end_location = self.previous_location();
        let source = Some(NodeSource::from_source_location(
            &start_location,
            &end_location,
        ));

        Ok(ExceptHandler {
            type_,
            name,
            body,
            source,
        })
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

    /// Extract access modifier from a function name based on naming conventions
    /// Consume newlines and empty lines
    fn consume_newlines(&mut self) {
        while let Some(token) = self.current_token() {
            if matches!(token.token_type, TokenType::Newline) {
                self.advance();
            } else {
                break;
            }
        }
    }

    /// Parse a function definition: def name(args) -> `return_type`: body
    fn parse_function_definition(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Parse function name
        if let Some(token) = self.current_token() {
            if let TokenType::Name(name_type) = &token.token_type {
                let clean_name = name_type.name.clone();
                let access_modifier = match name_type.access_modifier {
                    crate::lexer::token::AccessModifier::Public => None,
                    crate::lexer::token::AccessModifier::Protected => Some("protected".to_string()),
                    crate::lexer::token::AccessModifier::Private => Some("private".to_string()),
                    crate::lexer::token::AccessModifier::Internal => Some("internal".to_string()),
                    crate::lexer::token::AccessModifier::File => Some("file".to_string()),
                };
                self.advance(); // consume name

                // Parse argument list
                if !self.match_token(&TokenType::LeftParen) {
                    return Err(ParseError::UnexpectedToken {
                        expected: "'('".to_string(),
                        found: self.current_token_string(),
                        location: self.current_location(),
                    });
                }
                self.advance(); // consume '('

                let args = if self.match_token(&TokenType::RightParen) {
                    // Empty parameter list
                    self.advance();
                    Arguments { args: vec![] }
                } else {
                    let args = self.parse_function_arguments()?;
                    if !self.match_token(&TokenType::RightParen) {
                        return Err(ParseError::UnexpectedToken {
                            expected: "')'".to_string(),
                            found: self.current_token_string(),
                            location: self.current_location(),
                        });
                    }
                    self.advance(); // consume ')'
                    args
                };

                // Parse optional return type
                let return_type = if self.match_token(&TokenType::RArrow) {
                    self.advance(); // consume '->'
                    Some(Box::new(self.parse_type_expression()?))
                } else {
                    None
                };

                // Parse colon and body
                if !self.match_token(&TokenType::Colon) {
                    return Err(ParseError::UnexpectedToken {
                        expected: "':'".to_string(),
                        found: self.current_token_string(),
                        location: self.current_location(),
                    });
                }
                self.advance(); // consume ':'
                self.consume_newlines();

                // Parse function body
                let body = self.parse_block()?;

                // Consume the dedent that ends the function block
                if self.match_token(&TokenType::Dedent) {
                    self.advance();
                }

                let end_location = self.current_location();

                Ok(Node::FunctionDef(FunctionDef {
                    access_modifier,
                    name: clean_name,
                    args,
                    decorators: vec![], // TODO: Add decorator support later
                    return_type,
                    body,
                    source: Some(NodeSource::from_source_location(
                        &start_location,
                        &end_location,
                    )),
                }))
            } else {
                Err(ParseError::UnexpectedToken {
                    expected: "function name".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                })
            }
        } else {
            Err(ParseError::UnexpectedToken {
                expected: "function name".to_string(),
                found: "end of input".to_string(),
                location: self.current_location(),
            })
        }
    }

    /// Parse function arguments (without the parentheses)
    fn parse_function_arguments(&mut self) -> Result<Arguments, ParseError> {
        let mut args = Vec::new();

        loop {
            // Parse argument name
            if let Some(token) = self.current_token() {
                if let TokenType::Name(name_type) = &token.token_type {
                    let arg_name = name_type.name.clone();
                    self.advance(); // consume name

                    // Parse optional type annotation
                    let arg_type = if self.match_token(&TokenType::Colon) {
                        self.advance(); // consume ':'
                        Some(Box::new(self.parse_type_expression()?))
                    } else {
                        None
                    };

                    // Parse optional default value
                    let default = if self.match_token(&TokenType::Equal) {
                        self.advance(); // consume '='
                        Some(Box::new(self.parse_expression()?))
                    } else {
                        None
                    };

                    args.push(Arg {
                        name: arg_name,
                        type_: arg_type,
                        default,
                        source: None, // TODO: Add source tracking for individual args
                    });

                    // Check for more arguments
                    if self.match_token(&TokenType::Comma) {
                        self.advance();
                        // Allow trailing comma
                        if self.match_token(&TokenType::RightParen) {
                            break;
                        }
                    } else {
                        break;
                    }
                } else {
                    return Err(ParseError::UnexpectedToken {
                        expected: "parameter name".to_string(),
                        found: self.current_token_string(),
                        location: self.current_location(),
                    });
                }
            } else {
                return Err(ParseError::UnexpectedToken {
                    expected: "parameter name".to_string(),
                    found: "end of input".to_string(),
                    location: self.current_location(),
                });
            }
        }

        Ok(Arguments { args })
    }

    /// Parse a class definition: class Name[T](Base): body
    fn parse_class_definition(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Parse class name with access modifier
        let (access_modifier, name) = self.parse_type_definition_name()?;

        // Parse optional generic parameters: [T, U, V]
        let type_params = self.parse_optional_generic_params()?;

        // Parse optional inheritance/protocol implementation: (Base, Protocol1, Protocol2)
        let bases = if self.match_token(&TokenType::LeftParen) {
            self.parse_inheritance_list()?
        } else {
            Vec::new()
        };

        // Parse colon and body
        if !self.match_token(&TokenType::Colon) {
            return Err(ParseError::UnexpectedToken {
                expected: "':'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume ':'

        // Parse class body
        let body = self.parse_block()?;

        // Consume the dedent that ends the class block
        if self.match_token(&TokenType::Dedent) {
            self.advance();
        }

        let end_location = self.current_location();

        Ok(Node::ClassDef(ClassDef {
            access_modifier,
            name,
            type_params,
            bases,
            body,
            source: Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            )),
        }))
    }

    /// Parse a struct definition: struct Name[T](Protocol): body
    fn parse_struct_definition(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Parse struct name with access modifier
        let (access_modifier, name) = self.parse_type_definition_name()?;

        // Parse optional generic parameters: [T, U, V]
        let type_params = self.parse_optional_generic_params()?;

        // Parse optional protocol implementation: (Protocol1, Protocol2)
        let bases = if self.match_token(&TokenType::LeftParen) {
            self.parse_inheritance_list()?
        } else {
            Vec::new()
        };

        // Parse colon and body
        if !self.match_token(&TokenType::Colon) {
            return Err(ParseError::UnexpectedToken {
                expected: "':'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume ':'

        // Parse struct body
        let body = self.parse_block()?;

        // Consume the dedent that ends the struct block
        if self.match_token(&TokenType::Dedent) {
            self.advance();
        }

        let end_location = self.current_location();

        Ok(Node::StructDef(StructDef {
            access_modifier,
            name,
            type_params,
            bases,
            body,
            source: Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            )),
        }))
    }

    /// Parse a protocol definition: protocol Name[T](Base): body
    fn parse_protocol_definition(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Parse protocol name with access modifier
        let (access_modifier, name) = self.parse_type_definition_name()?;

        // Parse optional generic parameters: [T, U, V]
        let type_params = self.parse_optional_generic_params()?;

        // Parse optional protocol inheritance: (Base1, Base2)
        let bases = if self.match_token(&TokenType::LeftParen) {
            self.parse_inheritance_list()?
        } else {
            Vec::new()
        };

        // Parse colon and body
        if !self.match_token(&TokenType::Colon) {
            return Err(ParseError::UnexpectedToken {
                expected: "':'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume ':'

        // Parse protocol body
        let body = self.parse_block()?;

        // Consume the dedent that ends the protocol block
        if self.match_token(&TokenType::Dedent) {
            self.advance();
        }

        let end_location = self.current_location();

        Ok(Node::ProtocolDef(ProtocolDef {
            access_modifier,
            name,
            type_params,
            bases,
            body,
            source: Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            )),
        }))
    }

    /// Parse a type definition name and extract access modifier
    fn parse_type_definition_name(&mut self) -> Result<(Option<String>, String), ParseError> {
        if let Some(token) = self.current_token() {
            if let TokenType::Name(name_type) = &token.token_type {
                let clean_name = name_type.name.clone();
                let access_modifier = match name_type.access_modifier {
                    crate::lexer::token::AccessModifier::Public => None,
                    crate::lexer::token::AccessModifier::Protected => Some("protected".to_string()),
                    crate::lexer::token::AccessModifier::Private => Some("private".to_string()),
                    crate::lexer::token::AccessModifier::Internal => Some("internal".to_string()),
                    crate::lexer::token::AccessModifier::File => Some("file".to_string()),
                };
                self.advance(); // consume name
                Ok((access_modifier, clean_name))
            } else {
                Err(ParseError::UnexpectedToken {
                    expected: "type name".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                })
            }
        } else {
            Err(ParseError::UnexpectedToken {
                expected: "type name".to_string(),
                found: "end of input".to_string(),
                location: self.current_location(),
            })
        }
    }

    /// Parse optional generic parameters: [T, U, V] or empty
    fn parse_optional_generic_params(&mut self) -> Result<Vec<String>, ParseError> {
        if self.match_token(&TokenType::LeftBracket) {
            self.advance(); // consume '['

            let mut type_params = Vec::new();

            // Parse first type parameter
            if !self.match_token(&TokenType::RightBracket) {
                type_params.push(self.parse_name_token()?);

                // Parse remaining type parameters
                while self.match_token(&TokenType::Comma) {
                    self.advance(); // consume ','
                    if self.match_token(&TokenType::RightBracket) {
                        break; // trailing comma
                    }
                    type_params.push(self.parse_name_token()?);
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

            Ok(type_params)
        } else {
            Ok(Vec::new())
        }
    }

    /// Parse inheritance/protocol implementation list: (Base, Protocol1, Protocol2)
    fn parse_inheritance_list(&mut self) -> Result<Vec<Node>, ParseError> {
        if !self.match_token(&TokenType::LeftParen) {
            return Err(ParseError::UnexpectedToken {
                expected: "'('".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume '('

        let mut bases = Vec::new();

        // Parse first base type
        if !self.match_token(&TokenType::RightParen) {
            bases.push(self.parse_type_expression()?);

            // Parse remaining base types
            while self.match_token(&TokenType::Comma) {
                self.advance(); // consume ','
                if self.match_token(&TokenType::RightParen) {
                    break; // trailing comma
                }
                bases.push(self.parse_type_expression()?);
            }
        }

        // Expect closing paren
        if !self.match_token(&TokenType::RightParen) {
            return Err(ParseError::UnexpectedToken {
                expected: "')'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume ')'

        Ok(bases)
    }

    /// Parse an import statement: import module [as alias], module2 [as alias2], ...
    fn parse_import_statement(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();
        let mut names = Vec::new();

        // Parse first import
        names.push(self.parse_import_alias()?);

        // Parse additional imports separated by commas
        while self.match_token(&TokenType::Comma) {
            self.advance(); // consume ','
            names.push(self.parse_import_alias()?);
        }

        let end_location = self.current_location();

        Ok(Node::Import(Import {
            names,
            source: Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            )),
        }))
    }

    /// Parse a from-import statement: from module import name [as alias], name2 [as alias2], ...
    fn parse_import_from_statement(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Parse module name (dotted name like 'package.module')
        let module = Some(self.parse_dotted_name()?);

        // Expect 'import' keyword
        if !self.match_token(&TokenType::Import) {
            return Err(ParseError::UnexpectedToken {
                expected: "'import'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume 'import'

        let mut names = Vec::new();

        // Handle star import: from module import *
        if self.match_token(&TokenType::Star) {
            self.advance(); // consume '*'
            names.push(Alias {
                name: "*".to_string(),
                as_name: None,
                source: Some(NodeSource::from_source_location(
                    &self.current_location(),
                    &self.current_location(),
                )),
            });
        } else {
            // Parse first import name
            names.push(self.parse_import_alias()?);

            // Parse additional names separated by commas
            while self.match_token(&TokenType::Comma) {
                self.advance(); // consume ','
                names.push(self.parse_import_alias()?);
            }
        }

        let end_location = self.current_location();

        Ok(Node::ImportFrom(ImportFrom {
            module,
            names,
            level: 0, // No relative imports for now
            source: Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            )),
        }))
    }

    /// Parse an import alias: name [as alias]
    fn parse_import_alias(&mut self) -> Result<Alias, ParseError> {
        let start_location = self.current_location();

        // Parse the module/name (could be dotted like 'package.module')
        let name = self.parse_dotted_name()?;

        // Parse optional 'as alias'
        let as_name = if self.match_token(&TokenType::As) {
            self.advance(); // consume 'as'
            Some(self.parse_name_token()?)
        } else {
            None
        };

        Ok(Alias {
            name,
            as_name,
            source: Some(NodeSource::from_source_location(
                &start_location,
                &self.current_location(),
            )),
        })
    }

    /// Parse a dotted name like 'package.module.submodule'
    fn parse_dotted_name(&mut self) -> Result<String, ParseError> {
        let mut parts = Vec::new();

        // Parse first part
        parts.push(self.parse_name_token()?);

        // Parse additional parts separated by dots
        while self.match_token(&TokenType::Dot) {
            self.advance(); // consume '.'
            parts.push(self.parse_name_token()?);
        }

        Ok(parts.join("."))
    }

    /// Check if the current position starts a property definition
    fn is_property_definition(&self) -> bool {
        // Check for property keyword or get/set property
        if self.match_token(&TokenType::Property) {
            return true;
        }

        if self.match_token(&TokenType::Get) || self.match_token(&TokenType::Set) {
            // Check if next token is "property"
            if let Some(next_token) = self.tokens.get(self.current + 1) {
                return matches!(next_token.token_type, TokenType::Property);
            }
        }

        false
    }

    /// Parse a property definition: property name: type = value or property name(self) -> type: body
    fn parse_property_definition(&mut self) -> Result<Node, ParseError> {
        let start_location = self.current_location();

        // Parse optional get/set prefix
        let mut is_get_only = false;
        let mut is_set_only = false;

        if self.match_token(&TokenType::Get) {
            is_get_only = true;
            self.advance(); // consume 'get'
        } else if self.match_token(&TokenType::Set) {
            is_set_only = true;
            self.advance(); // consume 'set'
        }

        // Expect 'property' keyword
        if !self.match_token(&TokenType::Property) {
            return Err(ParseError::UnexpectedToken {
                expected: "'property'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume 'property'

        // Parse property name with access modifier
        let (access_modifier, name) = self.parse_type_definition_name()?;

        // Check if this is an explicit property (has parentheses) or auto property
        if self.match_token(&TokenType::LeftParen) {
            // Explicit property: property name(self) -> type: body
            self.parse_explicit_property(
                start_location,
                access_modifier,
                name,
                is_get_only,
                is_set_only,
            )
        } else {
            // Auto property: property name: type = value
            self.parse_auto_property(
                start_location,
                access_modifier,
                name,
                is_get_only,
                is_set_only,
            )
        }
    }

    /// Parse an auto property: property name: type = value
    fn parse_auto_property(
        &mut self,
        start_location: SourceLocation,
        access_modifier: Option<String>,
        name: String,
        is_get_only: bool,
        is_set_only: bool,
    ) -> Result<Node, ParseError> {
        // Parse optional type annotation
        let type_ = if self.match_token(&TokenType::Colon) {
            self.advance(); // consume ':'
            Some(Box::new(self.parse_type_expression()?))
        } else {
            None
        };

        // Parse optional default value
        let default = if self.match_token(&TokenType::Equal) {
            self.advance(); // consume '='
            Some(Box::new(self.parse_expression()?))
        } else {
            None
        };

        let end_location = self.current_location();

        Ok(Node::PropertyDef(PropertyDef {
            access_modifier,
            name,
            type_,
            default,
            getter: None,
            setter: None,
            is_get_only,
            is_set_only,
            source: Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            )),
        }))
    }

    /// Parse an explicit property: property name(self) -> type: body
    fn parse_explicit_property(
        &mut self,
        start_location: SourceLocation,
        access_modifier: Option<String>,
        name: String,
        is_get_only: bool,
        is_set_only: bool,
    ) -> Result<Node, ParseError> {
        // Consume the left parenthesis
        self.advance(); // consume '('

        // Parse function arguments (self, optional setter parameter)
        let args = if self.match_token(&TokenType::RightParen) {
            // Empty parameter list (shouldn't happen for properties)
            self.advance();
            Arguments { args: vec![] }
        } else {
            let args = self.parse_function_arguments()?;
            if !self.match_token(&TokenType::RightParen) {
                return Err(ParseError::UnexpectedToken {
                    expected: "')'".to_string(),
                    found: self.current_token_string(),
                    location: self.current_location(),
                });
            }
            self.advance(); // consume ')'
            args
        };

        // Parse optional return type
        let type_ = if self.match_token(&TokenType::RArrow) {
            self.advance(); // consume '->'
            Some(Box::new(self.parse_type_expression()?))
        } else {
            None
        };

        // Expect ':'
        if !self.match_token(&TokenType::Colon) {
            return Err(ParseError::UnexpectedToken {
                expected: "':'".to_string(),
                found: self.current_token_string(),
                location: self.current_location(),
            });
        }
        self.advance(); // consume ':'

        // Parse property body (similar to function body)
        let body = self.parse_block()?;

        // Consume the dedent that ends the property block
        if self.match_token(&TokenType::Dedent) {
            self.advance();
        }

        let end_location = self.current_location();

        // Determine if this is a getter or setter based on arguments
        let is_getter = args.args.len() == 1; // Only self parameter
        let is_setter = args.args.len() == 2; // Self + value parameter

        let getter = if is_getter {
            Some(Box::new(Node::List(crate::ast::node::List {
                elements: body.clone(),
                source: None,
            })))
        } else {
            None
        };

        let setter = if is_setter {
            Some(Box::new(Node::List(crate::ast::node::List {
                elements: body,
                source: None,
            })))
        } else {
            None
        };

        Ok(Node::PropertyDef(PropertyDef {
            access_modifier,
            name,
            type_,
            default: None,
            getter,
            setter,
            is_get_only,
            is_set_only,
            source: Some(NodeSource::from_source_location(
                &start_location,
                &end_location,
            )),
        }))
    }
}
