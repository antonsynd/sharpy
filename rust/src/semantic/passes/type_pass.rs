use super::{AnalysisPass, PassResult};
/// Type analysis pass
/// Handles type checking, type inference, and semantic analysis of expressions
use crate::ast::node::{BinaryOp, Node, UnaryOp};
use crate::ast::types::ResolvedType;
use crate::semantic::module_registry::ModuleRegistry;
use crate::semantic::{BuiltinType, SemanticError, SemanticType};

/// Third pass: type checking and semantic analysis
pub struct TypePass {
    /// Current scope depth for tracking variable lifetimes
    scope_depth: usize,
}

impl AnalysisPass for TypePass {
    fn name(&self) -> &'static str {
        "Type Pass"
    }

    fn run(&mut self, ast: &Node, registry: &mut ModuleRegistry) -> PassResult {
        let mut errors = Vec::new();
        self.scope_depth = 0;

        if let Err(err) = self.analyze_types(ast, registry) {
            errors.push(err);
        }

        PassResult {
            errors,
            should_continue: false, // This is the final pass
        }
    }

    fn can_continue_with_errors(&self) -> bool {
        false // Type errors should stop compilation
    }
}

impl TypePass {
    /// Create a new type analysis pass
    #[must_use]
    pub const fn new() -> Self {
        Self { scope_depth: 0 }
    }

    /// Analyze types in the AST
    fn analyze_types(
        &mut self,
        node: &Node,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        match node {
            Node::Module(module) => {
                for stmt in &module.body {
                    self.analyze_types(stmt, registry)?;
                }
            }

            Node::Assign(assign) => {
                self.analyze_assignment(&assign.target, &assign.value, registry)?;
            }

            Node::If(if_stmt) => {
                self.analyze_if_statement(
                    &if_stmt.test,
                    &if_stmt.body,
                    if if_stmt.else_.is_empty() {
                        None
                    } else {
                        Some(&if_stmt.else_)
                    },
                    registry,
                )?;
            }

            Node::While(while_stmt) => {
                self.analyze_while_loop(&while_stmt.test, &while_stmt.body, registry)?;
            }

            Node::For(for_stmt) => {
                self.analyze_for_loop(&for_stmt.target, &for_stmt.iter, &for_stmt.body, registry)?;
            }

            Node::Call(call) => {
                self.analyze_function_call(call, registry)?;
            }

            Node::Return(return_stmt) => {
                if let Some(ref value) = return_stmt.value {
                    self.analyze_expression(value, registry)?;
                }
            }

            _ => {
                // For other nodes, we might need specific handling
                // For now, we'll skip or handle generically
            }
        }

        Ok(())
    }

    /// Enter a new scope
    const fn enter_scope(&mut self) {
        self.scope_depth += 1;
    }

    /// Exit current scope
    const fn exit_scope(&mut self) {
        if self.scope_depth > 0 {
            self.scope_depth -= 1;
        }
    }

    /// Analyze assignment statement
    fn analyze_assignment(
        &mut self,
        target: &Node,
        value: &Node,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        let _value_type = self.infer_expression_type(value, registry)?;

        // For simple assignments to names, we can update the symbol type
        if let Node::Name(name) = target
            && let Some(current_module) = registry.current_module_mut()
        {
            if let Some(_symbol) = current_module.symbols.lookup_symbol(&name.id) {
                // TODO: Update the symbol's type if it was unknown
                // For now, we'll just do type checking without mutation
                // This requires extending the SymbolTable API to support mutation
            } else {
                return Err(SemanticError::UndefinedSymbol(name.id.clone()));
            }
        }

        Ok(())
    }

    /// Analyze expression and return its type
    fn analyze_expression(
        &mut self,
        expr: &Node,
        registry: &mut ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        self.infer_expression_type(expr, registry)
    }

    /// Infer the type of an expression
    fn infer_expression_type(
        &mut self,
        expr: &Node,
        registry: &mut ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        match expr {
            Node::Constant(constant) => Ok(self.infer_literal_type(&constant.value)),

            Node::Name(name) => self.infer_identifier_type(&name.id, registry),

            Node::BinaryOp(binop) => {
                self.infer_binary_op_type(&binop.left, &binop.op, &binop.right, registry)
            }

            Node::UnaryOp(unop) => self.infer_unary_op_type(&unop.op, &unop.operand, registry),

            Node::Call(call) => self.infer_function_call_type(call, registry),

            Node::Attribute(attr) => {
                self.infer_attribute_access_type(&attr.value, &attr.attr, registry)
            }

            Node::Subscript(subscript) => {
                self.infer_subscript_type(&subscript.value, &subscript.slice, registry)
            }

            Node::List(list) => {
                // Infer list type from elements
                if list.elements.is_empty() {
                    Ok(SemanticType::Array(Box::new(SemanticType::Unknown(
                        "empty_list".to_string(),
                    ))))
                } else {
                    let first_type = self.infer_expression_type(&list.elements[0], registry)?;
                    Ok(SemanticType::Array(Box::new(first_type)))
                }
            }

            _ => {
                // TODO: Handle other expression types
                Ok(SemanticType::Unknown("unhandled_expression".to_string()))
            }
        }
    }

    /// Infer type from literal value
    fn infer_literal_type(&self, value: &crate::ast::node::ConstantValue) -> SemanticType {
        match value {
            crate::ast::node::ConstantValue::Bool(_) => SemanticType::Builtin(BuiltinType::Bool),
            crate::ast::node::ConstantValue::Int(_) => SemanticType::Builtin(BuiltinType::Int),
            crate::ast::node::ConstantValue::Float(_) => SemanticType::Builtin(BuiltinType::Float),
            crate::ast::node::ConstantValue::Str(_) => SemanticType::Builtin(BuiltinType::Str),
            crate::ast::node::ConstantValue::None => SemanticType::Builtin(BuiltinType::None),
            _ => SemanticType::Unknown("unsupported_literal".to_string()),
        }
    }

    /// Infer type from identifier
    fn infer_identifier_type(
        &self,
        name: &str,
        registry: &ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        if let Some(symbol) = registry.resolve_symbol(name) {
            Ok(symbol.symbol_type.clone())
        } else {
            Err(SemanticError::UndefinedSymbol(name.to_string()))
        }
    }

    /// Infer type from binary operation
    fn infer_binary_op_type(
        &mut self,
        left: &Node,
        op: &BinaryOp,
        right: &Node,
        registry: &mut ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        let left_type = self.infer_expression_type(left, registry)?;
        let right_type = self.infer_expression_type(right, registry)?;

        match op {
            BinaryOp::Add | BinaryOp::Sub | BinaryOp::Mult | BinaryOp::Div => {
                if self.types_compatible(&left_type, &right_type) {
                    Ok(left_type)
                } else {
                    Err(SemanticError::TypeMismatch {
                        expected: self.semantic_type_to_resolved_type(&left_type),
                        found: self.semantic_type_to_resolved_type(&right_type),
                        line: 0,
                        column: 0,
                    })
                }
            }

            _ => Ok(SemanticType::Builtin(BuiltinType::Bool)), // Comparison and logical ops return bool
        }
    }

    /// Infer type from unary operation
    fn infer_unary_op_type(
        &mut self,
        op: &UnaryOp,
        operand: &Node,
        registry: &mut ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        let operand_type = self.infer_expression_type(operand, registry)?;

        match op {
            UnaryOp::UnarySub | UnaryOp::UnaryAdd => Ok(operand_type),
            UnaryOp::Not => Ok(SemanticType::Builtin(BuiltinType::Bool)),
            _ => Ok(SemanticType::Unknown("unhandled_unary_op".to_string())),
        }
    }

    /// Infer type from function call
    fn infer_function_call_type(
        &mut self,
        call: &crate::ast::node::Call,
        registry: &ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        // Extract function name from the function node
        let func_name = match call.function.as_ref() {
            Node::Name(name) => &name.id,
            Node::Attribute(attr) => &attr.attr, // Method call
            _ => return Ok(SemanticType::Unknown("complex_call".to_string())),
        };

        if let Some(symbol) = registry.resolve_symbol(func_name) {
            match &symbol.symbol_type {
                SemanticType::Function { return_type, .. } => Ok(return_type
                    .as_ref()
                    .map_or(SemanticType::Builtin(BuiltinType::None), |t| {
                        t.as_ref().clone()
                    })),
                _ => Ok(SemanticType::Unknown("not_callable".to_string())),
            }
        } else {
            Ok(SemanticType::Unknown("undefined_function".to_string()))
        }
    }

    /// Infer type from attribute access
    fn infer_attribute_access_type(
        &mut self,
        object: &Node,
        attr: &str,
        registry: &mut ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        let _object_type = self.infer_expression_type(object, registry)?;

        // TODO: Look up attribute in object's type definition
        // For now, return unknown type
        let _ = attr;
        Ok(SemanticType::Unknown("attribute_access".to_string()))
    }

    /// Infer type from subscript access
    fn infer_subscript_type(
        &mut self,
        array: &Node,
        _index: &Node,
        registry: &mut ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        let array_type = self.infer_expression_type(array, registry)?;

        match array_type {
            SemanticType::Array(element_type) => Ok(*element_type),
            _ => Err(SemanticError::NotIndexable(
                self.semantic_type_to_resolved_type(&array_type),
            )),
        }
    }

    /// Check if two types are compatible (can be assigned)
    fn types_compatible(&self, expected: &SemanticType, actual: &SemanticType) -> bool {
        match (expected, actual) {
            (SemanticType::Unknown(_), _) | (_, SemanticType::Unknown(_)) => true,
            (SemanticType::Optional(inner), other) | (other, SemanticType::Optional(inner)) => {
                self.types_compatible(inner, other)
            }
            _ => expected == actual,
        }
    }

    /// Convert `SemanticType` to `ResolvedType` for error reporting
    fn semantic_type_to_resolved_type(&self, semantic_type: &SemanticType) -> ResolvedType {
        // This is a placeholder conversion - in a real implementation,
        // we'd need proper mapping between semantic types and resolved types
        ResolvedType {
            components: vec![crate::ast::types::ResolvedTypeComponent {
                name: format!("{semantic_type:?}"),
                parameters: Vec::new(),
                source: None,
            }],
            optional: false,
            source: None,
        }
    }

    // Additional analysis methods for specific statement types

    fn analyze_if_statement(
        &mut self,
        condition: &Node,
        then_body: &[Node],
        else_body: Option<&Vec<Node>>,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        let condition_type = self.infer_expression_type(condition, registry)?;

        // Condition should be boolean or convertible to boolean
        if !matches!(
            condition_type,
            SemanticType::Builtin(BuiltinType::Bool) | SemanticType::Unknown(_)
        ) {
            return Err(SemanticError::TypeMismatch {
                expected: self
                    .semantic_type_to_resolved_type(&SemanticType::Builtin(BuiltinType::Bool)),
                found: self.semantic_type_to_resolved_type(&condition_type),
                line: 0,
                column: 0,
            });
        }

        for stmt in then_body {
            self.analyze_types(stmt, registry)?;
        }

        if let Some(else_stmts) = else_body {
            for stmt in else_stmts {
                self.analyze_types(stmt, registry)?;
            }
        }

        Ok(())
    }

    fn analyze_while_loop(
        &mut self,
        condition: &Node,
        body: &[Node],
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        let condition_type = self.infer_expression_type(condition, registry)?;

        if !matches!(
            condition_type,
            SemanticType::Builtin(BuiltinType::Bool) | SemanticType::Unknown(_)
        ) {
            return Err(SemanticError::TypeMismatch {
                expected: self
                    .semantic_type_to_resolved_type(&SemanticType::Builtin(BuiltinType::Bool)),
                found: self.semantic_type_to_resolved_type(&condition_type),
                line: 0,
                column: 0,
            });
        }

        for stmt in body {
            self.analyze_types(stmt, registry)?;
        }

        Ok(())
    }

    fn analyze_for_loop(
        &mut self,
        target: &Node,
        iterable: &Node,
        body: &[Node],
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        let _iterable_type = self.infer_expression_type(iterable, registry)?;

        // TODO: Check if iterable_type is actually iterable
        // TODO: Add loop variable to scope
        let _ = target;

        for stmt in body {
            self.analyze_types(stmt, registry)?;
        }

        Ok(())
    }

    fn analyze_function_call(
        &mut self,
        call: &crate::ast::node::Call,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        // Verify function exists and type check arguments
        let _return_type = self.infer_function_call_type(call, registry)?;

        // Type check arguments
        for arg in &call.positional_args {
            self.analyze_expression(arg, registry)?;
        }

        // TODO: Check argument types against function signature

        Ok(())
    }
}

impl Default for TypePass {
    fn default() -> Self {
        Self::new()
    }
}
