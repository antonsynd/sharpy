use super::{AnalysisPass, PassResult};
/// Declaration analysis pass
/// Handles collection of declarations (classes, functions, variables) without analyzing bodies
use crate::ast::node::{Assign, ClassDef, FunctionDef, Name, Node, TypedName};
use crate::semantic::module_registry::ModuleRegistry;
use crate::semantic::{
    AccessLevel, SemanticError, SemanticType, Symbol, SymbolKind, SymbolMetadata,
};
use crate::utils::SourceLocation;

/// First pass: collect all declarations
pub struct DeclarationPass;

impl AnalysisPass for DeclarationPass {
    fn name(&self) -> &'static str {
        "Declaration Pass"
    }

    fn run(&mut self, ast: &Node, registry: &mut ModuleRegistry) -> PassResult {
        let mut errors = Vec::new();

        if let Err(err) = self.collect_declarations(ast, registry) {
            errors.push(err);
        }

        PassResult {
            errors,
            should_continue: true, // Always continue to next pass
        }
    }

    fn can_continue_with_errors(&self) -> bool {
        true // Declaration errors don't prevent type checking
    }
}

impl DeclarationPass {
    /// Collect all declarations in the AST
    fn collect_declarations(
        &mut self,
        node: &Node,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        match node {
            Node::Module(module) => {
                for stmt in &module.body {
                    self.collect_declarations(stmt, registry)?;
                }
            }

            Node::ClassDef(class_def) => {
                self.collect_class_declaration(class_def, registry)?;
            }

            Node::FunctionDef(func_def) => {
                self.collect_function_declaration(func_def, registry)?;
            }

            Node::Assign(assign) => {
                // Handle variable declarations through assignments
                self.collect_variable_from_assignment(assign, registry)?;
            }

            _ => {
                // For other nodes, we might need to recurse based on the node type
                // For now, we'll skip other types as they likely don't contain top-level declarations
            }
        }

        Ok(())
    }

    /// Collect class declaration
    fn collect_class_declaration(
        &mut self,
        class_def: &ClassDef,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        let current_module = registry
            .current_module_mut()
            .ok_or(SemanticError::NoCurrentModule)?;

        // Check for duplicate class declaration
        if current_module
            .symbols
            .lookup_symbol(&class_def.name)
            .is_some()
        {
            return Err(SemanticError::DuplicateSymbol(class_def.name.clone()));
        }

        // Extract access level from access modifier or decorators
        let access_level = AccessLevel::from_modifier(class_def.access_modifier.as_deref());

        // Create class symbol
        let symbol = Symbol {
            id: format!("{}::{}", current_module.file_path, class_def.name),
            name: class_def.name.clone(),
            kind: SymbolKind::Class,
            symbol_type: SemanticType::Class {
                name: class_def.name.clone(),
                module: None,               // Will be filled later
                generic_params: Vec::new(), // TODO: Extract from type_params
            },
            access_level,
            scope_id: "module".to_string(), // TODO: Use proper scope tracking
            location: class_def
                .source
                .as_ref()
                .map(|s| SourceLocation::new(s.line_start, s.col_start, s.line_end, s.col_end)),
            is_static: false,
            generic_params: Vec::new(), // TODO: Extract from class definition
            metadata: SymbolMetadata::Class {
                base_class: None, // TODO: Extract from bases
                protocols: Vec::new(),
                members: Vec::new(),
                methods: Vec::new(),
                properties: Vec::new(),
            },
        };

        // Add to current module's symbol table
        current_module
            .symbols
            .add_symbol(symbol)
            .map_err(SemanticError::DuplicateSymbol)?;

        // TODO: We might want to enter the class scope and collect its members
        // For now, we'll handle member collection in the type analysis pass

        Ok(())
    }

    /// Collect function declaration
    fn collect_function_declaration(
        &mut self,
        func_def: &FunctionDef,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        let current_module = registry
            .current_module_mut()
            .ok_or(SemanticError::NoCurrentModule)?;

        // Check for duplicate function declaration
        if current_module
            .symbols
            .lookup_symbol(&func_def.name)
            .is_some()
        {
            return Err(SemanticError::DuplicateSymbol(func_def.name.clone()));
        }

        // Extract access level from access modifier
        let access_level = AccessLevel::from_modifier(func_def.access_modifier.as_deref());

        // Create function symbol
        let symbol = Symbol {
            id: format!("{}::{}", current_module.file_path, func_def.name),
            name: func_def.name.clone(),
            kind: SymbolKind::Function,
            symbol_type: SemanticType::Function {
                params: Vec::new(), // TODO: Extract parameter types
                return_type: None,  // TODO: Extract return type
            },
            access_level,
            scope_id: "module".to_string(), // TODO: Use proper scope tracking
            location: func_def
                .source
                .as_ref()
                .map(|s| SourceLocation::new(s.line_start, s.col_start, s.line_end, s.col_end)),
            is_static: false,
            generic_params: Vec::new(), // TODO: Extract from function definition
            metadata: SymbolMetadata::Function {
                parameters: Vec::new(),
                return_type: None,
                is_abstract: false,
            },
        };

        // Add to current module's symbol table
        current_module
            .symbols
            .add_symbol(symbol)
            .map_err(SemanticError::DuplicateSymbol)?;

        Ok(())
    }

    /// Collect variable declaration from assignment
    fn collect_variable_from_assignment(
        &mut self,
        assign: &Assign,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        match assign.target.as_ref() {
            Node::Name(name) => {
                self.collect_variable_from_name(name, registry)?;
            }
            Node::TypedName(typed_name) => {
                // Handle typed assignments like `x: int = 5`
                self.collect_variable_from_typed_name(typed_name, registry)?;
            }
            Node::Tuple(tuple) => {
                // Handle destructuring assignment
                for element in &tuple.elements {
                    match element {
                        Node::Name(name) => {
                            self.collect_variable_from_name(name, registry)?;
                        }
                        Node::TypedName(typed_name) => {
                            self.collect_variable_from_typed_name(typed_name, registry)?;
                        }
                        _ => {} // Ignore other types in destructuring for now
                    }
                }
            }
            _ => {} // Ignore other assignment targets for now
        }

        Ok(())
    }

    /// Collect variable declaration from a name node
    fn collect_variable_from_name(
        &mut self,
        name: &Name,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        let current_module = registry
            .current_module_mut()
            .ok_or(SemanticError::NoCurrentModule)?;

        // Get the current scope ID from the symbol table
        let scope_id = current_module
            .symbols
            .current_scope_id
            .clone()
            .unwrap_or_else(|| "module".to_string());

        // Check for duplicate variable declaration in current scope
        if current_module.symbols.lookup_symbol(&name.id).is_some() {
            return Err(SemanticError::DuplicateSymbol(name.id.clone()));
        }

        // Create variable symbol
        let symbol = Symbol {
            id: format!("{}::{}", current_module.file_path, name.id),
            name: name.id.clone(),
            kind: SymbolKind::Variable,
            symbol_type: SemanticType::Unknown("inferred".to_string()), // Type will be inferred later
            access_level: AccessLevel::Public, // Variables default to public
            scope_id,                          // Use the actual current scope ID
            location: name
                .source
                .as_ref()
                .map(|s| SourceLocation::new(s.line_start, s.col_start, s.line_end, s.col_end)),
            is_static: false,
            generic_params: Vec::new(),
            metadata: SymbolMetadata::Variable {
                is_mutable: true,    // TODO: Determine mutability
                is_member: false,    // This is a module-level variable
                default_value: None, // TODO: Extract from assignment
            },
        };

        // Add to current module's symbol table
        current_module
            .symbols
            .add_symbol(symbol)
            .map_err(SemanticError::DuplicateSymbol)?;

        Ok(())
    }

    /// Collect variable declaration from a typed name node (e.g., `x: int`)
    fn collect_variable_from_typed_name(
        &mut self,
        typed_name: &TypedName,
        registry: &mut ModuleRegistry,
    ) -> Result<(), SemanticError> {
        let current_module = registry
            .current_module_mut()
            .ok_or(SemanticError::NoCurrentModule)?;

        // Get the current scope ID from the symbol table
        let scope_id = current_module
            .symbols
            .current_scope_id
            .clone()
            .unwrap_or_else(|| "module".to_string());

        // Check for duplicate variable declaration in current scope
        if current_module
            .symbols
            .lookup_symbol(&typed_name.id)
            .is_some()
        {
            return Err(SemanticError::DuplicateSymbol(typed_name.id.clone()));
        }

        // Parse the type annotation from typed_name.type_
        let symbol_type = self.convert_ast_type_to_semantic_type(&typed_name.type_)?;

        // Create variable symbol
        let symbol = Symbol {
            id: format!("{}::{}", current_module.file_path, typed_name.id),
            name: typed_name.id.clone(),
            kind: SymbolKind::Variable,
            symbol_type,
            access_level: AccessLevel::Public, // Variables default to public
            scope_id,                          // Use the actual current scope ID
            location: typed_name
                .source
                .as_ref()
                .map(|s| SourceLocation::new(s.line_start, s.col_start, s.line_end, s.col_end)),
            is_static: false,
            generic_params: Vec::new(),
            metadata: SymbolMetadata::Variable {
                is_mutable: true,    // TODO: Determine mutability
                is_member: false,    // This is a module-level variable
                default_value: None, // TODO: Extract from assignment
            },
        };

        // Add to current module's symbol table
        current_module
            .symbols
            .add_symbol(symbol)
            .map_err(SemanticError::DuplicateSymbol)?;

        Ok(())
    }

    /// Convert AST type node to `SemanticType`
    fn convert_ast_type_to_semantic_type(
        &self,
        type_node: &Node,
    ) -> Result<SemanticType, SemanticError> {
        use crate::semantic::types::{BuiltinType, SemanticType};

        match type_node {
            Node::TypeName(type_name) => {
                // Convert basic type names to builtin or class types
                match type_name.name.as_str() {
                    "int" => Ok(SemanticType::Builtin(BuiltinType::Int)),
                    "float" => Ok(SemanticType::Builtin(BuiltinType::Float)),
                    "str" => Ok(SemanticType::Builtin(BuiltinType::Str)),
                    "bool" => Ok(SemanticType::Builtin(BuiltinType::Bool)),
                    "bytes" => Ok(SemanticType::Builtin(BuiltinType::Bytes)),
                    "None" => Ok(SemanticType::Builtin(BuiltinType::None)),
                    "List" => Ok(SemanticType::Builtin(BuiltinType::List)),
                    "Dict" => Ok(SemanticType::Builtin(BuiltinType::Dict)),
                    "Set" => Ok(SemanticType::Builtin(BuiltinType::Set)),
                    "Tuple" => Ok(SemanticType::Builtin(BuiltinType::Tuple)),
                    _ => {
                        // Assume it's a user-defined class for now
                        Ok(SemanticType::Class {
                            name: type_name.name.clone(),
                            module: None,
                            generic_params: Vec::new(),
                        })
                    }
                }
            }
            Node::GenericType(generic_type) => {
                // Handle generic types like List[int], Dict[str, int]
                let base_type = self.convert_ast_type_to_semantic_type(&generic_type.base_type)?;
                let mut arg_types = Vec::new();
                for arg in &generic_type.type_args {
                    arg_types.push(self.convert_ast_type_to_semantic_type(arg)?);
                }
                Ok(SemanticType::Generic {
                    base: Box::new(base_type),
                    args: arg_types,
                })
            }
            Node::OptionalType(optional_type) => {
                // Handle optional types like int?
                let inner_type =
                    self.convert_ast_type_to_semantic_type(&optional_type.inner_type)?;
                Ok(SemanticType::Optional(Box::new(inner_type)))
            }
            Node::QualifiedType(qualified_type) => {
                // Handle module-qualified types like collections.defaultdict
                Ok(SemanticType::Class {
                    name: qualified_type.name.clone(),
                    module: Some(qualified_type.module_path.join(".")),
                    generic_params: Vec::new(),
                })
            }
            Node::UnionType(union_type) => {
                // Handle union types like int | str
                let mut union_types = Vec::new();
                for type_node in &union_type.types {
                    union_types.push(self.convert_ast_type_to_semantic_type(type_node)?);
                }
                Ok(SemanticType::Union(union_types))
            }
            _ => {
                // For any other node type, return an unknown type
                Ok(SemanticType::Unknown(format!(
                    "unsupported_type_{type_node:?}"
                )))
            }
        }
    }
}
