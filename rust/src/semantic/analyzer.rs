use crate::ast::node::{
    Assign, BinaryOp, BinaryOp_, Call, ClassDef, Constant, ConstantValue, FunctionDef, Import,
    ImportFrom, Name, Node, PropertyDef, ProtocolDef, StructDef, Subscript, UnaryOp, UnaryOp_,
};
use crate::semantic::{
    AccessLevel, BuiltinType, ScopeKind, SemanticType, Symbol, SymbolKind, SymbolMetadata,
    SymbolTable,
};

/// Main semantic analyzer for Sharpy
pub struct SemanticAnalyzer {
    /// Symbol table for tracking symbols and scopes
    pub symbol_table: SymbolTable,

    /// Stack of error messages
    errors: Vec<String>,

    /// Current module name being analyzed
    current_module: Option<String>,
}

impl SemanticAnalyzer {
    /// Create a new semantic analyzer
    #[must_use]
    pub fn new() -> Self {
        Self {
            symbol_table: SymbolTable::new(),
            current_module: None,
            errors: Vec::new(),
        }
    }

    /// Analyze a module (list of top-level statements)
    /// # Errors
    /// Returns errors for invalid symbols or duplicate definitions
    pub fn analyze_module(
        &mut self,
        statements: &[Node],
        module_name: Option<String>,
    ) -> Result<(), Vec<String>> {
        self.current_module.clone_from(&module_name);

        // Enter module scope
        let _module_scope_id = self
            .symbol_table
            .enter_scope(ScopeKind::Module, module_name);

        // Analyze all statements
        for statement in statements {
            if let Err(error) = self.analyze_statement(statement) {
                self.errors.push(error);
            }
        }

        // Exit module scope
        self.symbol_table.exit_scope();

        if self.errors.is_empty() {
            Ok(())
        } else {
            Err(self.errors.clone())
        }
    }

    /// Analyze a single statement
    fn analyze_statement(&mut self, node: &Node) -> Result<(), String> {
        match node {
            Node::FunctionDef(func) => self.analyze_function_def(func),
            Node::ClassDef(class) => self.analyze_class_def(class),
            Node::StructDef(struct_def) => self.analyze_struct_def(struct_def),
            Node::ProtocolDef(protocol) => self.analyze_protocol_def(protocol),
            Node::PropertyDef(prop) => self.analyze_property_def(prop),
            Node::Import(import) => self.analyze_import(import),
            Node::ImportFrom(import_from) => self.analyze_import_from(import_from),
            Node::Assign(assign) => self.analyze_assignment(assign),

            // For now, other statements are analyzed as expressions
            _ => {
                // For expressions, analyze and ignore the type for now
                self.analyze_expression(node)?;
                Ok(())
            }
        }
    }

    /// Analyze a function definition
    fn analyze_function_def(&mut self, func: &FunctionDef) -> Result<(), String> {
        // Extract access level from function name
        let access_level = AccessLevel::from_modifier(func.access_modifier.as_deref());

        // Determine if this is a method or function based on current scope
        let symbol_kind = self
            .symbol_table
            .current_scope()
            .map_or(SymbolKind::Function, |scope| match scope.kind {
                ScopeKind::Class | ScopeKind::Struct | ScopeKind::Protocol => SymbolKind::Method,
                _ => SymbolKind::Function,
            });

        // Parse return type
        let return_type = func
            .return_type
            .as_ref()
            .and_then(|type_node| self.resolve_type_annotation(type_node))
            .or_else(|| {
                // If no return type specified, default to None for functions
                Some(SemanticType::builtin(BuiltinType::None))
            });

        // Create function symbol
        let current_scope_id = self
            .symbol_table
            .current_scope_id
            .as_ref()
            .ok_or("No current scope")?;

        let function_type = SemanticType::function(
            Vec::new(), // Parameter types will be filled in below
            return_type.clone(),
        );

        let mut symbol = Symbol::new(
            func.name.clone(),
            symbol_kind,
            function_type,
            access_level,
            current_scope_id.clone(),
            func.source.as_ref().map(|s| {
                crate::utils::SourceLocation::new(s.line_start, s.col_start, s.line_end, s.col_end)
            }),
        );

        // Check if function is abstract (has ellipsis body)
        let is_abstract = Self::is_function_abstract(func);

        // Set metadata based on symbol kind
        let metadata = match symbol.kind {
            SymbolKind::Function => SymbolMetadata::Function {
                parameters: Vec::new(),
                return_type: return_type.clone(),
                is_abstract,
            },
            SymbolKind::Method => SymbolMetadata::Method {
                parameters: Vec::new(),
                return_type: return_type.clone(),
                is_abstract,
                is_override: false, // TODO: Check for @override attribute
                is_virtual: false,  // TODO: Check for virtual methods
            },
            _ => SymbolMetadata::None,
        };

        symbol = symbol.with_metadata(metadata);

        // Add symbol to current scope
        let symbol_id = self.symbol_table.add_symbol(symbol)?;

        // Enter function scope to analyze parameters and body
        let _func_scope_id = self
            .symbol_table
            .enter_scope(ScopeKind::Function, Some(func.name.clone()));

        // Analyze parameters
        let mut param_types = Vec::new();
        let mut param_symbol_ids = Vec::new();

        for arg in &func.args.args {
            let param_symbol_id = self.analyze_parameter(arg)?;

            // Get parameter type for function signature
            if let Some(param_symbol) = self.symbol_table.get_symbol(&param_symbol_id) {
                param_types.push(param_symbol.symbol_type.clone());
            }

            param_symbol_ids.push(param_symbol_id);
        }

        // Update function symbol with parameter information
        if let Some(symbol) = self.symbol_table.symbols.get_mut(&symbol_id) {
            // Update function type with parameter types
            symbol.symbol_type = SemanticType::function(param_types, return_type);

            // Update metadata with parameter symbol IDs
            match &mut symbol.metadata {
                SymbolMetadata::Function { parameters, .. }
                | SymbolMetadata::Method { parameters, .. } => {
                    *parameters = param_symbol_ids;
                }
                _ => {}
            }
        }

        // Analyze function body (if not abstract)
        if !is_abstract {
            for statement in &func.body {
                self.analyze_statement(statement)?;
            }
        }

        // Exit function scope
        self.symbol_table.exit_scope();

        Ok(())
    }

    /// Check if a function is abstract (has ellipsis as body)
    fn is_function_abstract(func: &FunctionDef) -> bool {
        func.body.len() == 1
            && matches!(func.body[0], Node::Constant(ref c)
            if matches!(c.value, crate::ast::node::ConstantValue::Ellipsis))
    }

    /// Analyze a parameter
    fn analyze_parameter(&mut self, arg: &crate::ast::node::Arg) -> Result<String, String> {
        let param_type = arg
            .type_
            .as_ref()
            .and_then(|type_node| self.resolve_type_annotation(type_node))
            .unwrap_or_else(|| SemanticType::Unknown(arg.name.clone()));

        let current_scope_id = self
            .symbol_table
            .current_scope_id
            .as_ref()
            .ok_or("No current scope")?;

        let symbol = Symbol::new(
            arg.name.clone(),
            SymbolKind::Parameter,
            param_type,
            AccessLevel::Public, // Parameters are always accessible within function
            current_scope_id.clone(),
            None, // TODO: Add location information
        );

        self.symbol_table.add_symbol(symbol)
    }

    /// Analyze a class definition
    fn analyze_class_def(&mut self, class: &ClassDef) -> Result<(), String> {
        let access_level = AccessLevel::from_modifier(class.access_modifier.as_deref());

        let current_scope_id = self
            .symbol_table
            .current_scope_id
            .as_ref()
            .ok_or("No current scope")?;

        // Create class type
        let class_type = SemanticType::class(
            class.name.clone(),
            self.current_module.clone(),
            Vec::new(), // TODO: Handle generic parameters
        );

        let mut symbol = Symbol::new(
            class.name.clone(),
            SymbolKind::Class,
            class_type,
            access_level,
            current_scope_id.clone(),
            class.source.as_ref().map(|s| {
                crate::utils::SourceLocation::new(s.line_start, s.col_start, s.line_end, s.col_end)
            }),
        );

        // Set class metadata
        let metadata = SymbolMetadata::Class {
            base_class: None,      // TODO: Resolve base class
            protocols: Vec::new(), // TODO: Resolve protocol implementations
            members: Vec::new(),
            methods: Vec::new(),
            properties: Vec::new(),
        };

        symbol = symbol.with_metadata(metadata);

        // Add class symbol
        let _class_symbol_id = self.symbol_table.add_symbol(symbol)?;

        // Enter class scope
        let _class_scope_id = self
            .symbol_table
            .enter_scope(ScopeKind::Class, Some(class.name.clone()));

        // Analyze class body
        for statement in &class.body {
            self.analyze_statement(statement)?;
        }

        // Exit class scope
        self.symbol_table.exit_scope();

        Ok(())
    }

    /// Analyze a struct definition
    fn analyze_struct_def(&mut self, struct_def: &StructDef) -> Result<(), String> {
        let access_level = AccessLevel::from_modifier(struct_def.access_modifier.as_deref());

        let current_scope_id = self
            .symbol_table
            .current_scope_id
            .as_ref()
            .ok_or("No current scope")?;

        let struct_type = SemanticType::struct_type(
            struct_def.name.clone(),
            self.current_module.clone(),
            Vec::new(),
        );

        let mut symbol = Symbol::new(
            struct_def.name.clone(),
            SymbolKind::Struct,
            struct_type,
            access_level,
            current_scope_id.clone(),
            struct_def.source.as_ref().map(|s| {
                crate::utils::SourceLocation::new(s.line_start, s.col_start, s.line_end, s.col_end)
            }),
        );

        let metadata = SymbolMetadata::Struct {
            protocols: Vec::new(),
            members: Vec::new(),
            methods: Vec::new(),
            properties: Vec::new(),
        };

        symbol = symbol.with_metadata(metadata);
        let _struct_symbol_id = self.symbol_table.add_symbol(symbol)?;

        // Enter struct scope
        let _struct_scope_id = self
            .symbol_table
            .enter_scope(ScopeKind::Struct, Some(struct_def.name.clone()));

        // Analyze struct body
        for statement in &struct_def.body {
            self.analyze_statement(statement)?;
        }

        // Exit struct scope
        self.symbol_table.exit_scope();

        Ok(())
    }

    /// Analyze a protocol definition
    fn analyze_protocol_def(&mut self, protocol: &ProtocolDef) -> Result<(), String> {
        let access_level = AccessLevel::from_modifier(protocol.access_modifier.as_deref());

        let current_scope_id = self
            .symbol_table
            .current_scope_id
            .as_ref()
            .ok_or("No current scope")?;

        let protocol_type = SemanticType::protocol(
            protocol.name.clone(),
            self.current_module.clone(),
            Vec::new(),
        );

        let mut symbol = Symbol::new(
            protocol.name.clone(),
            SymbolKind::Protocol,
            protocol_type,
            access_level,
            current_scope_id.clone(),
            protocol.source.as_ref().map(|s| {
                crate::utils::SourceLocation::new(s.line_start, s.col_start, s.line_end, s.col_end)
            }),
        );

        let metadata = SymbolMetadata::Protocol {
            base_protocols: Vec::new(),
            methods: Vec::new(),
            properties: Vec::new(),
        };

        symbol = symbol.with_metadata(metadata);
        let _protocol_symbol_id = self.symbol_table.add_symbol(symbol)?;

        // Enter protocol scope
        let _protocol_scope_id = self
            .symbol_table
            .enter_scope(ScopeKind::Protocol, Some(protocol.name.clone()));

        // Analyze protocol body
        for statement in &protocol.body {
            self.analyze_statement(statement)?;
        }

        // Exit protocol scope
        self.symbol_table.exit_scope();

        Ok(())
    }

    /// Analyze a property definition
    fn analyze_property_def(&mut self, prop: &PropertyDef) -> Result<(), String> {
        let access_level = AccessLevel::from_modifier(prop.access_modifier.as_deref());

        let current_scope_id = self
            .symbol_table
            .current_scope_id
            .as_ref()
            .ok_or("No current scope")?
            .clone();

        // Resolve property type
        let prop_type = prop.type_.as_ref().map_or_else(
            || SemanticType::Unknown(prop.name.clone()),
            |type_node| {
                self.resolve_type_annotation(type_node)
                    .unwrap_or_else(|| SemanticType::Unknown(prop.name.clone()))
            },
        );

        let mut symbol = Symbol::new(
            prop.name.clone(),
            SymbolKind::Property,
            prop_type,
            access_level,
            current_scope_id,
            None, // TODO: Add location
        );

        let metadata = SymbolMetadata::Property {
            has_getter: prop.getter.is_some()
                || prop.is_get_only
                || (!prop.is_set_only && prop.setter.is_none()),
            has_setter: prop.setter.is_some()
                || prop.is_set_only
                || (!prop.is_get_only && prop.getter.is_none()),
            is_auto: prop.getter.is_none() && prop.setter.is_none(),
            backing_field: None, // TODO: Create backing field for auto properties
        };

        symbol = symbol.with_metadata(metadata);
        let _prop_symbol_id = self.symbol_table.add_symbol(symbol)?;

        Ok(())
    }

    /// Analyze an import statement
    fn analyze_import(&mut self, import: &Import) -> Result<(), String> {
        // For now, just create import symbols for each imported name
        for alias in &import.names {
            let import_name = alias.as_name.as_ref().unwrap_or(&alias.name).clone();

            let current_scope_id = self
                .symbol_table
                .current_scope_id
                .as_ref()
                .ok_or("No current scope")?;

            let symbol = Symbol::new(
                import_name,
                SymbolKind::Import,
                SemanticType::Unknown(alias.name.clone()), // TODO: Resolve imported module type
                AccessLevel::Public,
                current_scope_id.clone(),
                None,
            );

            self.symbol_table.add_symbol(symbol)?;
        }

        Ok(())
    }

    /// Analyze a from-import statement
    fn analyze_import_from(&mut self, import_from: &ImportFrom) -> Result<(), String> {
        // Similar to import, create symbols for imported names
        for alias in &import_from.names {
            let import_name = alias.as_name.as_ref().unwrap_or(&alias.name).clone();

            let current_scope_id = self
                .symbol_table
                .current_scope_id
                .as_ref()
                .ok_or("No current scope")?;

            let symbol = Symbol::new(
                import_name,
                SymbolKind::Import,
                SemanticType::Unknown(alias.name.clone()),
                AccessLevel::Public,
                current_scope_id.clone(),
                None,
            );

            self.symbol_table.add_symbol(symbol)?;
        }

        Ok(())
    }

    /// Analyze an assignment
    fn analyze_assignment(&mut self, assign: &Assign) -> Result<(), String> {
        // First, analyze the value expression to get its type
        let value_type = self.analyze_expression(&assign.value)?;

        // Then handle the target (left side of assignment)
        // For simple names, add them to the symbol table
        if let Node::Name(name) = assign.target.as_ref() {
            // Get the current scope ID or use a default
            let scope_id = self
                .symbol_table
                .current_scope_id
                .as_deref()
                .unwrap_or("global");

            // Add or update the variable in the symbol table
            let symbol = Symbol::new(
                name.id.clone(),
                SymbolKind::Variable,
                value_type,
                AccessLevel::Private, // Default access level
                scope_id.to_string(),
                None, // No location info for now
            );
            if let Err(err) = self.symbol_table.add_symbol(symbol) {
                return Err(format!("Failed to add symbol: {err}"));
            }
        } else {
            // For more complex targets (like tuples, attributes), just validate the value
            // This is a simplified implementation
        }

        Ok(())
    }

    /// Analyze an expression and return its semantic type
    fn analyze_expression(&mut self, expr: &Node) -> Result<SemanticType, String> {
        match expr {
            Node::Constant(constant) => self.analyze_constant(constant),
            Node::Name(name) => self.analyze_name_reference(name),
            Node::BinaryOp(binop) => self.analyze_binary_operation(binop),
            Node::UnaryOp(unop) => self.analyze_unary_operation(unop),
            Node::Call(call) => self.analyze_function_call(call),
            Node::Subscript(subscript) => self.analyze_subscript(subscript),
            _ => {
                // For now, return unknown type for unhandled expressions
                Ok(SemanticType::Unknown("unhandled_expression".to_string()))
            }
        }
    }

    /// Analyze an expression for type validation without returning the type
    /// Resolve a type annotation to a semantic type
    fn resolve_type_annotation(&self, type_node: &Node) -> Option<SemanticType> {
        match type_node {
            Node::Name(name) => {
                // Look up type by name
                self.symbol_table.lookup_type(&name.id)
            }
            Node::TypeName(type_name) => {
                // Handle TypeName nodes
                self.symbol_table.lookup_type(&type_name.name)
            }
            Node::Subscript(subscript) => {
                // Handle generic types like List[int]
                if let Node::Name(base_name) = &*subscript.value {
                    self.symbol_table.lookup_type(&base_name.id)
                    // TODO: Parse subscript arguments and create generic type
                } else {
                    None
                }
            }
            _ => None,
        }
    }

    /// Analyze a constant expression
    fn analyze_constant(&self, constant: &Constant) -> Result<SemanticType, String> {
        match &constant.value {
            ConstantValue::Int(_) => Ok(SemanticType::Builtin(BuiltinType::Int)),
            ConstantValue::Float(_) => Ok(SemanticType::Builtin(BuiltinType::Float)),
            ConstantValue::Str(_) => Ok(SemanticType::Builtin(BuiltinType::Str)),
            ConstantValue::Bool(_) => Ok(SemanticType::Builtin(BuiltinType::Bool)),
            ConstantValue::None => Ok(SemanticType::Builtin(BuiltinType::None)),
            _ => Ok(SemanticType::Unknown("unsupported_constant".to_string())),
        }
    }

    /// Analyze a name reference
    fn analyze_name_reference(&self, name: &Name) -> Result<SemanticType, String> {
        if let Some(symbol) = self.symbol_table.lookup_symbol(&name.id) {
            Ok(symbol.symbol_type.clone())
        } else {
            Err(format!("Undefined variable: {}", name.id))
        }
    }

    /// Analyze a binary operation
    fn analyze_binary_operation(&mut self, binop: &BinaryOp_) -> Result<SemanticType, String> {
        let left_type = self.analyze_expression(&binop.left)?;
        let right_type = self.analyze_expression(&binop.right)?;

        // Type checking for binary operations
        match binop.op {
            BinaryOp::Add | BinaryOp::Sub | BinaryOp::Mult | BinaryOp::Div => {
                // Arithmetic operations - check for numeric compatibility
                match (&left_type, &right_type) {
                    (
                        SemanticType::Builtin(BuiltinType::Int),
                        SemanticType::Builtin(BuiltinType::Int),
                    ) => Ok(SemanticType::Builtin(BuiltinType::Int)),
                    (
                        SemanticType::Builtin(BuiltinType::Float),
                        SemanticType::Builtin(BuiltinType::Float),
                    ) => Ok(SemanticType::Builtin(BuiltinType::Float)),
                    (
                        SemanticType::Builtin(BuiltinType::Int),
                        SemanticType::Builtin(BuiltinType::Float),
                    )
                    | (
                        SemanticType::Builtin(BuiltinType::Float),
                        SemanticType::Builtin(BuiltinType::Int),
                    ) => Ok(SemanticType::Builtin(BuiltinType::Float)),
                    (
                        SemanticType::Builtin(BuiltinType::Str),
                        SemanticType::Builtin(BuiltinType::Str),
                    ) if matches!(binop.op, BinaryOp::Add) => {
                        Ok(SemanticType::Builtin(BuiltinType::Str))
                    }
                    _ => Err(format!(
                        "Invalid types for {:?}: {:?} and {:?}",
                        binop.op, left_type, right_type
                    )),
                }
            }
            _ => Err(format!("Unsupported binary operation: {:?}", binop.op)),
        }
    }

    /// Analyze a unary operation
    fn analyze_unary_operation(&mut self, unop: &UnaryOp_) -> Result<SemanticType, String> {
        let operand_type = self.analyze_expression(&unop.operand)?;

        match unop.op {
            UnaryOp::Not => match operand_type {
                SemanticType::Builtin(BuiltinType::Bool) => {
                    Ok(SemanticType::Builtin(BuiltinType::Bool))
                }
                _ => Err(format!(
                    "Not operation requires boolean operand, got {operand_type:?}"
                )),
            },
            UnaryOp::UnaryAdd | UnaryOp::UnarySub => match operand_type {
                SemanticType::Builtin(BuiltinType::Int | BuiltinType::Float) => Ok(operand_type),
                _ => Err(format!(
                    "Unary +/- requires numeric operand, got {operand_type:?}"
                )),
            },
            _ => Err(format!("Unsupported unary operation: {:?}", unop.op)),
        }
    }

    /// Analyze a function call
    fn analyze_function_call(&mut self, call: &Call) -> Result<SemanticType, String> {
        // Analyze all arguments and collect their types
        let mut arg_types = Vec::new();
        for arg in &call.positional_args {
            let arg_type = self.analyze_expression(arg)?;
            arg_types.push(arg_type);
        }

        // For built-in functions, we can handle some basic ones
        if let Node::Name(name) = &*call.function {
            match name.id.as_str() {
                "print" => {
                    // print() can take any number of arguments of any type
                    Ok(SemanticType::Builtin(BuiltinType::None))
                }
                "len" => {
                    // len() requires exactly one argument of a collection type
                    if arg_types.len() != 1 {
                        return Err(format!("len() takes exactly 1 argument, got {}", arg_types.len()));
                    }
                    // For now, accept any type (could be enhanced to check if it's a collection)
                    Ok(SemanticType::Builtin(BuiltinType::Int))
                }
                "str" | "int" | "float" | "bool" => {
                    // Type conversion functions take exactly one argument
                    if arg_types.len() != 1 {
                        let func_name = name.id.as_str();
                        return Err(format!("{}() takes exactly 1 argument, got {}", func_name, arg_types.len()));
                    }
                    // Return the corresponding type
                    match name.id.as_str() {
                        "str" => Ok(SemanticType::Builtin(BuiltinType::Str)),
                        "int" => Ok(SemanticType::Builtin(BuiltinType::Int)),
                        "float" => Ok(SemanticType::Builtin(BuiltinType::Float)),
                        "bool" => Ok(SemanticType::Builtin(BuiltinType::Bool)),
                        _ => unreachable!(),
                    }
                }
                _ => {
                    // Look up user-defined function
                    if let Some(symbol) = self.symbol_table.lookup_symbol(&name.id) {
                        match &symbol.symbol_type {
                            SemanticType::Function { params, return_type } => {
                                // Validate argument count
                                if arg_types.len() != params.len() {
                                    return Err(format!(
                                        "Function '{}' expects {} arguments, got {}",
                                        name.id,
                                        params.len(),
                                        arg_types.len()
                                    ));
                                }

                                // Validate argument types
                                for (i, (expected, actual)) in params.iter().zip(arg_types.iter()).enumerate() {
                                    if !self.types_compatible(actual, expected) {
                                        return Err(format!(
                                            "Function '{}' argument {} expects type {:?}, got {:?}",
                                            name.id,
                                            i + 1,
                                            expected,
                                            actual
                                        ));
                                    }
                                }

                                // Return function's return type
                                if let Some(return_type) = return_type {
                                    Ok((**return_type).clone())
                                } else {
                                    Ok(SemanticType::Builtin(BuiltinType::None))
                                }
                            }
                            _ => Err(format!("'{}' is not a function", name.id)),
                        }
                    } else {
                        Err(format!("Undefined function: {}", name.id))
                    }
                }
            }
        } else {
            // Complex function expressions not yet supported
            Ok(SemanticType::Unknown("complex_function".to_string()))
        }
    }

    /// Analyze a subscript operation
    fn analyze_subscript(&mut self, subscript: &Subscript) -> Result<SemanticType, String> {
        let value_type = self.analyze_expression(&subscript.value)?;
        let _slice_type = self.analyze_expression(&subscript.slice)?;

        // Basic subscript type checking
        match value_type {
            SemanticType::Array(element_type) => Ok((*element_type).clone()),
            SemanticType::Builtin(BuiltinType::Str) => Ok(SemanticType::Builtin(BuiltinType::Str)), // Character access
            _ => Err(format!("Cannot subscript type: {value_type:?}")),
        }
    }

    /// Check if two types are compatible for operations
    const fn types_compatible(&self, left: &SemanticType, right: &SemanticType) -> bool {
        match (left, right) {
            // Exact matches
            (SemanticType::Builtin(BuiltinType::Int), SemanticType::Builtin(BuiltinType::Int))
            | (
                SemanticType::Builtin(BuiltinType::Float),
                SemanticType::Builtin(BuiltinType::Float),
            )
            | (SemanticType::Builtin(BuiltinType::Str), SemanticType::Builtin(BuiltinType::Str))
            | (
                SemanticType::Builtin(BuiltinType::Bool),
                SemanticType::Builtin(BuiltinType::Bool),
            )
            | (
                SemanticType::Builtin(BuiltinType::None),
                SemanticType::Builtin(BuiltinType::None),
            ) => true,

            // Numeric type compatibility
            (
                SemanticType::Builtin(BuiltinType::Int),
                SemanticType::Builtin(BuiltinType::Float),
            )
            | (
                SemanticType::Builtin(BuiltinType::Float),
                SemanticType::Builtin(BuiltinType::Int),
            ) => true,

            // Unknown types are compatible with anything (for gradual typing)
            (SemanticType::Unknown(_), _) | (_, SemanticType::Unknown(_)) => true,

            _ => false,
        }
    }

    /// Get analysis errors
    #[must_use]
    pub fn get_errors(&self) -> &[String] {
        &self.errors
    }

    /// Get the symbol table
    #[must_use]
    pub const fn get_symbol_table(&self) -> &SymbolTable {
        &self.symbol_table
    }
}

impl Default for SemanticAnalyzer {
    fn default() -> Self {
        Self::new()
    }
}
