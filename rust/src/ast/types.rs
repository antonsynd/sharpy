use crate::ast::node::{Node, NodeSource};

/// Type system components for the AST
#[derive(Debug, Clone, PartialEq)]
pub struct TypeComponent {
    pub name: String,
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

#[derive(Debug, Clone, PartialEq)]
pub struct Type {
    pub components: Vec<TypeComponent>,
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

#[derive(Debug, Clone, PartialEq)]
pub struct TypeParameter {
    pub name: String,
    pub default_type: Option<Type>,
    pub constraint: Option<Box<Node>>,
    pub resolved_type: Option<Type>,
    pub source: Option<NodeSource>,
}

impl TypeParameter {
    #[must_use]
    pub const fn new(name: String) -> Self {
        Self {
            name,
            default_type: None,
            constraint: None,
            resolved_type: None,
            source: None,
        }
    }

    #[must_use]
    pub const fn with_default(name: String, default_type: Type) -> Self {
        Self {
            name,
            default_type: Some(default_type),
            constraint: None,
            resolved_type: None,
            source: None,
        }
    }

    #[must_use]
    pub const fn with_constraint(name: String, constraint: Box<Node>) -> Self {
        Self {
            name,
            default_type: None,
            constraint: Some(constraint),
            resolved_type: None,
            source: None,
        }
    }
}

/// Parameter specification for functions
#[derive(Debug, Clone, PartialEq)]
pub struct ParameterSpecification {
    pub name: String,
    pub type_: Option<Type>,
    pub default: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

impl ParameterSpecification {
    #[must_use]
    pub const fn new(name: String) -> Self {
        Self {
            name,
            type_: None,
            default: None,
            source: None,
        }
    }

    #[must_use]
    pub const fn with_type(name: String, type_: Type) -> Self {
        Self {
            name,
            type_: Some(type_),
            default: None,
            source: None,
        }
    }

    #[must_use]
    pub const fn with_default(name: String, default: Box<Node>) -> Self {
        Self {
            name,
            type_: None,
            default: Some(default),
            source: None,
        }
    }

    #[must_use]
    pub const fn with_type_and_default(name: String, type_: Type, default: Box<Node>) -> Self {
        Self {
            name,
            type_: Some(type_),
            default: Some(default),
            source: None,
        }
    }
}
