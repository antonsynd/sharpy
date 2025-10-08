/// Builtin symbol definitions for the Sharpy language
///
/// This module provides comprehensive builtin type and function definitions
/// with metadata needed for both semantic analysis and code generation.
use crate::semantic::{AccessLevel, BuiltinType, SemanticType, Symbol, SymbolKind, SymbolMetadata};
use std::collections::HashMap;

/// Metadata about how a builtin maps to C# implementation
#[derive(Debug, Clone)]
pub struct CSharpMapping {
    /// The C# name (after name mangling)
    pub csharp_name: String,
    /// The C# namespace where this symbol is defined
    pub csharp_namespace: String,
    /// The full C# type path for methods (e.g., "Sharpy.Str")
    pub csharp_type: Option<String>,
    /// Whether this is a static method/function
    pub is_static: bool,
    /// Whether this is an extension method
    pub is_extension: bool,
}

impl CSharpMapping {
    /// Create a new C# mapping for a builtin function
    #[must_use]
    pub fn builtin_function(name: &str) -> Self {
        Self {
            csharp_name: to_pascal_case(name),
            csharp_namespace: "Sharpy".to_string(),
            csharp_type: Some("__Exports__".to_string()),
            is_static: true,
            is_extension: false,
        }
    }

    /// Create a new C# mapping for a type constructor
    #[must_use]
    pub fn type_constructor(_sharpy_type: &str, csharp_type: &str) -> Self {
        Self {
            csharp_name: csharp_type.to_string(),
            csharp_namespace: "Sharpy".to_string(),
            csharp_type: None,
            is_static: false,
            is_extension: false,
        }
    }

    /// Create a new C# mapping for an instance method
    #[must_use]
    pub fn instance_method(csharp_type: &str, method_name: &str) -> Self {
        Self {
            csharp_name: to_pascal_case(method_name),
            csharp_namespace: "Sharpy".to_string(),
            csharp_type: Some(csharp_type.to_string()),
            is_static: false,
            is_extension: false,
        }
    }

    /// Create a new C# mapping for a dunder method
    #[must_use]
    pub fn dunder_method(csharp_type: &str, dunder_name: &str) -> Self {
        Self {
            csharp_name: to_dunder_case(dunder_name),
            csharp_namespace: "Sharpy".to_string(),
            csharp_type: Some(csharp_type.to_string()),
            is_static: false,
            is_extension: false,
        }
    }
}

/// Extended symbol with C# mapping metadata
#[derive(Debug, Clone)]
pub struct BuiltinSymbol {
    pub symbol: Symbol,
    pub csharp_mapping: CSharpMapping,
}

/// Convert `snake_case` to `PascalCase`
fn to_pascal_case(name: &str) -> String {
    name.split('_')
        .map(|word| {
            let mut chars = word.chars();
            match chars.next() {
                Some(first) => first.to_uppercase().collect::<String>() + chars.as_str(),
                None => String::new(),
            }
        })
        .collect()
}

/// Convert dunder method names (e.g., __str__ -> __Str__)
fn to_dunder_case(name: &str) -> String {
    if name.starts_with("__") && name.ends_with("__") {
        let core = &name[2..name.len() - 2];
        format!("__{core}__", core = to_pascal_case(core))
    } else {
        to_pascal_case(name)
    }
}

/// Create all builtin function symbols
#[must_use]
pub fn create_builtin_functions() -> Vec<BuiltinSymbol> {
    vec![
        // === I/O Functions ===
        builtin_function(
            "print",
            vec![], // Variadic - takes any number of arguments
            None,
            "Print output to stdout",
        ),
        builtin_function(
            "input",
            vec![SemanticType::Builtin(BuiltinType::Str)], // Optional prompt
            Some(SemanticType::Builtin(BuiltinType::Str)),
            "Read a line from stdin",
        ),
        // === Type Conversion Functions ===
        builtin_function(
            "int",
            vec![SemanticType::Unknown("any".to_string())],
            Some(SemanticType::Builtin(BuiltinType::Int)),
            "Convert to integer",
        ),
        builtin_function(
            "float",
            vec![SemanticType::Unknown("any".to_string())],
            Some(SemanticType::Builtin(BuiltinType::Float)),
            "Convert to float",
        ),
        builtin_function(
            "str",
            vec![SemanticType::Unknown("any".to_string())],
            Some(SemanticType::Builtin(BuiltinType::Str)),
            "Convert to string",
        ),
        builtin_function(
            "bool",
            vec![SemanticType::Unknown("any".to_string())],
            Some(SemanticType::Builtin(BuiltinType::Bool)),
            "Convert to boolean",
        ),
        builtin_function(
            "list",
            vec![], // Can take an iterable or no args
            Some(SemanticType::Builtin(BuiltinType::List)),
            "Create a list",
        ),
        builtin_function(
            "dict",
            vec![],
            Some(SemanticType::Builtin(BuiltinType::Dict)),
            "Create a dictionary",
        ),
        builtin_function(
            "set",
            vec![],
            Some(SemanticType::Builtin(BuiltinType::Set)),
            "Create a set",
        ),
        builtin_function(
            "tuple",
            vec![],
            Some(SemanticType::Builtin(BuiltinType::Tuple)),
            "Create a tuple",
        ),
        // === Collection Functions ===
        builtin_function(
            "len",
            vec![SemanticType::Unknown("collection".to_string())],
            Some(SemanticType::Builtin(BuiltinType::Int)),
            "Return the length of a collection",
        ),
        builtin_function(
            "range",
            vec![
                SemanticType::Builtin(BuiltinType::Int), // stop
            ],
            Some(SemanticType::Unknown("range".to_string())),
            "Create a range object",
        ),
        builtin_function(
            "enumerate",
            vec![SemanticType::Unknown("iterable".to_string())],
            Some(SemanticType::Unknown("enumerate".to_string())),
            "Return an enumerate object",
        ),
        builtin_function(
            "zip",
            vec![], // Variadic - takes multiple iterables
            Some(SemanticType::Unknown("zip".to_string())),
            "Zip multiple iterables together",
        ),
        builtin_function(
            "reversed",
            vec![SemanticType::Unknown("sequence".to_string())],
            Some(SemanticType::Unknown("reversed".to_string())),
            "Return a reversed iterator",
        ),
        builtin_function(
            "sorted",
            vec![SemanticType::Unknown("iterable".to_string())],
            Some(SemanticType::Builtin(BuiltinType::List)),
            "Return a sorted list",
        ),
        // === Math Functions ===
        builtin_function(
            "abs",
            vec![SemanticType::Unknown("number".to_string())],
            Some(SemanticType::Unknown("number".to_string())),
            "Return the absolute value",
        ),
        builtin_function(
            "min",
            vec![], // Variadic
            Some(SemanticType::Unknown("any".to_string())),
            "Return the minimum value",
        ),
        builtin_function(
            "max",
            vec![], // Variadic
            Some(SemanticType::Unknown("any".to_string())),
            "Return the maximum value",
        ),
        builtin_function(
            "sum",
            vec![SemanticType::Unknown("iterable".to_string())],
            Some(SemanticType::Unknown("number".to_string())),
            "Sum all elements",
        ),
        builtin_function(
            "round",
            vec![SemanticType::Builtin(BuiltinType::Float)],
            Some(SemanticType::Builtin(BuiltinType::Int)),
            "Round to nearest integer",
        ),
        builtin_function(
            "pow",
            vec![
                SemanticType::Unknown("base".to_string()),
                SemanticType::Unknown("exp".to_string()),
            ],
            Some(SemanticType::Unknown("number".to_string())),
            "Raise to power",
        ),
        // === Introspection Functions ===
        builtin_function(
            "type",
            vec![SemanticType::Unknown("any".to_string())],
            Some(SemanticType::Unknown("type".to_string())),
            "Return the type of an object",
        ),
        builtin_function(
            "isinstance",
            vec![
                SemanticType::Unknown("object".to_string()),
                SemanticType::Unknown("type".to_string()),
            ],
            Some(SemanticType::Builtin(BuiltinType::Bool)),
            "Check if object is instance of type",
        ),
        builtin_function(
            "hasattr",
            vec![
                SemanticType::Unknown("object".to_string()),
                SemanticType::Builtin(BuiltinType::Str),
            ],
            Some(SemanticType::Builtin(BuiltinType::Bool)),
            "Check if object has attribute",
        ),
        builtin_function(
            "getattr",
            vec![
                SemanticType::Unknown("object".to_string()),
                SemanticType::Builtin(BuiltinType::Str),
            ],
            Some(SemanticType::Unknown("any".to_string())),
            "Get attribute value",
        ),
        builtin_function(
            "setattr",
            vec![
                SemanticType::Unknown("object".to_string()),
                SemanticType::Builtin(BuiltinType::Str),
                SemanticType::Unknown("value".to_string()),
            ],
            None,
            "Set attribute value",
        ),
        // === String Functions ===
        builtin_function(
            "repr",
            vec![SemanticType::Unknown("any".to_string())],
            Some(SemanticType::Builtin(BuiltinType::Str)),
            "Return repr string representation",
        ),
        builtin_function(
            "chr",
            vec![SemanticType::Builtin(BuiltinType::Int)],
            Some(SemanticType::Builtin(BuiltinType::Str)),
            "Convert int to character",
        ),
        builtin_function(
            "ord",
            vec![SemanticType::Builtin(BuiltinType::Str)],
            Some(SemanticType::Builtin(BuiltinType::Int)),
            "Convert character to int",
        ),
        // === Iterator Functions ===
        builtin_function(
            "iter",
            vec![SemanticType::Unknown("iterable".to_string())],
            Some(SemanticType::Unknown("iterator".to_string())),
            "Create an iterator",
        ),
        builtin_function(
            "next",
            vec![SemanticType::Unknown("iterator".to_string())],
            Some(SemanticType::Unknown("any".to_string())),
            "Get next item from iterator",
        ),
        builtin_function(
            "all",
            vec![SemanticType::Unknown("iterable".to_string())],
            Some(SemanticType::Builtin(BuiltinType::Bool)),
            "Check if all elements are truthy",
        ),
        builtin_function(
            "any",
            vec![SemanticType::Unknown("iterable".to_string())],
            Some(SemanticType::Builtin(BuiltinType::Bool)),
            "Check if any element is truthy",
        ),
        builtin_function(
            "filter",
            vec![
                SemanticType::Unknown("function".to_string()),
                SemanticType::Unknown("iterable".to_string()),
            ],
            Some(SemanticType::Unknown("filter".to_string())),
            "Filter elements",
        ),
        builtin_function(
            "map",
            vec![
                SemanticType::Unknown("function".to_string()),
                SemanticType::Unknown("iterable".to_string()),
            ],
            Some(SemanticType::Unknown("map".to_string())),
            "Map function over iterable",
        ),
        // === Utility Functions ===
        builtin_function(
            "id",
            vec![SemanticType::Unknown("any".to_string())],
            Some(SemanticType::Builtin(BuiltinType::Int)),
            "Return object identity",
        ),
        builtin_function(
            "hash",
            vec![SemanticType::Unknown("any".to_string())],
            Some(SemanticType::Builtin(BuiltinType::Int)),
            "Return hash value",
        ),
    ]
}

/// Helper to create a builtin function symbol
fn builtin_function(
    name: &str,
    params: Vec<SemanticType>,
    return_type: Option<SemanticType>,
    _documentation: &str,
) -> BuiltinSymbol {
    let symbol = Symbol {
        id: format!("builtin::{name}"),
        name: name.to_string(),
        kind: SymbolKind::Function,
        symbol_type: SemanticType::Function {
            params,
            return_type: return_type.clone().map(Box::new),
        },
        access_level: AccessLevel::Public,
        scope_id: "builtin".to_string(),
        location: None,
        is_static: true,
        generic_params: Vec::new(),
        metadata: SymbolMetadata::Function {
            parameters: Vec::new(),
            return_type,
            is_abstract: false,
        },
    };

    BuiltinSymbol {
        symbol,
        csharp_mapping: CSharpMapping::builtin_function(name),
    }
}

/// Create all builtin type methods
#[must_use]
pub fn create_builtin_methods() -> HashMap<String, Vec<BuiltinSymbol>> {
    let mut all_methods = HashMap::new();

    // String methods
    all_methods.insert("str".to_string(), create_str_methods());
    all_methods.insert("list".to_string(), create_list_methods());
    all_methods.insert("dict".to_string(), create_dict_methods());
    all_methods.insert("set".to_string(), create_set_methods());

    all_methods
}

/// Create string instance methods
fn create_str_methods() -> Vec<BuiltinSymbol> {
    vec![
        instance_method(
            "str",
            "upper",
            vec![],
            SemanticType::Builtin(BuiltinType::Str),
        ),
        instance_method(
            "str",
            "lower",
            vec![],
            SemanticType::Builtin(BuiltinType::Str),
        ),
        instance_method(
            "str",
            "strip",
            vec![],
            SemanticType::Builtin(BuiltinType::Str),
        ),
        instance_method(
            "str",
            "lstrip",
            vec![],
            SemanticType::Builtin(BuiltinType::Str),
        ),
        instance_method(
            "str",
            "rstrip",
            vec![],
            SemanticType::Builtin(BuiltinType::Str),
        ),
        instance_method(
            "str",
            "split",
            vec![],
            SemanticType::Generic {
                base: Box::new(SemanticType::Builtin(BuiltinType::List)),
                args: vec![SemanticType::Builtin(BuiltinType::Str)],
            },
        ),
        instance_method(
            "str",
            "join",
            vec![SemanticType::Unknown("iterable".to_string())],
            SemanticType::Builtin(BuiltinType::Str),
        ),
        instance_method(
            "str",
            "replace",
            vec![
                SemanticType::Builtin(BuiltinType::Str),
                SemanticType::Builtin(BuiltinType::Str),
            ],
            SemanticType::Builtin(BuiltinType::Str),
        ),
        instance_method(
            "str",
            "startswith",
            vec![SemanticType::Builtin(BuiltinType::Str)],
            SemanticType::Builtin(BuiltinType::Bool),
        ),
        instance_method(
            "str",
            "endswith",
            vec![SemanticType::Builtin(BuiltinType::Str)],
            SemanticType::Builtin(BuiltinType::Bool),
        ),
        instance_method(
            "str",
            "find",
            vec![SemanticType::Builtin(BuiltinType::Str)],
            SemanticType::Builtin(BuiltinType::Int),
        ),
        instance_method(
            "str",
            "count",
            vec![SemanticType::Builtin(BuiltinType::Str)],
            SemanticType::Builtin(BuiltinType::Int),
        ),
        // Dunder methods
        dunder_method(
            "str",
            "__str__",
            vec![],
            SemanticType::Builtin(BuiltinType::Str),
        ),
        dunder_method(
            "str",
            "__repr__",
            vec![],
            SemanticType::Builtin(BuiltinType::Str),
        ),
        dunder_method(
            "str",
            "__len__",
            vec![],
            SemanticType::Builtin(BuiltinType::Int),
        ),
        dunder_method(
            "str",
            "__eq__",
            vec![SemanticType::Builtin(BuiltinType::Str)],
            SemanticType::Builtin(BuiltinType::Bool),
        ),
    ]
}

/// Create list instance methods
fn create_list_methods() -> Vec<BuiltinSymbol> {
    vec![
        instance_method(
            "list",
            "append",
            vec![SemanticType::Unknown("item".to_string())],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "list",
            "extend",
            vec![SemanticType::Unknown("iterable".to_string())],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "list",
            "insert",
            vec![
                SemanticType::Builtin(BuiltinType::Int),
                SemanticType::Unknown("item".to_string()),
            ],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "list",
            "remove",
            vec![SemanticType::Unknown("item".to_string())],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "list",
            "pop",
            vec![],
            SemanticType::Unknown("item".to_string()),
        ),
        instance_method(
            "list",
            "clear",
            vec![],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "list",
            "index",
            vec![SemanticType::Unknown("item".to_string())],
            SemanticType::Builtin(BuiltinType::Int),
        ),
        instance_method(
            "list",
            "count",
            vec![SemanticType::Unknown("item".to_string())],
            SemanticType::Builtin(BuiltinType::Int),
        ),
        instance_method(
            "list",
            "sort",
            vec![],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "list",
            "reverse",
            vec![],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "list",
            "copy",
            vec![],
            SemanticType::Builtin(BuiltinType::List),
        ),
        // Dunder methods
        dunder_method(
            "list",
            "__len__",
            vec![],
            SemanticType::Builtin(BuiltinType::Int),
        ),
        dunder_method(
            "list",
            "__getitem__",
            vec![SemanticType::Builtin(BuiltinType::Int)],
            SemanticType::Unknown("item".to_string()),
        ),
        dunder_method(
            "list",
            "__setitem__",
            vec![
                SemanticType::Builtin(BuiltinType::Int),
                SemanticType::Unknown("item".to_string()),
            ],
            SemanticType::Builtin(BuiltinType::None),
        ),
    ]
}

/// Create dict instance methods
fn create_dict_methods() -> Vec<BuiltinSymbol> {
    vec![
        instance_method(
            "dict",
            "keys",
            vec![],
            SemanticType::Unknown("dict_keys".to_string()),
        ),
        instance_method(
            "dict",
            "values",
            vec![],
            SemanticType::Unknown("dict_values".to_string()),
        ),
        instance_method(
            "dict",
            "items",
            vec![],
            SemanticType::Unknown("dict_items".to_string()),
        ),
        instance_method(
            "dict",
            "get",
            vec![SemanticType::Unknown("key".to_string())],
            SemanticType::Unknown("value".to_string()),
        ),
        instance_method(
            "dict",
            "pop",
            vec![SemanticType::Unknown("key".to_string())],
            SemanticType::Unknown("value".to_string()),
        ),
        instance_method(
            "dict",
            "clear",
            vec![],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "dict",
            "update",
            vec![SemanticType::Unknown("other".to_string())],
            SemanticType::Builtin(BuiltinType::None),
        ),
        // Dunder methods
        dunder_method(
            "dict",
            "__len__",
            vec![],
            SemanticType::Builtin(BuiltinType::Int),
        ),
        dunder_method(
            "dict",
            "__getitem__",
            vec![SemanticType::Unknown("key".to_string())],
            SemanticType::Unknown("value".to_string()),
        ),
        dunder_method(
            "dict",
            "__setitem__",
            vec![
                SemanticType::Unknown("key".to_string()),
                SemanticType::Unknown("value".to_string()),
            ],
            SemanticType::Builtin(BuiltinType::None),
        ),
        dunder_method(
            "dict",
            "__contains__",
            vec![SemanticType::Unknown("key".to_string())],
            SemanticType::Builtin(BuiltinType::Bool),
        ),
    ]
}

/// Create set instance methods
fn create_set_methods() -> Vec<BuiltinSymbol> {
    vec![
        instance_method(
            "set",
            "add",
            vec![SemanticType::Unknown("item".to_string())],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "set",
            "remove",
            vec![SemanticType::Unknown("item".to_string())],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "set",
            "discard",
            vec![SemanticType::Unknown("item".to_string())],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "set",
            "pop",
            vec![],
            SemanticType::Unknown("item".to_string()),
        ),
        instance_method(
            "set",
            "clear",
            vec![],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "set",
            "union",
            vec![SemanticType::Builtin(BuiltinType::Set)],
            SemanticType::Builtin(BuiltinType::Set),
        ),
        instance_method(
            "set",
            "intersection",
            vec![SemanticType::Builtin(BuiltinType::Set)],
            SemanticType::Builtin(BuiltinType::Set),
        ),
        instance_method(
            "set",
            "difference",
            vec![SemanticType::Builtin(BuiltinType::Set)],
            SemanticType::Builtin(BuiltinType::Set),
        ),
        instance_method(
            "set",
            "symmetric_difference",
            vec![SemanticType::Builtin(BuiltinType::Set)],
            SemanticType::Builtin(BuiltinType::Set),
        ),
        instance_method(
            "set",
            "update",
            vec![SemanticType::Unknown("other".to_string())],
            SemanticType::Builtin(BuiltinType::None),
        ),
        instance_method(
            "set",
            "issubset",
            vec![SemanticType::Builtin(BuiltinType::Set)],
            SemanticType::Builtin(BuiltinType::Bool),
        ),
        instance_method(
            "set",
            "issuperset",
            vec![SemanticType::Builtin(BuiltinType::Set)],
            SemanticType::Builtin(BuiltinType::Bool),
        ),
        instance_method(
            "set",
            "isdisjoint",
            vec![SemanticType::Builtin(BuiltinType::Set)],
            SemanticType::Builtin(BuiltinType::Bool),
        ),
        // Dunder methods
        dunder_method(
            "set",
            "__len__",
            vec![],
            SemanticType::Builtin(BuiltinType::Int),
        ),
        dunder_method(
            "set",
            "__contains__",
            vec![SemanticType::Unknown("item".to_string())],
            SemanticType::Builtin(BuiltinType::Bool),
        ),
    ]
}

/// Helper to create an instance method symbol
fn instance_method(
    type_name: &str,
    method_name: &str,
    params: Vec<SemanticType>,
    return_type: SemanticType,
) -> BuiltinSymbol {
    let symbol = Symbol {
        id: format!("builtin::{type_name}.{method_name}"),
        name: method_name.to_string(),
        kind: SymbolKind::Method,
        symbol_type: SemanticType::Function {
            params,
            return_type: Some(Box::new(return_type.clone())),
        },
        access_level: AccessLevel::Public,
        scope_id: format!("builtin::{type_name}"),
        location: None,
        is_static: false,
        generic_params: Vec::new(),
        metadata: SymbolMetadata::Method {
            parameters: Vec::new(),
            return_type: Some(return_type),
            is_abstract: false,
            is_override: false,
            is_virtual: false,
        },
    };

    let csharp_type = match type_name {
        "str" => "Str",
        "list" => "List",
        "dict" => "Dict",
        "set" => "Set",
        _ => type_name,
    };

    BuiltinSymbol {
        symbol,
        csharp_mapping: CSharpMapping::instance_method(csharp_type, method_name),
    }
}

/// Helper to create a dunder method symbol
fn dunder_method(
    type_name: &str,
    dunder_name: &str,
    params: Vec<SemanticType>,
    return_type: SemanticType,
) -> BuiltinSymbol {
    let symbol = Symbol {
        id: format!("builtin::{type_name}.{dunder_name}"),
        name: dunder_name.to_string(),
        kind: SymbolKind::Method,
        symbol_type: SemanticType::Function {
            params,
            return_type: Some(Box::new(return_type.clone())),
        },
        access_level: AccessLevel::Public,
        scope_id: format!("builtin::{type_name}"),
        location: None,
        is_static: false,
        generic_params: Vec::new(),
        metadata: SymbolMetadata::Method {
            parameters: Vec::new(),
            return_type: Some(return_type),
            is_abstract: false,
            is_override: false,
            is_virtual: false,
        },
    };

    let csharp_type = match type_name {
        "str" => "Str",
        "list" => "List",
        "dict" => "Dict",
        "set" => "Set",
        _ => type_name,
    };

    BuiltinSymbol {
        symbol,
        csharp_mapping: CSharpMapping::dunder_method(csharp_type, dunder_name),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_to_pascal_case() {
        assert_eq!(to_pascal_case("print"), "Print");
        assert_eq!(to_pascal_case("enumerate"), "Enumerate");
        assert_eq!(
            to_pascal_case("symmetric_difference"),
            "SymmetricDifference"
        );
    }

    #[test]
    fn test_to_dunder_case() {
        assert_eq!(to_dunder_case("__str__"), "__Str__");
        assert_eq!(to_dunder_case("__repr__"), "__Repr__");
        assert_eq!(to_dunder_case("__eq__"), "__Eq__");
    }

    #[test]
    fn test_builtin_functions_count() {
        let functions = create_builtin_functions();
        assert!(functions.len() > 30, "Should have many builtin functions");
    }

    #[test]
    fn test_csharp_mapping_for_print() {
        let functions = create_builtin_functions();
        let print_fn = functions.iter().find(|f| f.symbol.name == "print").unwrap();
        assert_eq!(print_fn.csharp_mapping.csharp_name, "Print");
        assert_eq!(print_fn.csharp_mapping.csharp_namespace, "Sharpy");
        assert!(print_fn.csharp_mapping.is_static);
    }
}
