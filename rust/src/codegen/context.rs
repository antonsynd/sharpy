/// Code generation context
///
/// Tracks state during code generation including:
/// - Current indentation level
/// - Symbol table for lookups
/// - Name mangler for collision detection
/// - Current module/class/function scope
use crate::semantic::{BuiltinSymbol, SemanticType, Symbol, SymbolTable};
use std::collections::HashMap;

use super::{CodeGenError, CodeGenResult, NameContext, NameMangler};

/// Code generation context
pub struct CodeGenContext {
    /// Current indentation level (number of spaces)
    indent: usize,

    /// Spaces per indent level
    indent_size: usize,

    /// Symbol table from semantic analysis
    symbol_table: SymbolTable,

    /// Name mangler for this scope
    mangler: NameMangler,

    /// Cache of builtin function mappings (name → `BuiltinSymbol`)
    builtin_functions: HashMap<String, BuiltinSymbol>,

    /// Cache of builtin methods (type → methods)
    builtin_methods: HashMap<String, Vec<BuiltinSymbol>>,

    /// Current module name (for namespace generation)
    current_module: Option<String>,

    /// Current class name (for member generation)
    current_class: Option<String>,

    /// Generated using statements (to avoid duplicates)
    using_statements: Vec<String>,

    /// Whether to emit #line directives for debugging
    emit_line_directives: bool,
}

impl CodeGenContext {
    /// Create a new code generation context
    #[must_use]
    pub fn new(symbol_table: SymbolTable) -> Self {
        // Load builtin mappings
        let builtin_functions = crate::semantic::builtins::create_builtin_functions()
            .into_iter()
            .map(|b| (b.symbol.name.clone(), b))
            .collect();

        let builtin_methods = crate::semantic::builtins::create_builtin_methods();

        Self {
            indent: 0,
            indent_size: 4,
            symbol_table,
            mangler: NameMangler::new(),
            builtin_functions,
            builtin_methods,
            current_module: None,
            current_class: None,
            using_statements: vec!["using System;".to_string(), "using Sharpy;".to_string()],
            emit_line_directives: false,
        }
    }

    /// Increase indentation
    pub const fn indent(&mut self) {
        self.indent += self.indent_size;
    }

    /// Decrease indentation
    pub const fn dedent(&mut self) {
        if self.indent >= self.indent_size {
            self.indent -= self.indent_size;
        }
    }

    /// Get current indentation as string
    #[must_use]
    pub fn get_indent(&self) -> String {
        " ".repeat(self.indent)
    }

    /// Mangle a name based on context
    pub fn mangle_name(
        &mut self,
        sharpy_name: &str,
        context: NameContext,
        is_literal: bool,
    ) -> CodeGenResult<String> {
        self.mangler.mangle(sharpy_name, context, is_literal)
    }

    /// Look up a symbol in the symbol table
    #[must_use]
    pub fn lookup_symbol(&self, name: &str) -> Option<&Symbol> {
        self.symbol_table.lookup_symbol(name)
    }

    /// Get builtin function mapping
    #[must_use]
    pub fn get_builtin_function(&self, name: &str) -> Option<&BuiltinSymbol> {
        self.builtin_functions.get(name)
    }

    /// Get builtin methods for a type
    #[must_use]
    pub fn get_builtin_methods(&self, type_name: &str) -> Option<&Vec<BuiltinSymbol>> {
        self.builtin_methods.get(type_name)
    }

    /// Set current module
    pub fn set_module(&mut self, name: String) {
        self.current_module = Some(name);
    }

    /// Get current module
    #[must_use]
    pub fn get_module(&self) -> Option<&str> {
        self.current_module.as_deref()
    }

    /// Set current class
    pub fn enter_class(&mut self, name: String) {
        self.current_class = Some(name);
    }

    /// Exit current class
    pub fn exit_class(&mut self) {
        self.current_class = None;
    }

    /// Get current class
    #[must_use]
    pub fn get_class(&self) -> Option<&str> {
        self.current_class.as_deref()
    }

    /// Add a using statement (if not already added)
    pub fn add_using(&mut self, namespace: &str) {
        let using_stmt = format!("using {namespace};");
        if !self.using_statements.contains(&using_stmt) {
            self.using_statements.push(using_stmt);
        }
    }

    /// Get all using statements
    #[must_use]
    pub fn get_using_statements(&self) -> &[String] {
        &self.using_statements
    }

    /// Enable/disable line directives
    pub const fn set_emit_line_directives(&mut self, enable: bool) {
        self.emit_line_directives = enable;
    }

    /// Check if line directives should be emitted
    #[must_use]
    pub const fn should_emit_line_directives(&self) -> bool {
        self.emit_line_directives
    }

    /// Enter a new scope (creates child mangler)
    pub fn enter_scope(&mut self) {
        self.mangler = self.mangler.enter_scope();
    }

    /// Exit scope (resets mangler)
    pub fn exit_scope(&mut self) {
        self.mangler.reset();
    }

    /// Convert a semantic type to C# type string
    pub fn type_to_csharp(&self, semantic_type: &SemanticType) -> CodeGenResult<String> {
        match semantic_type {
            SemanticType::Builtin(builtin) => Ok(builtin_type_to_csharp(builtin)),
            SemanticType::Class { name, .. } | SemanticType::Struct { name, .. } => {
                Ok(name.clone())
            }
            SemanticType::Protocol { name, .. } => Ok(format!("I{name}")), // Protocol → Interface
            SemanticType::Generic { base, args } => {
                let base_str = self.type_to_csharp(base)?;
                let args_str: Result<Vec<_>, _> =
                    args.iter().map(|arg| self.type_to_csharp(arg)).collect();
                let args_str = args_str?;
                Ok(format!("{base_str}<{}>", args_str.join(", ")))
            }
            SemanticType::Optional(inner) => {
                let inner_str = self.type_to_csharp(inner)?;
                Ok(format!("{inner_str}?"))
            }
            SemanticType::Function {
                params,
                return_type,
            } => {
                // Function types map to delegates
                let param_types: Result<Vec<_>, _> =
                    params.iter().map(|p| self.type_to_csharp(p)).collect();
                let param_types = param_types?;

                let return_str = if let Some(ret) = return_type {
                    self.type_to_csharp(ret)?
                } else {
                    "void".to_string()
                };

                if param_types.is_empty() {
                    Ok(format!("Func<{return_str}>"))
                } else {
                    Ok(format!("Func<{}, {return_str}>", param_types.join(", ")))
                }
            }
            SemanticType::Tuple(elements) => {
                let elem_types: Result<Vec<_>, _> =
                    elements.iter().map(|e| self.type_to_csharp(e)).collect();
                let elem_types = elem_types?;
                Ok(format!("({})", elem_types.join(", ")))
            }
            SemanticType::Array(element_type) => {
                let elem_str = self.type_to_csharp(element_type)?;
                Ok(format!("{elem_str}[]"))
            }
            SemanticType::Unknown(desc) => Err(CodeGenError::UnsupportedType {
                sharpy_type: format!("Unknown({desc})"),
                reason: "Cannot generate C# code for unknown type".to_string(),
            }),
        }
    }
}

/// Convert builtin type to C# type name
fn builtin_type_to_csharp(builtin: &crate::semantic::BuiltinType) -> String {
    use crate::semantic::BuiltinType;

    match builtin {
        BuiltinType::Int => "int".to_string(),
        BuiltinType::Float => "double".to_string(),
        BuiltinType::Str => "Str".to_string(),
        BuiltinType::Bool => "bool".to_string(),
        BuiltinType::None => "void".to_string(),
        BuiltinType::List => "List".to_string(),
        BuiltinType::Dict => "Dict".to_string(),
        BuiltinType::Set => "Set".to_string(),
        BuiltinType::Tuple => "Tuple".to_string(),
        BuiltinType::Bytes => "Bytes".to_string(),
        BuiltinType::ByteArray => "ByteArray".to_string(),
        BuiltinType::FrozenSet => "FrozenSet".to_string(),
        BuiltinType::Object => "Object".to_string(),
        BuiltinType::Exception => "Exception".to_string(),
        BuiltinType::Slice => "Slice".to_string(),
        BuiltinType::Array => "Array".to_string(),
        BuiltinType::MemoryView => "MemoryView".to_string(),
        BuiltinType::Ellipsis => "Ellipsis".to_string(),
        // Numeric types
        BuiltinType::Byte => "byte".to_string(),
        BuiltinType::Short => "short".to_string(),
        BuiltinType::Long => "long".to_string(),
        BuiltinType::UInt => "uint".to_string(),
        BuiltinType::UShort => "ushort".to_string(),
        BuiltinType::ULong => "ulong".to_string(),
        BuiltinType::SByte => "sbyte".to_string(),
        BuiltinType::Char => "char".to_string(),
        BuiltinType::Double => "double".to_string(),
        BuiltinType::Decimal => "decimal".to_string(),
        BuiltinType::Complex => "Complex".to_string(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_indent() {
        let mut ctx = CodeGenContext::new(SymbolTable::new());
        assert_eq!(ctx.get_indent(), "");

        ctx.indent();
        assert_eq!(ctx.get_indent(), "    ");

        ctx.indent();
        assert_eq!(ctx.get_indent(), "        ");

        ctx.dedent();
        assert_eq!(ctx.get_indent(), "    ");

        ctx.dedent();
        assert_eq!(ctx.get_indent(), "");
    }

    #[test]
    fn test_using_statements() {
        let mut ctx = CodeGenContext::new(SymbolTable::new());

        // Default using statements
        assert!(
            ctx.get_using_statements()
                .contains(&"using System;".to_string())
        );
        assert!(
            ctx.get_using_statements()
                .contains(&"using Sharpy;".to_string())
        );

        // Add new using
        ctx.add_using("System.Collections.Generic");
        assert!(
            ctx.get_using_statements()
                .contains(&"using System.Collections.Generic;".to_string())
        );

        // Duplicate should not be added
        let count_before = ctx.get_using_statements().len();
        ctx.add_using("System");
        assert_eq!(ctx.get_using_statements().len(), count_before);
    }

    #[test]
    fn test_builtin_type_conversion() {
        use crate::semantic::BuiltinType;

        assert_eq!(builtin_type_to_csharp(&BuiltinType::Int), "int");
        assert_eq!(builtin_type_to_csharp(&BuiltinType::Float), "double");
        assert_eq!(builtin_type_to_csharp(&BuiltinType::Str), "Str");
        assert_eq!(builtin_type_to_csharp(&BuiltinType::Bool), "bool");
        assert_eq!(builtin_type_to_csharp(&BuiltinType::None), "void");
        assert_eq!(builtin_type_to_csharp(&BuiltinType::List), "List");
    }
}
