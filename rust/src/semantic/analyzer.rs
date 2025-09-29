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

        // Set initial class metadata (will be populated after analyzing body)
        let metadata = SymbolMetadata::Class {
            base_class: None,      // TODO: Resolve base class
            protocols: Vec::new(), // TODO: Resolve protocol implementations
            members: Vec::new(),
            methods: Vec::new(),
            properties: Vec::new(),
        };

        symbol = symbol.with_metadata(metadata);

        // Add class symbol
        let class_symbol_id = self.symbol_table.add_symbol(symbol)?;

        // Enter class scope
        let _class_scope_id = self
            .symbol_table
            .enter_scope(ScopeKind::Class, Some(class.name.clone()));

        // Track members found during analysis by checking what gets added
        let mut methods = Vec::new();
        let mut properties = Vec::new();
        let mut members = Vec::new();

        // Analyze class body and collect member information
        for statement in &class.body {
            match statement {
                Node::FunctionDef(func_def) => {
                    // Analyze the method
                    self.analyze_statement(statement)?;

                    // Find the method symbol by name in current (class) scope
                    if let Some(method_symbol_id) = self
                        .symbol_table
                        .current_scope()
                        .and_then(|scope| scope.get_symbol(&func_def.name))
                    {
                        methods.push(method_symbol_id.clone());
                    }
                }
                Node::PropertyDef(prop_def) => {
                    // Analyze the property
                    self.analyze_statement(statement)?;

                    // Find the property symbol by name in current (class) scope
                    if let Some(property_symbol_id) = self
                        .symbol_table
                        .current_scope()
                        .and_then(|scope| scope.get_symbol(&prop_def.name))
                    {
                        properties.push(property_symbol_id.clone());
                    }
                }
                Node::Assign(assign) => {
                    // Class-level variable assignment
                    self.analyze_statement(statement)?;

                    // For simple assignments, try to get the target name
                    if let Node::Name(name_node) = &*assign.target
                        && let Some(member_symbol_id) = self
                            .symbol_table
                            .current_scope()
                            .and_then(|scope| scope.get_symbol(&name_node.id))
                    {
                        members.push(member_symbol_id.clone());
                    }
                }
                _ => {
                    // Analyze other statements
                    self.analyze_statement(statement)?;
                }
            }
        }

        // Update class symbol with collected member information
        if let Some(class_symbol) = self.symbol_table.symbols.get_mut(&class_symbol_id)
            && let SymbolMetadata::Class {
                methods: class_methods,
                properties: class_properties,
                members: class_members,
                ..
            } = &mut class_symbol.metadata
        {
            *class_methods = methods;
            *class_properties = properties;
            *class_members = members;
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
        match assign.target.as_ref() {
            Node::Name(name) => {
                // Simple assignment: x = value
                self.add_variable_to_scope(&name.id, value_type)?;
            }
            Node::TypedName(typed_name) => {
                // Typed assignment: x: int = value
                if let Some(declared_type) = self.resolve_type_annotation(&typed_name.type_) {
                    // Validate that the value type is compatible with the declared type
                    if !self.is_type_assignable(&value_type, &declared_type) {
                        return Err(format!(
                            "Cannot assign {} to variable '{}' of type {}",
                            value_type.display_name(),
                            typed_name.id,
                            declared_type.display_name()
                        ));
                    }
                    // Use the declared type as the variable's type (supports type narrowing)
                    self.add_variable_to_scope(&typed_name.id, declared_type)?;
                } else {
                    return Err(format!(
                        "Cannot resolve type annotation for variable '{}'",
                        typed_name.id
                    ));
                }
            }
            _ => {
                // For more complex targets (like tuples, attributes), just validate the value
                // This is a simplified implementation
            }
        }

        Ok(())
    }

    /// Helper method to add a variable to the current scope
    fn add_variable_to_scope(&mut self, name: &str, var_type: SemanticType) -> Result<(), String> {
        let scope_id = self
            .symbol_table
            .current_scope_id
            .as_deref()
            .unwrap_or("global");

        let symbol = Symbol::new(
            name.to_string(),
            SymbolKind::Variable,
            var_type,
            AccessLevel::Private, // Default access level
            scope_id.to_string(),
            None, // No location info for now
        );

        self.symbol_table
            .add_symbol(symbol)
            .map_err(|err| format!("Failed to add symbol: {err}"))
            .map(|_| ())
    }

    /// Check if a value type can be assigned to a variable of a declared type
    fn is_type_assignable(&self, value_type: &SemanticType, declared_type: &SemanticType) -> bool {
        // Use existing type compatibility, but also support declared type as "wider" acceptance
        value_type.is_assignable_to(declared_type)
            || self.types_compatible(value_type, declared_type)
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
            Node::List(list) => self.analyze_list_literal(list),
            Node::Dict(dict) => self.analyze_dict_literal(dict),
            Node::Set(set) => self.analyze_set_literal(set),
            Node::Attribute(attribute) => self.analyze_attribute_access(attribute),
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
            Node::GenericType(generic_type) => {
                // Handle generic types like List[int], Dict[str, int]
                let base_type = self.resolve_type_annotation(&generic_type.base_type)?;

                // Parse generic type arguments
                let mut type_args = Vec::new();
                for arg_node in &generic_type.type_args {
                    if let Some(arg_type) = self.resolve_type_annotation(arg_node) {
                        type_args.push(arg_type);
                    } else {
                        return None; // Failed to resolve argument type
                    }
                }

                // Validate argument count for known generic types
                match &base_type {
                    SemanticType::Builtin(builtin) => {
                        if let Some(expected_count) = builtin.generic_param_count()
                            && type_args.len() != expected_count
                        {
                            return None; // Invalid argument count
                        }
                    }
                    _ => {} // For user-defined types, allow any number of args for now
                }

                Some(SemanticType::Generic {
                    base: Box::new(base_type),
                    args: type_args,
                })
            }
            Node::Subscript(subscript) => {
                // Handle generic types like List[int], Dict[str, bool] (legacy fallback)
                if let Node::Name(base_name) = &*subscript.value {
                    if let Some(base_type) = self.symbol_table.lookup_type(&base_name.id) {
                        // Parse the subscript arguments
                        let type_args = self.parse_generic_type_args(&subscript.slice)?;

                        // Validate argument count for known generic types
                        match &base_type {
                            SemanticType::Builtin(builtin) => {
                                if let Some(expected_count) = builtin.generic_param_count()
                                    && type_args.len() != expected_count
                                {
                                    return None; // Invalid argument count
                                }

                                // Create generic instantiation
                                Some(SemanticType::Generic {
                                    base: Box::new(base_type),
                                    args: type_args,
                                })
                            }
                            _ => {
                                // For user-defined generic types
                                Some(SemanticType::Generic {
                                    base: Box::new(base_type),
                                    args: type_args,
                                })
                            }
                        }
                    } else {
                        None
                    }
                } else {
                    None
                }
            }
            _ => None,
        }
    }

    /// Parse generic type arguments from a subscript slice
    fn parse_generic_type_args(&self, slice_node: &Node) -> Option<Vec<SemanticType>> {
        match slice_node {
            // Single argument: List[int]
            Node::Name(name) => {
                if let Some(arg_type) = self.symbol_table.lookup_type(&name.id) {
                    Some(vec![arg_type])
                } else {
                    None
                }
            }
            // TypeName node: List[int] where int is a TypeName
            Node::TypeName(type_name) => {
                if let Some(arg_type) = self.symbol_table.lookup_type(&type_name.name) {
                    Some(vec![arg_type])
                } else {
                    None
                }
            }
            // Multiple arguments: Dict[str, int] (represented as tuple)
            Node::Tuple(tuple) => {
                let mut type_args = Vec::new();
                for element in &tuple.elements {
                    if let Some(arg_type) = self.resolve_type_annotation(element) {
                        type_args.push(arg_type);
                    } else {
                        return None; // Failed to resolve one of the arguments
                    }
                }
                Some(type_args)
            }
            // Handle nested subscript or other complex type expressions
            _ => {
                // Try to resolve it as a type annotation recursively
                if let Some(arg_type) = self.resolve_type_annotation(slice_node) {
                    Some(vec![arg_type])
                } else {
                    None
                }
            }
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
                        return Err(format!(
                            "len() takes exactly 1 argument, got {}",
                            arg_types.len()
                        ));
                    }
                    // For now, accept any type (could be enhanced to check if it's a collection)
                    Ok(SemanticType::Builtin(BuiltinType::Int))
                }
                "str" | "int" | "float" | "bool" => {
                    // Type conversion functions take exactly one argument
                    if arg_types.len() != 1 {
                        let func_name = name.id.as_str();
                        return Err(format!(
                            "{}() takes exactly 1 argument, got {}",
                            func_name,
                            arg_types.len()
                        ));
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
                    // Look up user-defined function or class constructor
                    if let Some(symbol) = self.symbol_table.lookup_symbol(&name.id) {
                        match &symbol.symbol_type {
                            SemanticType::Function {
                                params,
                                return_type,
                            } => {
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
                                for (i, (expected, actual)) in
                                    params.iter().zip(arg_types.iter()).enumerate()
                                {
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
                            SemanticType::Class {
                                name: class_name, ..
                            } => {
                                // Handle constructor call
                                let class_name_owned = class_name.clone();
                                self.analyze_constructor_call(&class_name_owned, &arg_types)
                            }
                            _ => Err(format!("'{}' is not a function", name.id)),
                        }
                    } else {
                        Err(format!("Undefined function: {}", name.id))
                    }
                }
            }
        } else if let Node::Attribute(attribute) = &*call.function {
            // Handle method calls (obj.method())
            self.analyze_method_call(attribute, &arg_types)
        } else {
            // Complex function expressions not yet supported
            Ok(SemanticType::Unknown("complex_function".to_string()))
        }
    }

    /// Analyze a constructor call (ClassName(args))
    fn analyze_constructor_call(
        &mut self,
        class_name: &str,
        arg_types: &[SemanticType],
    ) -> Result<SemanticType, String> {
        // Look for the class symbol first to get its type
        if let Some(class_symbol) = self.symbol_table.lookup_symbol(class_name) {
            let class_type = class_symbol.symbol_type.clone();

            // Look for __init__ method by searching through class metadata
            if let SymbolMetadata::Class { methods, .. } = &class_symbol.metadata {
                // Look for __init__ method in the class methods
                let mut init_method = None;
                for method_id in methods {
                    if let Some(method_symbol) = self.symbol_table.symbols.get(method_id)
                        && method_symbol.name == "__init__"
                    {
                        init_method = Some(method_symbol);
                        break;
                    }
                }

                if let Some(init_symbol) = init_method {
                    if let SemanticType::Function { params, .. } = &init_symbol.symbol_type {
                        // For __init__, params include self, so we skip the first parameter
                        let expected_params = if params.is_empty() {
                            0
                        } else {
                            params.len() - 1
                        };

                        if arg_types.len() != expected_params {
                            return Err(format!(
                                "Constructor for '{}' expects {} arguments, got {}",
                                class_name,
                                expected_params,
                                arg_types.len()
                            ));
                        }

                        // Validate argument types (skip self parameter)
                        for (i, (expected, actual)) in
                            params.iter().skip(1).zip(arg_types.iter()).enumerate()
                        {
                            if !self.types_compatible(actual, expected) {
                                return Err(format!(
                                    "Constructor for '{}' argument {} expects type {:?}, got {:?}",
                                    class_name,
                                    i + 1,
                                    expected,
                                    actual
                                ));
                            }
                        }
                    }
                } else {
                    // No explicit __init__ method - check if arguments are provided
                    if !arg_types.is_empty() {
                        return Err(format!(
                            "Constructor for '{}' expects 0 arguments, got {}",
                            class_name,
                            arg_types.len()
                        ));
                    }
                }
            }

            // Return instance of the class
            Ok(class_type)
        } else {
            Err(format!("Unknown class: {class_name}"))
        }
    }

    /// Analyze a subscript operation
    fn analyze_subscript(&mut self, subscript: &Subscript) -> Result<SemanticType, String> {
        let value_type = self.analyze_expression(&subscript.value)?;
        let _index_type = self.analyze_expression(&subscript.slice)?;

        // Enhanced subscript type checking with generic support
        match &value_type {
            // Generic collections
            SemanticType::Generic { base, args } => {
                match base.as_ref() {
                    SemanticType::Builtin(BuiltinType::List) if args.len() == 1 => {
                        // List[T] -> T
                        Ok(args[0].clone())
                    }
                    SemanticType::Builtin(BuiltinType::Dict) if args.len() == 2 => {
                        // Dict[K, V] -> V
                        Ok(args[1].clone())
                    }
                    SemanticType::Builtin(BuiltinType::Set) => {
                        // Set[T] doesn't support subscript
                        Err("Set objects do not support item access".to_string())
                    }
                    _ => Ok(SemanticType::Unknown("generic_subscript".to_string())),
                }
            }
            // Legacy array type
            SemanticType::Array(element_type) => Ok((**element_type).clone()),
            // String character access
            SemanticType::Builtin(BuiltinType::Str) => Ok(SemanticType::Builtin(BuiltinType::Str)),
            // Untyped containers - return object type
            SemanticType::Builtin(BuiltinType::List | BuiltinType::Dict) => {
                Ok(SemanticType::Builtin(BuiltinType::Object))
            }
            _ => Err(format!(
                "Cannot subscript type: {}",
                value_type.display_name()
            )),
        }
    }

    /// Analyze a list literal
    fn analyze_list_literal(
        &mut self,
        list: &crate::ast::node::List,
    ) -> Result<SemanticType, String> {
        if list.elements.is_empty() {
            // Empty list - return generic List with unknown element type
            return Ok(SemanticType::Generic {
                base: Box::new(SemanticType::Builtin(BuiltinType::List)),
                args: vec![SemanticType::Builtin(BuiltinType::Object)],
            });
        }

        // Analyze all elements and infer common type
        let mut element_types = Vec::new();
        for element in &list.elements {
            let element_type = self.analyze_expression(element)?;
            element_types.push(element_type);
        }

        // Find common type among all elements
        let common_type = self.find_common_type(&element_types)?;

        Ok(SemanticType::Generic {
            base: Box::new(SemanticType::Builtin(BuiltinType::List)),
            args: vec![common_type],
        })
    }

    /// Analyze a dictionary literal
    fn analyze_dict_literal(
        &mut self,
        dict: &crate::ast::node::Dict,
    ) -> Result<SemanticType, String> {
        if dict.keys.is_empty() {
            // Empty dict - return generic Dict with unknown key/value types
            return Ok(SemanticType::Generic {
                base: Box::new(SemanticType::Builtin(BuiltinType::Dict)),
                args: vec![
                    SemanticType::Builtin(BuiltinType::Object),
                    SemanticType::Builtin(BuiltinType::Object),
                ],
            });
        }

        let mut key_types = Vec::new();
        let mut value_types = Vec::new();

        for (key, value) in dict.keys.iter().zip(dict.values.iter()) {
            if let Some(key_node) = key {
                let key_type = self.analyze_expression(key_node)?;
                key_types.push(key_type);
            } else {
                // **dict expansion - skip for now
                return Ok(SemanticType::Generic {
                    base: Box::new(SemanticType::Builtin(BuiltinType::Dict)),
                    args: vec![
                        SemanticType::Builtin(BuiltinType::Object),
                        SemanticType::Builtin(BuiltinType::Object),
                    ],
                });
            }

            let value_type = self.analyze_expression(value)?;
            value_types.push(value_type);
        }

        // Find common types for keys and values
        let common_key_type = self.find_common_type(&key_types)?;
        let common_value_type = self.find_common_type(&value_types)?;

        Ok(SemanticType::Generic {
            base: Box::new(SemanticType::Builtin(BuiltinType::Dict)),
            args: vec![common_key_type, common_value_type],
        })
    }

    /// Analyze a set literal
    fn analyze_set_literal(&mut self, set: &crate::ast::node::Set) -> Result<SemanticType, String> {
        if set.elements.is_empty() {
            // Empty set - return generic Set with unknown element type
            return Ok(SemanticType::Generic {
                base: Box::new(SemanticType::Builtin(BuiltinType::Set)),
                args: vec![SemanticType::Builtin(BuiltinType::Object)],
            });
        }

        // Analyze all elements and infer common type
        let mut element_types = Vec::new();
        for element in &set.elements {
            let element_type = self.analyze_expression(element)?;
            element_types.push(element_type);
        }

        // Find common type among all elements
        let common_type = self.find_common_type(&element_types)?;

        Ok(SemanticType::Generic {
            base: Box::new(SemanticType::Builtin(BuiltinType::Set)),
            args: vec![common_type],
        })
    }

    /// Find a common type among a collection of types
    fn find_common_type(&self, types: &[SemanticType]) -> Result<SemanticType, String> {
        if types.is_empty() {
            return Ok(SemanticType::Builtin(BuiltinType::Object));
        }

        if types.len() == 1 {
            return Ok(types[0].clone());
        }

        // Check if all types are identical
        let first_type = &types[0];
        if types.iter().all(|t| t == first_type) {
            return Ok(first_type.clone());
        }

        // Check for compatible numeric types
        let all_numeric = types.iter().all(|t| {
            matches!(
                t,
                SemanticType::Builtin(BuiltinType::Int | BuiltinType::Float)
            )
        });

        if all_numeric {
            // If there's any float, the common type is float
            if types
                .iter()
                .any(|t| matches!(t, SemanticType::Builtin(BuiltinType::Float)))
            {
                return Ok(SemanticType::Builtin(BuiltinType::Float));
            }
            // Otherwise, all are integers
            return Ok(SemanticType::Builtin(BuiltinType::Int));
        }

        // For now, fall back to object type for mixed collections
        // This could be enhanced to create union types in the future
        Ok(SemanticType::Builtin(BuiltinType::Object))
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

    /// Analyze method calls (`obj.method()`)
    fn analyze_method_call(
        &mut self,
        attribute: &crate::ast::node::Attribute,
        arg_types: &[SemanticType],
    ) -> Result<SemanticType, String> {
        // Get the object type first
        let obj_type = self.analyze_expression(&attribute.value)?;
        let method_name = &attribute.attr;

        // Determine if this is a static method call by checking the AST node type
        // If the attribute.value is a Name node referring to a class symbol, it's a static call
        // If it refers to a variable symbol, it's an instance call
        let is_static_call = matches!(attribute.value.as_ref(), Node::Name(name)
            if self.symbol_table.lookup_symbol(&name.id)
                .is_some_and(|s| s.kind == SymbolKind::Class));

        // Get the method function type
        let method_type = self.resolve_attribute_type(&obj_type, method_name)?;

        match method_type {
            SemanticType::Function {
                params,
                return_type,
            } => {
                // Determine if we need to skip 'self' parameter
                // For builtin types, the method definitions don't include 'self'
                // For user-defined types, they do include 'self' for instance methods
                let params_to_check: Vec<&SemanticType> = if is_static_call {
                    // Static call: check against all parameters
                    params.iter().collect()
                } else {
                    // Instance call: check if this is a builtin method or user-defined method
                    match &obj_type {
                        SemanticType::Builtin(_) | SemanticType::Generic { .. } => {
                            // Builtin methods don't include 'self' in their parameter list
                            params.iter().collect()
                        }
                        _ => {
                            // User-defined methods include 'self', so skip the first parameter
                            params.iter().skip(1).collect()
                        }
                    }
                };

                if arg_types.len() != params_to_check.len() {
                    return Err(format!(
                        "Method '{}' expects {} arguments, got {}",
                        method_name,
                        params_to_check.len(),
                        arg_types.len()
                    ));
                }

                // Validate argument types
                for (i, (expected, actual)) in
                    params_to_check.iter().zip(arg_types.iter()).enumerate()
                {
                    if !self.types_compatible(actual, expected) {
                        return Err(format!(
                            "Method '{}' argument {} expects type {:?}, got {:?}",
                            method_name,
                            i + 1,
                            expected,
                            actual
                        ));
                    }
                }

                // Return the method's return type
                if let Some(return_type) = return_type {
                    Ok(*return_type)
                } else {
                    Ok(SemanticType::Builtin(BuiltinType::None))
                }
            }
            _ => Err(format!("'{method_name}' is not a method")),
        }
    }

    /// Analyze attribute access (obj.attr)
    fn analyze_attribute_access(
        &mut self,
        attribute: &crate::ast::node::Attribute,
    ) -> Result<SemanticType, String> {
        let obj_type = self.analyze_expression(&attribute.value)?;

        // Resolve the attribute type based on the object type
        self.resolve_attribute_type(&obj_type, &attribute.attr)
    }

    /// Resolve the type of an attribute access
    fn resolve_attribute_type(
        &self,
        obj_type: &SemanticType,
        attr_name: &str,
    ) -> Result<SemanticType, String> {
        match obj_type {
            SemanticType::Class { name, .. } | SemanticType::Struct { name, .. } => {
                // Look up the class/struct definition and find the attribute
                if let Some(class_symbol) = self.symbol_table.lookup_symbol(name) {
                    // Check class metadata for methods first
                    if let SymbolMetadata::Class {
                        methods,
                        properties,
                        members,
                        ..
                    } = &class_symbol.metadata
                    {
                        // Search in methods first
                        for method_id in methods {
                            if let Some(method_symbol) = self.symbol_table.symbols.get(method_id)
                                && method_symbol.name == attr_name
                            {
                                return Ok(method_symbol.symbol_type.clone());
                            }
                        }

                        // Search in properties
                        for property_id in properties {
                            if let Some(property_symbol) =
                                self.symbol_table.symbols.get(property_id)
                                && property_symbol.name == attr_name
                            {
                                return Ok(property_symbol.symbol_type.clone());
                            }
                        }

                        // Search in general members
                        for member_id in members {
                            if let Some(member_symbol) = self.symbol_table.symbols.get(member_id)
                                && member_symbol.name == attr_name
                            {
                                return Ok(member_symbol.symbol_type.clone());
                            }
                        }
                    }

                    // Fallback: try the old scope-based lookup
                    let scope_id = &class_symbol.scope_id;
                    if let Some(attr_symbol) = self
                        .symbol_table
                        .lookup_symbol_in_scope(attr_name, scope_id)
                    {
                        match attr_symbol.kind {
                            SymbolKind::Property | SymbolKind::Variable => {
                                Ok(attr_symbol.symbol_type.clone())
                            }
                            SymbolKind::Function | SymbolKind::Method => {
                                // For method access without call, return the function type
                                Ok(attr_symbol.symbol_type.clone())
                            }
                            _ => Err(format!("'{attr_name}' is not a valid attribute")),
                        }
                    } else {
                        Err(format!("'{name}' object has no attribute '{attr_name}'"))
                    }
                } else {
                    Err(format!("Cannot find class definition for '{name}'"))
                }
            }
            SemanticType::Builtin(builtin_type) => {
                // Handle built-in type attributes/methods
                self.resolve_builtin_attribute(builtin_type, attr_name)
            }
            SemanticType::Generic { base, args } => {
                // For generic types, resolve attributes on the base type and adjust for generic args
                let base_attr_type = self.resolve_attribute_type(base, attr_name)?;
                self.resolve_generic_method_type(&base_attr_type, args)
            }
            _ => Err(format!(
                "Cannot access attribute '{}' on type '{}'",
                attr_name,
                obj_type.display_name()
            )),
        }
    }

    /// Resolve method types for generic collections
    fn resolve_generic_method_type(
        &self,
        method_type: &SemanticType,
        generic_args: &[SemanticType],
    ) -> Result<SemanticType, String> {
        match method_type {
            SemanticType::Function {
                params,
                return_type,
            } => {
                // For generic collection methods, substitute Object with the appropriate generic type
                let adjusted_params = params
                    .iter()
                    .map(|param| {
                        match param {
                            SemanticType::Builtin(BuiltinType::Object) => {
                                // For methods like append(element), use the element type (first generic arg)
                                if generic_args.is_empty() {
                                    param.clone()
                                } else {
                                    generic_args[0].clone()
                                }
                            }
                            _ => param.clone(),
                        }
                    })
                    .collect();

                // Adjust return type if needed
                let adjusted_return_type = return_type.as_ref().map(|rt| {
                    match &**rt {
                        SemanticType::Builtin(BuiltinType::Object) => {
                            // For methods like pop(), return the element type
                            if generic_args.is_empty() {
                                rt.clone()
                            } else {
                                Box::new(generic_args[0].clone())
                            }
                        }
                        _ => rt.clone(),
                    }
                });

                Ok(SemanticType::Function {
                    params: adjusted_params,
                    return_type: adjusted_return_type,
                })
            }
            _ => Ok(method_type.clone()),
        }
    }

    /// Resolve attributes on built-in types
    fn resolve_builtin_attribute(
        &self,
        builtin_type: &BuiltinType,
        attr_name: &str,
    ) -> Result<SemanticType, String> {
        match (builtin_type, attr_name) {
            // String methods
            (BuiltinType::Str, "upper" | "lower" | "strip") => {
                Ok(SemanticType::Function {
                    params: vec![], // These methods take no arguments
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Str))),
                })
            }
            (BuiltinType::Str, "replace") => {
                Ok(SemanticType::Function {
                    params: vec![
                        SemanticType::Builtin(BuiltinType::Str), // old
                        SemanticType::Builtin(BuiltinType::Str), // new
                    ],
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Str))),
                })
            }
            (BuiltinType::Str, "split") => {
                Ok(SemanticType::Function {
                    params: vec![], // No arguments for simple split
                    return_type: Some(Box::new(SemanticType::Generic {
                        base: Box::new(SemanticType::Builtin(BuiltinType::List)),
                        args: vec![SemanticType::Builtin(BuiltinType::Str)],
                    })),
                })
            }

            // List methods
            (BuiltinType::List, "append") => {
                Ok(SemanticType::Function {
                    params: vec![SemanticType::Builtin(BuiltinType::Object)], // One argument (element to append)
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::None))),
                })
            }
            (BuiltinType::List, "extend") => {
                Ok(SemanticType::Function {
                    params: vec![SemanticType::Builtin(BuiltinType::Object)], // One argument (iterable)
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::None))),
                })
            }
            (BuiltinType::List, "insert") => {
                Ok(SemanticType::Function {
                    params: vec![
                        SemanticType::Builtin(BuiltinType::Int),    // index
                        SemanticType::Builtin(BuiltinType::Object), // element
                    ],
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::None))),
                })
            }
            (BuiltinType::List, "remove") => {
                Ok(SemanticType::Function {
                    params: vec![SemanticType::Builtin(BuiltinType::Object)], // element to remove
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::None))),
                })
            }
            (BuiltinType::List, "clear") => {
                Ok(SemanticType::Function {
                    params: vec![], // No arguments
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::None))),
                })
            }
            (BuiltinType::List, "pop") => {
                Ok(SemanticType::Function {
                    params: vec![], // Optional index argument simplified to no args for now
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Object))), // Should be element type
                })
            }
            (BuiltinType::List, "index") => {
                Ok(SemanticType::Function {
                    params: vec![SemanticType::Builtin(BuiltinType::Object)], // element to find
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Int))),
                })
            }

            // Dict methods
            (BuiltinType::Dict, "keys") => {
                Ok(SemanticType::Function {
                    params: vec![],
                    return_type: Some(Box::new(SemanticType::Generic {
                        base: Box::new(SemanticType::Builtin(BuiltinType::List)),
                        args: vec![SemanticType::Builtin(BuiltinType::Object)], // Should be key type
                    })),
                })
            }
            (BuiltinType::Dict, "values") => {
                Ok(SemanticType::Function {
                    params: vec![],
                    return_type: Some(Box::new(SemanticType::Generic {
                        base: Box::new(SemanticType::Builtin(BuiltinType::List)),
                        args: vec![SemanticType::Builtin(BuiltinType::Object)], // Should be value type
                    })),
                })
            }
            (BuiltinType::Dict, "get") => {
                Ok(SemanticType::Function {
                    params: vec![SemanticType::Builtin(BuiltinType::Object)], // key
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Object))), // Should be value type
                })
            }
            (BuiltinType::Dict, "pop") => {
                Ok(SemanticType::Function {
                    params: vec![SemanticType::Builtin(BuiltinType::Object)], // key
                    return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Object))), // Should be value type
                })
            }

            _ => Err(format!(
                "'{}' object has no attribute '{}'",
                builtin_type.as_str(),
                attr_name
            )),
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
