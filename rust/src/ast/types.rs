use crate::ast::node::{Node, NodeSource};

/// A type component (delimited by a period), e.g.
/// sharpy.collections.defaultdict[K, V>] has the components, "sharpy",
/// "collections", and "defaultdict[K, V]".
#[derive(Debug, Clone, PartialEq)]
pub struct TypeComponent {
    /// The base name of this component, e.g. "sharpy" or "defaultdict".
    pub name: String,

    /// The type parameters for this component, if any, e.g. K and V.
    pub parameters: Vec<TypeParameter>,

    pub source: Option<NodeSource>,
}

impl TypeComponent {
    #[must_use]
    pub const fn new(name: String) -> Self {
        Self {
            name,
            parameters: Vec::new(),
            source: None,
        }
    }

    #[must_use]
    pub const fn with_parameters(name: String, parameters: Vec<TypeParameter>) -> Self {
        Self {
            name,
            parameters,
            source: None,
        }
    }
}

/// A complete type, including all components.
#[derive(Debug, Clone, PartialEq)]
pub struct Type {
    /// The components of the type, including the modules, parent types, and
    /// the actual type itself.
    pub components: Vec<TypeComponent>,

    /// Whether or not the type is optional, e.g. T?. In Sharpy, this means a true optional,
    /// not a nullable type.
    pub optional: bool,

    pub source: Option<NodeSource>,
}

impl Type {
    #[must_use]
    pub const fn new(components: Vec<TypeComponent>) -> Self {
        Self {
            components,
            optional: false,
            source: None,
        }
    }

    #[must_use]
    pub fn simple(name: String) -> Self {
        Self {
            components: vec![TypeComponent::new(name)],
            optional: false,
            source: None,
        }
    }

    #[must_use]
    pub const fn optional(mut self) -> Self {
        self.optional = true;
        self
    }
}

/// A type parameter, which can be either a generic type parameter or a
/// concrete type parameter. This is for both as part of a type, class,
/// function, etc.
#[derive(Debug, Clone, PartialEq)]
pub enum TypeParameter {
    Generic(GenericTypeParameter),
    Concrete(ConcreteTypeParameter),
}

/// A type parameter that is part of a generic type.
#[derive(Debug, Clone, PartialEq)]
pub struct GenericTypeParameter {
    /// The placeholder's name, e.g. T.
    pub name: String,

    /// The default type for this parameter, if any, e.g. T = int.
    pub default_type: Option<Type>,

    /// Constraints on the type parameter, e.g. T : Protocol, or a where clause.
    pub constraint: Vec<Node>,

    pub source: Option<NodeSource>,
}

impl GenericTypeParameter {
    #[must_use]
    pub const fn new(name: String) -> Self {
        Self {
            name,
            default_type: None,
            constraint: vec![],
            source: None,
        }
    }

    #[must_use]
    pub const fn with_default(name: String, default_type: Type) -> Self {
        Self {
            name,
            default_type: Some(default_type),
            constraint: vec![],
            source: None,
        }
    }

    #[must_use]
    pub const fn with_constraint(name: String, constraint: Vec<Node>) -> Self {
        Self {
            name,
            default_type: None,
            constraint,
            source: None,
        }
    }
}

/// A concrete type inside a generic type slot, e.g. defaultdict[str]. This
/// should also be used for replacing a generic type parameter with a concrete
/// type during type resolution.
#[derive(Debug, Clone, PartialEq)]
pub struct ConcreteTypeParameter {
    /// The concrete type, e.g. int or str.
    pub type_: Type,

    pub source: Option<NodeSource>,
}

impl ConcreteTypeParameter {
    #[must_use]
    pub const fn new(type_: Type) -> Self {
        Self {
            type_,
            source: None,
        }
    }
}
