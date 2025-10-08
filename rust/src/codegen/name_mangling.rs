/// Name mangling engine for Sharpy → C# translation
///
/// Converts Sharpy naming conventions to C# conventions:
/// - `snake_case` → `PascalCase` (types, methods, properties)
/// - `snake_case` → `camelCase` (parameters, local variables)
/// - `__dunder__` → `__Dunder__` (magic methods)
/// - `SCREAMING_SNAKE` → `PascalCase` (constants)
/// - Backtick names preserved exactly
use std::collections::HashSet;

/// Context for name mangling - determines the casing convention
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum NameContext {
    /// Type names (class, struct, protocol, enum)
    Type,
    /// Method names (instance and static)
    Method,
    /// Property names
    Property,
    /// Function parameter names
    Parameter,
    /// Local variable names
    LocalVariable,
    /// Constant names
    Constant,
    /// Module/namespace names
    Module,
    /// Enum variant names
    EnumVariant,
}

/// Name mangling engine with collision detection
pub struct NameMangler {
    /// Track used C# names to detect collisions
    used_names: HashSet<String>,
}

impl NameMangler {
    /// Create a new name mangler
    #[must_use]
    pub fn new() -> Self {
        Self {
            used_names: HashSet::new(),
        }
    }

    /// Mangle a Sharpy name to C# based on context
    ///
    /// # Arguments
    /// * `sharpy_name` - The original Sharpy name
    /// * `context` - The naming context (type, parameter, etc.)
    /// * `is_literal` - If true (backtick name), preserve exactly
    ///
    /// # Returns
    /// The mangled C# name
    ///
    /// # Errors
    /// Returns error if name collision detected
    pub fn mangle(
        &mut self,
        sharpy_name: &str,
        context: NameContext,
        is_literal: bool,
    ) -> Result<String, super::CodeGenError> {
        // Backtick names are preserved exactly
        if is_literal {
            let csharp_name = sharpy_name.to_string();
            self.check_collision(sharpy_name, &csharp_name)?;
            self.used_names.insert(csharp_name.clone());
            return Ok(csharp_name);
        }

        // Check for dunder methods
        if sharpy_name.starts_with("__") && sharpy_name.ends_with("__") {
            let csharp_name = Self::mangle_dunder(sharpy_name);
            self.check_collision(sharpy_name, &csharp_name)?;
            self.used_names.insert(csharp_name.clone());
            return Ok(csharp_name);
        }

        // Apply context-specific mangling
        let csharp_name = match context {
            NameContext::Type
            | NameContext::Method
            | NameContext::Property
            | NameContext::Constant
            | NameContext::Module
            | NameContext::EnumVariant => Self::to_pascal_case(sharpy_name),
            NameContext::Parameter | NameContext::LocalVariable => Self::to_camel_case(sharpy_name),
        };

        self.check_collision(sharpy_name, &csharp_name)?;
        self.used_names.insert(csharp_name.clone());
        Ok(csharp_name)
    }

    /// Convert `snake_case` to `PascalCase`
    fn to_pascal_case(name: &str) -> String {
        name.split('_')
            .filter(|s| !s.is_empty())
            .map(|word| {
                let mut chars = word.chars();
                match chars.next() {
                    Some(first) => first.to_uppercase().collect::<String>() + chars.as_str(),
                    None => String::new(),
                }
            })
            .collect()
    }

    /// Convert `snake_case` to camelCase
    fn to_camel_case(name: &str) -> String {
        let pascal = Self::to_pascal_case(name);
        let mut chars = pascal.chars();
        match chars.next() {
            Some(first) => first.to_lowercase().collect::<String>() + chars.as_str(),
            None => String::new(),
        }
    }

    /// Convert __dunder__ to __Dunder__
    fn mangle_dunder(name: &str) -> String {
        if name.starts_with("__") && name.ends_with("__") {
            let core = &name[2..name.len() - 2];
            format!("__{core}__", core = Self::to_pascal_case(core))
        } else {
            name.to_string()
        }
    }

    /// Check for name collision
    fn check_collision(
        &self,
        sharpy_name: &str,
        csharp_name: &str,
    ) -> Result<(), super::CodeGenError> {
        if self.used_names.contains(csharp_name) {
            return Err(super::CodeGenError::NameCollision {
                sharpy_name1: sharpy_name.to_string(),
                sharpy_name2: String::new(), // We don't track the original name
                csharp_name: csharp_name.to_string(),
            });
        }
        Ok(())
    }

    /// Reset the mangler (useful for new scopes)
    pub fn reset(&mut self) {
        self.used_names.clear();
    }

    /// Create a new scope (returns new mangler that inherits parent names)
    #[must_use]
    pub fn enter_scope(&self) -> Self {
        Self {
            used_names: self.used_names.clone(),
        }
    }
}

impl Default for NameMangler {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_pascal_case() {
        assert_eq!(NameMangler::to_pascal_case("hello"), "Hello");
        assert_eq!(NameMangler::to_pascal_case("hello_world"), "HelloWorld");
        assert_eq!(
            NameMangler::to_pascal_case("calculate_area"),
            "CalculateArea"
        );
        assert_eq!(NameMangler::to_pascal_case("http_request"), "HttpRequest");
    }

    #[test]
    fn test_camel_case() {
        assert_eq!(NameMangler::to_camel_case("hello"), "hello");
        assert_eq!(NameMangler::to_camel_case("hello_world"), "helloWorld");
        assert_eq!(NameMangler::to_camel_case("first_name"), "firstName");
        assert_eq!(NameMangler::to_camel_case("http_request"), "httpRequest");
    }

    #[test]
    fn test_dunder_mangling() {
        assert_eq!(NameMangler::mangle_dunder("__str__"), "__Str__");
        assert_eq!(NameMangler::mangle_dunder("__repr__"), "__Repr__");
        assert_eq!(NameMangler::mangle_dunder("__eq__"), "__Eq__");
        assert_eq!(NameMangler::mangle_dunder("__add__"), "__Add__");
        assert_eq!(NameMangler::mangle_dunder("__init__"), "__Init__");
    }

    #[test]
    fn test_context_specific_mangling() {
        let mut mangler = NameMangler::new();

        // Type names use PascalCase
        let type_name = mangler
            .mangle("my_class", NameContext::Type, false)
            .unwrap();
        assert_eq!(type_name, "MyClass");

        // Parameters use camelCase
        let param_name = mangler
            .mangle("first_name", NameContext::Parameter, false)
            .unwrap();
        assert_eq!(param_name, "firstName");

        // Methods use PascalCase
        let method_name = mangler
            .mangle("calculate_sum", NameContext::Method, false)
            .unwrap();
        assert_eq!(method_name, "CalculateSum");
    }

    #[test]
    fn test_literal_names_preserved() {
        let mut mangler = NameMangler::new();

        // Backtick names are preserved
        let literal = mangler.mangle("MyClass", NameContext::Type, true).unwrap();
        assert_eq!(literal, "MyClass");

        let literal2 = mangler
            .mangle("weird_NAME", NameContext::Type, true)
            .unwrap();
        assert_eq!(literal2, "weird_NAME");
    }

    #[test]
    fn test_collision_detection() {
        let mut mangler = NameMangler::new();

        // First name is fine
        mangler
            .mangle("process_data", NameContext::Method, false)
            .unwrap();

        // This would collide (both mangle to ProcessData)
        let result = mangler.mangle("ProcessData", NameContext::Method, true);
        assert!(result.is_err());
    }

    #[test]
    fn test_scope_isolation() {
        let mut parent = NameMangler::new();
        parent
            .mangle("my_var", NameContext::LocalVariable, false)
            .unwrap();

        // Child scope inherits parent names
        let mut child = parent.enter_scope();
        let result = child.mangle("my_var", NameContext::LocalVariable, false);
        assert!(result.is_err()); // Collision with parent

        // But child can add new names
        child
            .mangle("other_var", NameContext::LocalVariable, false)
            .unwrap();
    }
}
