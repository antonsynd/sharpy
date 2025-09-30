use super::{AnalysisPass, PassResult};
/// Type analysis pass
/// Handles type checking, type inference, and semantic analysis of expressions
use crate::ast::node::{BinaryOp, Node, UnaryOp};
use crate::ast::types::ResolvedType;
use crate::semantic::module_registry::ModuleRegistry;
use crate::semantic::types::create_builtin_methods;
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
        let value_type = self.infer_expression_type(value, registry)?;

        // For simple assignments to names, we can update the symbol type
        match target {
            Node::Name(name) => {
                if let Some(current_module) = registry.current_module_mut() {
                    if let Some(symbol) = current_module.symbols.lookup_symbol_mut(&name.id) {
                        // Update the symbol's type if it was unknown or compatible
                        match &symbol.symbol_type {
                            SemanticType::Unknown(_) => {
                                symbol.symbol_type = value_type;
                            }
                            existing_type => {
                                // Check type compatibility
                                if !value_type.is_assignable_to(existing_type) {
                                    // We don't have line/column info here, so use placeholder types
                                    // TODO: Thread position information through the analysis
                                    // TODO: Convert SemanticType to ResolvedType properly
                                    let placeholder_type = crate::ast::Type {
                                        components: vec![],
                                        optional: false,
                                        source: None,
                                    };
                                    return Err(SemanticError::TypeMismatch {
                                        expected: placeholder_type.clone(),
                                        found: placeholder_type,
                                        line: 0,
                                        column: 0,
                                    });
                                }
                            }
                        }
                    } else {
                        return Err(SemanticError::UndefinedSymbol(name.id.clone()));
                    }
                }
            }
            Node::TypedName(typed_name) => {
                if let Some(current_module) = registry.current_module_mut() {
                    if let Some(symbol) = current_module.symbols.lookup_symbol_mut(&typed_name.id) {
                        // For typed assignments, we should resolve the type annotation and check compatibility
                        // For now, we'll update with the value type if it's unknown
                        match &symbol.symbol_type {
                            SemanticType::Unknown(_) => {
                                symbol.symbol_type = value_type;
                            }
                            existing_type => {
                                // Check type compatibility
                                if !value_type.is_assignable_to(existing_type) {
                                    let placeholder_type = crate::ast::Type {
                                        components: vec![],
                                        optional: false,
                                        source: None,
                                    };
                                    return Err(SemanticError::TypeMismatch {
                                        expected: placeholder_type.clone(),
                                        found: placeholder_type,
                                        line: 0,
                                        column: 0,
                                    });
                                }
                            }
                        }
                    } else {
                        return Err(SemanticError::UndefinedSymbol(typed_name.id.clone()));
                    }
                }
            }
            _ => {
                // For other assignment targets (tuple destructuring, etc.),
                // we don't currently update symbol types
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

            Node::Call(call) => {
                // First validate the function call arguments
                self.analyze_function_call(call, registry)?;
                // Then infer the return type
                self.infer_function_call_type(call, registry)
            }

            Node::Attribute(attr) => {
                self.infer_attribute_access_type(&attr.value, &attr.attr, registry)
            }

            Node::Subscript(subscript) => {
                self.infer_subscript_type(&subscript.value, &subscript.slice, registry)
            }

            Node::List(list) => {
                // Infer list type from elements
                if list.elements.is_empty() {
                    Ok(SemanticType::Generic {
                        base: Box::new(SemanticType::Builtin(BuiltinType::List)),
                        args: vec![SemanticType::Builtin(BuiltinType::Object)],
                    })
                } else {
                    let first_type = self.infer_expression_type(&list.elements[0], registry)?;

                    // Check if we need to promote int to float for mixed numeric lists
                    let mut element_type = first_type;
                    for elem in &list.elements[1..] {
                        let elem_type = self.infer_expression_type(elem, registry)?;
                        if element_type == SemanticType::Builtin(BuiltinType::Int)
                            && elem_type == SemanticType::Builtin(BuiltinType::Float)
                        {
                            element_type = SemanticType::Builtin(BuiltinType::Float);
                        } else if element_type == SemanticType::Builtin(BuiltinType::Float)
                            && elem_type == SemanticType::Builtin(BuiltinType::Int)
                        {
                            // Already float, keep it
                        }
                    }

                    Ok(SemanticType::Generic {
                        base: Box::new(SemanticType::Builtin(BuiltinType::List)),
                        args: vec![element_type],
                    })
                }
            }

            Node::Dict(dict) => {
                // Infer dictionary type from keys and values
                if dict.keys.is_empty() {
                    Ok(SemanticType::Generic {
                        base: Box::new(SemanticType::Builtin(BuiltinType::Dict)),
                        args: vec![
                            SemanticType::Builtin(BuiltinType::Object),
                            SemanticType::Builtin(BuiltinType::Object),
                        ],
                    })
                } else {
                    // Check the first non-None key for type inference
                    let key_type = if let Some(key) = &dict.keys[0] {
                        self.infer_expression_type(key, registry)?
                    } else {
                        SemanticType::Unknown("dict_expansion_key".to_string())
                    };
                    let value_type = self.infer_expression_type(&dict.values[0], registry)?;
                    Ok(SemanticType::Generic {
                        base: Box::new(SemanticType::Builtin(BuiltinType::Dict)),
                        args: vec![key_type, value_type],
                    })
                }
            }

            Node::Set(set) => {
                // Infer set type from elements
                if set.elements.is_empty() {
                    Ok(SemanticType::Generic {
                        base: Box::new(SemanticType::Builtin(BuiltinType::Set)),
                        args: vec![SemanticType::Unknown("empty_set".to_string())],
                    })
                } else {
                    let element_type = self.infer_expression_type(&set.elements[0], registry)?;
                    Ok(SemanticType::Generic {
                        base: Box::new(SemanticType::Builtin(BuiltinType::Set)),
                        args: vec![element_type],
                    })
                }
            }

            Node::Tuple(tuple) => {
                // Infer tuple type from elements
                let mut element_types = Vec::new();
                for element in &tuple.elements {
                    element_types.push(self.infer_expression_type(element, registry)?);
                }
                Ok(SemanticType::Tuple(element_types))
            }

            Node::If(if_node) => {
                // Analyze conditional expression (ternary-like)
                let true_type = self.infer_expression_type(&if_node.body[0], registry)?;
                if if_node.else_.is_empty() {
                    // No else branch - could be None
                    Ok(SemanticType::Optional(Box::new(true_type)))
                } else {
                    let false_type = self.infer_expression_type(&if_node.else_[0], registry)?;
                    // If both branches have the same type, use that type
                    if self.types_compatible(&true_type, &false_type) {
                        Ok(true_type)
                    } else {
                        // Different types - use Unknown for now
                        Ok(SemanticType::Unknown("conditional_mixed_types".to_string()))
                    }
                }
            }

            _ => {
                // Handle remaining expression types with a more specific unknown type
                let node_type = match expr {
                    Node::Lambda(_) => "lambda",
                    Node::ListComp(_) => "list_comprehension",
                    Node::DictComp(_) => "dict_comprehension",
                    Node::SetComp(_) => "set_comprehension",
                    Node::GeneratorExp(_) => "generator_expression",
                    Node::YieldFrom(_) => "yield_from",
                    Node::Yield(_) => "yield",
                    Node::Await(_) => "await",
                    Node::Compare(_) => "comparison",
                    Node::BoolOp(_) => "bool_op",
                    Node::IfExp(_) => "if_expression",
                    _ => "unknown_expression",
                };
                Ok(SemanticType::Unknown(format!("{node_type}_expression")))
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
                let (compatible, result_type) =
                    self.arithmetic_types_compatible(&left_type, &right_type);
                if compatible {
                    Ok(result_type)
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
        registry: &mut ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        // First, infer the type of the function being called
        let func_type = match self.infer_expression_type(&call.function, registry) {
            Ok(t) => t,
            Err(SemanticError::UndefinedSymbol(name)) => {
                // Convert UndefinedSymbol to UndefinedFunction when in function call context
                if let Node::Name(func_name) = call.function.as_ref() {
                    return Err(SemanticError::UndefinedFunction(func_name.id.clone()));
                }
                return Err(SemanticError::UndefinedSymbol(name));
            }
            Err(other) => return Err(other),
        };

        match func_type {
            SemanticType::Function { return_type, .. } => {
                // Return the function's return type
                Ok(return_type
                    .as_ref()
                    .map_or(SemanticType::Builtin(BuiltinType::None), |t| {
                        t.as_ref().clone()
                    }))
            }
            SemanticType::Unknown(_) => {
                // If the function type is unknown, it might be a simple function name lookup
                // Try to extract the function name and look it up in the symbol table
                if let Node::Name(name) = call.function.as_ref() {
                    if let Some(symbol) = registry.resolve_symbol(&name.id) {
                        match &symbol.symbol_type {
                            SemanticType::Function { return_type, .. } => Ok(return_type
                                .as_ref()
                                .map_or(SemanticType::Builtin(BuiltinType::None), |t| {
                                    t.as_ref().clone()
                                })),
                            _ => Ok(SemanticType::Unknown("not_callable".to_string())),
                        }
                    } else {
                        Err(SemanticError::UndefinedFunction(name.id.clone()))
                    }
                } else {
                    Ok(func_type) // Return the unknown type
                }
            }
            _ => {
                // Not a function type - this is an error
                Ok(SemanticType::Unknown("not_callable".to_string()))
            }
        }
    }

    /// Infer type from attribute access
    fn infer_attribute_access_type(
        &mut self,
        object: &Node,
        attr: &str,
        registry: &mut ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        let object_type = self.infer_expression_type(object, registry)?;

        // Look up attribute in object's type definition
        match &object_type {
            SemanticType::Class { name, module, .. } => {
                // Look for the class definition to find its attributes
                let full_name = module
                    .as_ref()
                    .map_or(name.clone(), |m| format!("{m}.{name}"));

                if let Some(class_symbol) = registry.resolve_symbol(&full_name) {
                    // TODO: Implement proper class attribute lookup
                    // For now, assume all attributes exist and return unknown type
                    let _ = class_symbol;
                    Ok(SemanticType::Unknown(format!("{attr}_attribute")))
                } else {
                    Ok(SemanticType::Unknown(format!("{attr}_attribute")))
                }
            }
            SemanticType::Builtin(builtin_type) => {
                // Handle built-in type methods using our method definitions
                let builtin_methods = create_builtin_methods();
                let type_name = match builtin_type {
                    BuiltinType::Str => "str",
                    BuiltinType::List => "List",
                    BuiltinType::Dict => "Dict",
                    _ => {
                        return Err(SemanticError::AttributeNotFound {
                            object_type: format!("{builtin_type:?}"),
                            attribute: attr.to_string(),
                        });
                    }
                };

                if let Some(methods) = builtin_methods.get(type_name) {
                    if let Some(method_type) = methods.get(attr) {
                        Ok(method_type.clone())
                    } else {
                        Err(SemanticError::AttributeNotFound {
                            object_type: format!("{builtin_type:?}"),
                            attribute: attr.to_string(),
                        })
                    }
                } else {
                    Err(SemanticError::AttributeNotFound {
                        object_type: format!("{builtin_type:?}"),
                        attribute: attr.to_string(),
                    })
                }
            }
            SemanticType::Generic { base, args } => {
                // Handle generic types like List[str], Dict[str, int]
                if let SemanticType::Builtin(builtin_type) = base.as_ref() {
                    let builtin_methods = create_builtin_methods();
                    let type_name = match builtin_type {
                        BuiltinType::List => "List",
                        BuiltinType::Dict => "Dict",
                        _ => {
                            return Err(SemanticError::AttributeNotFound {
                                object_type: format!("{object_type:?}"),
                                attribute: attr.to_string(),
                            });
                        }
                    };

                    if let Some(methods) = builtin_methods.get(type_name) {
                        if let Some(method_type) = methods.get(attr) {
                            // For generic methods, we might need to adjust the return type
                            // based on the generic arguments
                            match (builtin_type, attr) {
                                (BuiltinType::List, "pop") => {
                                    // List[T].pop() returns T
                                    if let Some(item_type) = args.first() {
                                        Ok(SemanticType::Function {
                                            params: vec![],
                                            return_type: Some(Box::new(item_type.clone())),
                                        })
                                    } else {
                                        Ok(method_type.clone())
                                    }
                                }
                                _ => Ok(method_type.clone()),
                            }
                        } else {
                            Err(SemanticError::AttributeNotFound {
                                object_type: format!("{object_type:?}"),
                                attribute: attr.to_string(),
                            })
                        }
                    } else {
                        Err(SemanticError::AttributeNotFound {
                            object_type: format!("{object_type:?}"),
                            attribute: attr.to_string(),
                        })
                    }
                } else {
                    Ok(SemanticType::Unknown(format!("{attr}_attribute")))
                }
            }
            _ => {
                // For other types, assume the attribute exists but with unknown type
                Ok(SemanticType::Unknown(format!("{attr}_attribute")))
            }
        }
    }

    /// Infer type from subscript access
    fn infer_subscript_type(
        &mut self,
        array: &Node,
        _index: &Node,
        registry: &mut ModuleRegistry,
    ) -> Result<SemanticType, SemanticError> {
        let array_type = self.infer_expression_type(array, registry)?;

        match &array_type {
            SemanticType::Array(element_type) => Ok((**element_type).clone()),
            SemanticType::Generic { base, args } => {
                // Handle generic types like List[T], Dict[K, V], Set[T]
                match **base {
                    SemanticType::Builtin(BuiltinType::List) => {
                        // List[T] subscript returns T
                        if args.is_empty() {
                            Ok(SemanticType::Unknown("list_element".to_string()))
                        } else {
                            Ok(args[0].clone())
                        }
                    }
                    SemanticType::Builtin(BuiltinType::Dict) => {
                        // Dict[K, V] subscript returns V
                        if args.len() < 2 {
                            Ok(SemanticType::Unknown("dict_value".to_string()))
                        } else {
                            Ok(args[1].clone())
                        }
                    }
                    SemanticType::Builtin(BuiltinType::Tuple) => {
                        // Tuple subscript - would need index analysis for proper typing
                        Ok(SemanticType::Unknown("tuple_element".to_string()))
                    }
                    SemanticType::Builtin(BuiltinType::Set) => {
                        // Sets don't support item access
                        Err(SemanticError::InvalidOperation {
                            operation: "subscript".to_string(),
                            type_name: "Set objects do not support item access".to_string(),
                        })
                    }
                    _ => Err(SemanticError::NotIndexable(
                        self.semantic_type_to_resolved_type(&array_type),
                    )),
                }
            }
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
            // Allow int/float compatibility for arithmetic operations
            (
                SemanticType::Builtin(BuiltinType::Int),
                SemanticType::Builtin(BuiltinType::Float),
            )
            | (
                SemanticType::Builtin(BuiltinType::Float),
                SemanticType::Builtin(BuiltinType::Int),
            ) => true,
            _ => expected == actual,
        }
    }

    /// Check if two types are compatible specifically for arithmetic operations
    fn arithmetic_types_compatible(
        &self,
        left: &SemanticType,
        right: &SemanticType,
    ) -> (bool, SemanticType) {
        use BuiltinType::{Float, Int};

        match (left, right) {
            (SemanticType::Unknown(_), _) => (true, right.clone()),
            (_, SemanticType::Unknown(_)) => (true, left.clone()),

            // Exact match
            (l, r) if l == r => (true, l.clone()),

            // Int/Float arithmetic - float takes precedence
            (SemanticType::Builtin(Int), SemanticType::Builtin(Float))
            | (SemanticType::Builtin(Float), SemanticType::Builtin(Int)) => {
                (true, SemanticType::Builtin(Float))
            }

            // Other numeric types could be added here in the future
            _ => (false, left.clone()),
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
        let iterable_type = self.infer_expression_type(iterable, registry)?;

        // Check if iterable_type is actually iterable and infer element type
        let element_type = match &iterable_type {
            SemanticType::Array(element_type) => element_type.as_ref().clone(),
            SemanticType::Generic { base, args } => match base.as_ref() {
                SemanticType::Builtin(BuiltinType::List) if !args.is_empty() => args[0].clone(),
                SemanticType::Builtin(BuiltinType::Set) if !args.is_empty() => args[0].clone(),
                _ => SemanticType::Unknown("non_iterable".to_string()),
            },
            SemanticType::Builtin(BuiltinType::Str) => SemanticType::Builtin(BuiltinType::Str), // str iterates over str
            _ => SemanticType::Unknown("non_iterable".to_string()),
        };

        // Add loop variable to scope
        if let Node::Name(name) = target
            && let Some(current_module) = registry.current_module_mut()
        {
            // Add or update the loop variable with the inferred element type
            if let Some(symbol) = current_module.symbols.lookup_symbol_mut(&name.id) {
                symbol.symbol_type = element_type;
            } else {
                // Variable doesn't exist, create it
                let symbol = crate::semantic::symbol_table::Symbol {
                    id: format!("loop_var_{}", name.id),
                    name: name.id.clone(),
                    kind: crate::semantic::symbol_table::SymbolKind::Variable,
                    symbol_type: element_type,
                    access_level: crate::semantic::symbol_table::AccessLevel::Private,
                    scope_id: "current_scope".to_string(), // TODO: Use proper scope tracking
                    location: None,
                    is_static: false,
                    generic_params: vec![],
                    metadata: crate::semantic::symbol_table::SymbolMetadata::None,
                };
                let _ = current_module.symbols.add_symbol(symbol);
            }
        }

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

        // Check argument types against function signature
        match call.function.as_ref() {
            Node::Name(func_name) => {
                let func_symbol = registry.resolve_symbol(&func_name.id);

                if func_symbol.is_none() {
                    // Function not found - generate UndefinedFunction error
                    return Err(SemanticError::UndefinedFunction(func_name.id.clone()));
                }

                let func_symbol_type = func_symbol.unwrap().symbol_type.clone();
                match func_symbol_type {
                    SemanticType::Function { params, .. } => {
                        // Check if the number of arguments matches
                        if call.positional_args.len() != params.len() {
                            return Err(SemanticError::ArgumentCountMismatch {
                                function_name: func_name.id.clone(),
                                expected: params.len(),
                                found: call.positional_args.len(),
                            });
                        }

                        // Check each argument type
                        for (i, (arg, expected_type)) in
                            call.positional_args.iter().zip(params.iter()).enumerate()
                        {
                            let actual_type = self.infer_expression_type(arg, registry)?;
                            if !self.types_compatible(expected_type, &actual_type) {
                                return Err(SemanticError::ArgumentTypeMismatch {
                                    function_name: func_name.id.clone(),
                                    argument_index: i,
                                    expected: expected_type.display_name(),
                                    found: actual_type.display_name(),
                                });
                            }
                        }
                    }
                    _ => {
                        // It's a variable being called as function
                        return Err(SemanticError::VariableCalledAsFunction {
                            variable_name: func_name.id.clone(),
                            variable_type: func_symbol_type.display_name(),
                        });
                    }
                }
            }
            Node::Attribute(attr_node) => {
                // Handle method calls like object.method()
                let method_type =
                    self.infer_attribute_access_type(&attr_node.value, &attr_node.attr, registry)?;

                if let SemanticType::Function { params, .. } = method_type {
                    // Check if the number of arguments matches
                    if call.positional_args.len() != params.len() {
                        return Err(SemanticError::ArgumentCountMismatch {
                            function_name: format!("<object>.{}", attr_node.attr),
                            expected: params.len(),
                            found: call.positional_args.len(),
                        });
                    }

                    // Check each argument type
                    for (i, (arg, expected_type)) in
                        call.positional_args.iter().zip(params.iter()).enumerate()
                    {
                        let actual_type = self.infer_expression_type(arg, registry)?;
                        if !self.types_compatible(expected_type, &actual_type) {
                            return Err(SemanticError::ArgumentTypeMismatch {
                                function_name: format!("<object>.{}", attr_node.attr),
                                argument_index: i,
                                expected: expected_type.display_name(),
                                found: actual_type.display_name(),
                            });
                        }
                    }
                } else {
                    return Err(SemanticError::VariableCalledAsFunction {
                        variable_name: format!("<object>.{}", attr_node.attr),
                        variable_type: method_type.display_name(),
                    });
                }
            }
            _ => {
                // Other types of function calls - for now, just continue without specific validation
            }
        }

        Ok(())
    }
}

impl Default for TypePass {
    fn default() -> Self {
        Self::new()
    }
}
