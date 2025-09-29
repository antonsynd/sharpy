use std::collections::HashMap;

/// Represents different kinds of types in the Sharpy type system
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SemanticType {
    /// Built-in primitive types
    Builtin(BuiltinType),
    /// User-defined classes
    Class {
        name: String,
        module: Option<String>,
        generic_params: Vec<String>,
    },
    /// User-defined structs
    Struct {
        name: String,
        module: Option<String>,
        generic_params: Vec<String>,
    },
    /// User-defined protocols
    Protocol {
        name: String,
        module: Option<String>,
        generic_params: Vec<String>,
    },
    /// Generic type instantiation (e.g., List[int])
    Generic {
        base: Box<SemanticType>,
        args: Vec<SemanticType>,
    },
    /// Optional/nullable types (e.g., int?)
    Optional(Box<SemanticType>),
    /// Function types
    Function {
        params: Vec<SemanticType>,
        return_type: Option<Box<SemanticType>>,
    },
    /// Tuple types
    Tuple(Vec<SemanticType>),
    /// Union types (int | str)
    Union(Vec<SemanticType>),
    /// Array types (array[T])
    Array(Box<SemanticType>),
    /// Unknown/unresolved type
    Unknown(String),
}

/// Built-in types in Sharpy
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum BuiltinType {
    // Primitives
    Int,
    Float,
    Bool,
    Str,
    None,

    // Numeric types
    Byte,
    Short,
    Long,
    UInt,
    UShort,
    ULong,
    SByte,
    Char,
    Double,
    Decimal,
    Complex,

    // Collections (generic)
    List,      // List[T]
    Dict,      // Dict[K, V]
    Set,       // Set[T]
    Tuple,     // Tuple[...]
    Array,     // Array[T]
    FrozenSet, // FrozenSet[T]

    // Other built-ins
    Object,
    Bytes,
    ByteArray,
    Exception,
    Ellipsis,
    Slice,
    MemoryView,
}

impl BuiltinType {
    /// Get the string representation of the built-in type
    #[must_use]
    pub const fn as_str(&self) -> &'static str {
        match self {
            Self::Int => "int",
            Self::Float => "float",
            Self::Bool => "bool",
            Self::Str => "str",
            Self::None => "None",
            Self::Byte => "byte",
            Self::Short => "short",
            Self::Long => "long",
            Self::UInt => "uint",
            Self::UShort => "ushort",
            Self::ULong => "ulong",
            Self::SByte => "sbyte",
            Self::Char => "char",
            Self::Double => "double",
            Self::Decimal => "decimal",
            Self::Complex => "complex",
            Self::List => "list",
            Self::Dict => "dict",
            Self::Set => "set",
            Self::Tuple => "tuple",
            Self::Array => "array",
            Self::FrozenSet => "frozenset",
            Self::Object => "object",
            Self::Bytes => "bytes",
            Self::ByteArray => "bytearray",
            Self::Exception => "Exception",
            Self::Ellipsis => "Ellipsis",
            Self::Slice => "slice",
            Self::MemoryView => "memoryview",
        }
    }

    /// Get all built-in types
    #[must_use]
    pub fn all() -> Vec<Self> {
        vec![
            Self::Int,
            Self::Float,
            Self::Bool,
            Self::Str,
            Self::None,
            Self::Byte,
            Self::Short,
            Self::Long,
            Self::UInt,
            Self::UShort,
            Self::ULong,
            Self::SByte,
            Self::Char,
            Self::Double,
            Self::Decimal,
            Self::Complex,
            Self::List,
            Self::Dict,
            Self::Set,
            Self::Tuple,
            Self::Array,
            Self::FrozenSet,
            Self::Object,
            Self::Bytes,
            Self::ByteArray,
            Self::Exception,
            Self::Ellipsis,
            Self::Slice,
            Self::MemoryView,
        ]
    }

    /// Check if this type is a generic container type
    #[must_use]
    pub const fn is_generic(&self) -> bool {
        matches!(
            self,
            Self::List | Self::Dict | Self::Set | Self::Array | Self::FrozenSet | Self::Tuple
        )
    }

    /// Get the expected number of generic parameters for container types
    #[must_use]
    pub const fn generic_param_count(&self) -> Option<usize> {
        match self {
            Self::List | Self::Set | Self::Array | Self::FrozenSet => Some(1),
            Self::Dict => Some(2),
            _ => None, // Tuple and others have variable number of parameters
        }
    }
}

impl SemanticType {
    /// Create a built-in type
    #[must_use]
    pub const fn builtin(builtin: BuiltinType) -> Self {
        Self::Builtin(builtin)
    }

    /// Create a class type
    #[must_use]
    pub const fn class(name: String, module: Option<String>, generic_params: Vec<String>) -> Self {
        Self::Class {
            name,
            module,
            generic_params,
        }
    }

    /// Create a struct type
    #[must_use]
    pub const fn struct_type(
        name: String,
        module: Option<String>,
        generic_params: Vec<String>,
    ) -> Self {
        Self::Struct {
            name,
            module,
            generic_params,
        }
    }

    /// Create a protocol type
    #[must_use]
    pub const fn protocol(
        name: String,
        module: Option<String>,
        generic_params: Vec<String>,
    ) -> Self {
        Self::Protocol {
            name,
            module,
            generic_params,
        }
    }

    /// Create a generic instantiation
    #[must_use]
    pub fn generic(base: Self, args: Vec<Self>) -> Self {
        Self::Generic {
            base: Box::new(base),
            args,
        }
    }

    /// Create an optional type
    #[must_use]
    pub fn optional(inner: Self) -> Self {
        Self::Optional(Box::new(inner))
    }

    /// Create a function type
    pub fn function(params: Vec<Self>, return_type: Option<Self>) -> Self {
        Self::Function {
            params,
            return_type: return_type.map(Box::new),
        }
    }

    /// Get the display name of the type
    #[must_use]
    pub fn display_name(&self) -> String {
        match self {
            Self::Builtin(builtin) => builtin.as_str().to_string(),
            Self::Class { name, module, .. }
            | Self::Struct { name, module, .. }
            | Self::Protocol { name, module, .. } => module
                .as_ref()
                .map_or_else(|| name.clone(), |module| format!("{module}.{name}")),
            Self::Generic { base, args } => {
                let args_str = args
                    .iter()
                    .map(Self::display_name)
                    .collect::<Vec<_>>()
                    .join(", ");
                format!("{}[{}]", base.display_name(), args_str)
            }
            Self::Optional(inner) => format!("{}?", inner.display_name()),
            Self::Function {
                params,
                return_type,
            } => {
                let params_str = params
                    .iter()
                    .map(Self::display_name)
                    .collect::<Vec<_>>()
                    .join(", ");
                return_type.as_ref().map_or_else(
                    || format!("({params_str})"),
                    |ret| format!("({}) -> {}", params_str, ret.display_name()),
                )
            }
            Self::Tuple(types) => {
                let types_str = types
                    .iter()
                    .map(Self::display_name)
                    .collect::<Vec<_>>()
                    .join(", ");
                format!("tuple[{types_str}]")
            }
            Self::Union(types) => types
                .iter()
                .map(Self::display_name)
                .collect::<Vec<_>>()
                .join(" | "),
            Self::Array(inner) => format!("array[{}]", inner.display_name()),
            Self::Unknown(name) => format!("<?{name}>"),
        }
    }

    /// Check if this type is assignable to another type
    #[must_use]
    pub fn is_assignable_to(&self, other: &Self) -> bool {
        // Basic implementation - can be expanded with more sophisticated type checking
        match (self, other) {
            // Exact match
            (a, b) if a == b => true,

            // None can be assigned to any optional type
            (Self::Builtin(BuiltinType::None), Self::Optional(_)) => true,

            // Optional types
            (Self::Optional(inner), other_type) => inner.is_assignable_to(other_type),
            (inner_type, Self::Optional(other)) => inner_type.is_assignable_to(other),

            // All types can be assigned to object
            (_, Self::Builtin(BuiltinType::Object)) => true,

            // Array to List compatibility - Array[T] is assignable to List[T]
            (Self::Array(array_element_type), Self::Generic { base, args })
                if matches!(base.as_ref(), Self::Builtin(BuiltinType::List)) && args.len() == 1 =>
            {
                array_element_type.is_assignable_to(&args[0])
            }

            // List to Array compatibility - List[T] is assignable to Array[T]
            (Self::Generic { base, args }, Self::Array(array_element_type))
                if matches!(base.as_ref(), Self::Builtin(BuiltinType::List)) && args.len() == 1 =>
            {
                args[0].is_assignable_to(array_element_type)
            }

            // Generic type compatibility (basic check)
            (
                Self::Generic {
                    base: base1,
                    args: args1,
                },
                Self::Generic {
                    base: base2,
                    args: args2,
                },
            ) => {
                base1.is_assignable_to(base2)
                    && args1.len() == args2.len()
                    && args1
                        .iter()
                        .zip(args2.iter())
                        .all(|(a1, a2)| a1.is_assignable_to(a2))
            }

            _ => false,
        }
    }
}

/// Provides built-in types for the symbol table
#[must_use]
pub fn create_builtin_types() -> HashMap<String, SemanticType> {
    let mut types = HashMap::new();

    for builtin in BuiltinType::all() {
        let name = builtin.as_str();
        types.insert(name.to_string(), SemanticType::builtin(builtin.clone()));

        // Also add capitalized versions for common generic types
        match builtin {
            BuiltinType::List => {
                types.insert("List".to_string(), SemanticType::builtin(builtin));
            }
            BuiltinType::Dict => {
                types.insert("Dict".to_string(), SemanticType::builtin(builtin));
            }
            BuiltinType::Set => {
                types.insert("Set".to_string(), SemanticType::builtin(builtin));
            }
            BuiltinType::Tuple => {
                types.insert("Tuple".to_string(), SemanticType::builtin(builtin));
            }
            _ => {}
        }
    }

    types
}

/// Create builtin function definitions
#[must_use]
pub fn create_builtin_functions() -> Vec<(String, SemanticType)> {
    vec![
        // Collection functions
        (
            "len".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Int))),
            },
        ),
        // Conversion functions
        (
            "str".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Str))),
            },
        ),
        (
            "int".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Int))),
            },
        ),
        (
            "float".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Float))),
            },
        ),
        (
            "bool".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Bool))),
            },
        ),
        // IO functions
        (
            "print".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // at least 1 argument
                return_type: None,                                      // void return
            },
        ),
        // Math functions
        (
            "abs".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Union(vec![
                    SemanticType::Builtin(BuiltinType::Int),
                    SemanticType::Builtin(BuiltinType::Float),
                ])], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Union(vec![
                    SemanticType::Builtin(BuiltinType::Int),
                    SemanticType::Builtin(BuiltinType::Float),
                ]))),
            },
        ),
        (
            "max".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // at least 1 argument
                return_type: Some(Box::new(SemanticType::Unknown("any".to_string()))),
            },
        ),
        (
            "min".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // at least 1 argument
                return_type: Some(Box::new(SemanticType::Unknown("any".to_string()))),
            },
        ),
        (
            "sum".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument (iterable)
                return_type: Some(Box::new(SemanticType::Union(vec![
                    SemanticType::Builtin(BuiltinType::Int),
                    SemanticType::Builtin(BuiltinType::Float),
                ]))),
            },
        ),
        // Iterator functions
        (
            "iter".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Unknown("iterator".to_string()))),
            },
        ),
        (
            "next".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("iterator".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Unknown("any".to_string()))),
            },
        ),
        // Other utility functions
        (
            "id".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Int))),
            },
        ),
        (
            "repr".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Str))),
            },
        ),
        (
            "round".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Builtin(BuiltinType::Float)], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Int))),
            },
        ),
        (
            "sorted".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Unknown("list".to_string()))),
            },
        ),
        (
            "reversed".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Unknown("iterator".to_string()))),
            },
        ),
        // Numeric functions
        (
            "pow".to_string(),
            SemanticType::Function {
                params: vec![
                    SemanticType::Union(vec![
                        SemanticType::Builtin(BuiltinType::Int),
                        SemanticType::Builtin(BuiltinType::Float),
                    ]),
                    SemanticType::Union(vec![
                        SemanticType::Builtin(BuiltinType::Int),
                        SemanticType::Builtin(BuiltinType::Float),
                    ]),
                ], // exactly 2 arguments
                return_type: Some(Box::new(SemanticType::Union(vec![
                    SemanticType::Builtin(BuiltinType::Int),
                    SemanticType::Builtin(BuiltinType::Float),
                ]))),
            },
        ),
        (
            "oct".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Builtin(BuiltinType::Int)], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Str))),
            },
        ),
        (
            "ord".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Builtin(BuiltinType::Str)], // exactly 1 argument
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Int))),
            },
        ),
    ]
}

/// Creates built-in method definitions for built-in types
#[must_use]
pub fn create_builtin_methods()
-> std::collections::HashMap<String, std::collections::HashMap<String, SemanticType>> {
    let mut methods = std::collections::HashMap::new();

    // String methods
    let mut str_methods = std::collections::HashMap::new();
    str_methods.insert(
        "upper".to_string(),
        SemanticType::Function {
            params: vec![], // no arguments (self is implicit)
            return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Str))),
        },
    );
    str_methods.insert(
        "lower".to_string(),
        SemanticType::Function {
            params: vec![], // no arguments
            return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Str))),
        },
    );
    str_methods.insert(
        "split".to_string(),
        SemanticType::Function {
            params: vec![], // no arguments (separator is optional)
            return_type: Some(Box::new(SemanticType::Generic {
                base: Box::new(SemanticType::Builtin(BuiltinType::List)),
                args: vec![SemanticType::Builtin(BuiltinType::Str)],
            })),
        },
    );
    str_methods.insert(
        "strip".to_string(),
        SemanticType::Function {
            params: vec![], // no arguments
            return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Str))),
        },
    );
    methods.insert("str".to_string(), str_methods);

    // List methods
    let mut list_methods = std::collections::HashMap::new();
    list_methods.insert(
        "append".to_string(),
        SemanticType::Function {
            params: vec![SemanticType::Unknown("item".to_string())], // 1 argument (item to append)
            return_type: None,                                       // void return
        },
    );
    list_methods.insert(
        "pop".to_string(),
        SemanticType::Function {
            params: vec![], // no arguments (index is optional)
            return_type: Some(Box::new(SemanticType::Unknown("item".to_string()))), // returns item type
        },
    );
    list_methods.insert(
        "extend".to_string(),
        SemanticType::Function {
            params: vec![SemanticType::Unknown("iterable".to_string())], // 1 argument
            return_type: None,                                           // void return
        },
    );
    methods.insert("List".to_string(), list_methods);

    // Dict methods
    let mut dict_methods = std::collections::HashMap::new();
    dict_methods.insert(
        "keys".to_string(),
        SemanticType::Function {
            params: vec![], // no arguments
            return_type: Some(Box::new(SemanticType::Unknown("dict_keys".to_string()))),
        },
    );
    dict_methods.insert(
        "values".to_string(),
        SemanticType::Function {
            params: vec![], // no arguments
            return_type: Some(Box::new(SemanticType::Unknown("dict_values".to_string()))),
        },
    );
    dict_methods.insert(
        "items".to_string(),
        SemanticType::Function {
            params: vec![], // no arguments
            return_type: Some(Box::new(SemanticType::Unknown("dict_items".to_string()))),
        },
    );
    methods.insert("Dict".to_string(), dict_methods);

    methods
}
