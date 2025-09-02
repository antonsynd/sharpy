use crate::ast::node::{Node, NodeSource};

/// A simple type name, e.g. `int`, `str`, `MyClass`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TypeName {
    pub name: String,
    pub source: Option<NodeSource>,
}

impl TypeName {
    #[must_use]
    pub const fn new(name: String) -> Self {
        Self { name, source: None }
    }

    #[must_use]
    pub const fn with_source(name: String, source: NodeSource) -> Self {
        Self {
            name,
            source: Some(source),
        }
    }
}

/// A module-qualified type, e.g. `collections.defaultdict`, `sharpy.io.File`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct QualifiedType {
    /// The module path components, e.g. `["sharpy", "collections"] for sharpy.collections.defaultdict`
    pub module_path: Vec<String>,
    /// The final type name, e.g. "defaultdict"
    pub name: String,
    pub source: Option<NodeSource>,
}

impl QualifiedType {
    #[must_use]
    pub const fn new(module_path: Vec<String>, name: String) -> Self {
        Self {
            module_path,
            name,
            source: None,
        }
    }

    #[must_use]
    pub const fn with_source(module_path: Vec<String>, name: String, source: NodeSource) -> Self {
        Self {
            module_path,
            name,
            source: Some(source),
        }
    }
}

/// A generic/parameterized type, e.g. `list[int]`, `dict[str, int]`
#[derive(Debug, Clone, PartialEq)]
pub struct GenericType {
    /// The base type (can be `TypeName` or `QualifiedType`)
    pub base_type: Box<Node>,
    /// The type arguments
    pub type_args: Vec<Node>,
    pub source: Option<NodeSource>,
}

impl GenericType {
    #[must_use]
    pub const fn new(base_type: Box<Node>, type_args: Vec<Node>) -> Self {
        Self {
            base_type,
            type_args,
            source: None,
        }
    }

    #[must_use]
    pub const fn with_source(
        base_type: Box<Node>,
        type_args: Vec<Node>,
        source: NodeSource,
    ) -> Self {
        Self {
            base_type,
            type_args,
            source: Some(source),
        }
    }
}

/// An optional type, e.g. `int?`, `str?`
#[derive(Debug, Clone, PartialEq)]
pub struct OptionalType {
    /// The wrapped type
    pub inner_type: Box<Node>,
    pub source: Option<NodeSource>,
}

impl OptionalType {
    #[must_use]
    pub const fn new(inner_type: Box<Node>) -> Self {
        Self {
            inner_type,
            source: None,
        }
    }

    #[must_use]
    pub const fn with_source(inner_type: Box<Node>, source: NodeSource) -> Self {
        Self {
            inner_type,
            source: Some(source),
        }
    }
}

/// A union type, e.g. `int | str` (if supported)
#[derive(Debug, Clone, PartialEq)]
pub struct UnionType {
    /// The types in the union
    pub types: Vec<Node>,
    pub source: Option<NodeSource>,
}

impl UnionType {
    #[must_use]
    pub const fn new(types: Vec<Node>) -> Self {
        Self {
            types,
            source: None,
        }
    }

    #[must_use]
    pub const fn with_source(types: Vec<Node>, source: NodeSource) -> Self {
        Self {
            types,
            source: Some(source),
        }
    }
}

// Keep the old complex type system for semantic analysis / type resolution
// These will be used by the type checker, not the parser

/// A resolved type component used in semantic analysis
#[derive(Debug, Clone, PartialEq)]
pub struct ResolvedTypeComponent {
    /// The base name of this component, e.g. "sharpy" or "defaultdict".
    pub name: String,
    /// The resolved type parameters for this component, if any
    pub parameters: Vec<ResolvedTypeParameter>,
    pub source: Option<NodeSource>,
}

/// A complete resolved type used in semantic analysis
#[derive(Debug, Clone, PartialEq)]
pub struct ResolvedType {
    /// The components of the type, including the modules, parent types, and
    /// the actual type itself.
    pub components: Vec<ResolvedTypeComponent>,
    /// Whether or not the type is optional, e.g. T?. In Sharpy, this means a true optional,
    /// not a nullable type.
    pub optional: bool,
    pub source: Option<NodeSource>,
}

/// A resolved type parameter used in semantic analysis
#[derive(Debug, Clone, PartialEq)]
pub enum ResolvedTypeParameter {
    Generic(ResolvedGenericTypeParameter),
    Concrete(ResolvedConcreteTypeParameter),
}

/// A resolved generic type parameter used in semantic analysis
#[derive(Debug, Clone, PartialEq)]
pub struct ResolvedGenericTypeParameter {
    /// The placeholder's name, e.g. T.
    pub name: String,
    /// The default type for this parameter, if any, e.g. T = int.
    pub default_type: Option<ResolvedType>,
    /// Constraints on the type parameter, e.g. T : Protocol, or a where clause.
    pub constraint: Vec<ResolvedType>,
    pub source: Option<NodeSource>,
}

/// A resolved concrete type parameter used in semantic analysis
#[derive(Debug, Clone, PartialEq)]
pub struct ResolvedConcreteTypeParameter {
    /// The concrete type, e.g. int or str.
    pub type_: ResolvedType,
    pub source: Option<NodeSource>,
}

// Compatibility aliases for the old Type system (to be removed later)
pub type Type = ResolvedType;
pub type TypeComponent = ResolvedTypeComponent;
pub type TypeParameter = ResolvedTypeParameter;
pub type GenericTypeParameter = ResolvedGenericTypeParameter;
pub type ConcreteTypeParameter = ResolvedConcreteTypeParameter;
