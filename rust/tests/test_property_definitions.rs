use sharpy_compiler_toolchain::*;

#[test]
fn test_simple_auto_property() {
    let input = "property value: int = 5";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "value");
        assert_eq!(prop.access_modifier, None);
        assert!(prop.type_.is_some());
        assert!(prop.default.is_some());
        assert!(prop.getter.is_none());
        assert!(prop.setter.is_none());
        assert!(!prop.is_get_only);
        assert!(!prop.is_set_only);
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}

#[test]
fn test_protected_auto_property() {
    let input = "property _value: int = 5";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "value");
        assert_eq!(prop.access_modifier, Some("protected".to_string()));
        assert!(prop.type_.is_some());
        assert!(prop.default.is_some());
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}

#[test]
fn test_get_only_property() {
    let input = "get property length: int";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "length");
        assert_eq!(prop.access_modifier, None);
        assert!(prop.type_.is_some());
        assert!(prop.default.is_none());
        assert!(prop.is_get_only);
        assert!(!prop.is_set_only);
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}

#[test]
fn test_set_only_property() {
    let input = "set property _size: int = 0";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "size");
        assert_eq!(prop.access_modifier, Some("protected".to_string()));
        assert!(prop.type_.is_some());
        assert!(prop.default.is_some());
        assert!(!prop.is_get_only);
        assert!(prop.is_set_only);
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}

#[test]
fn test_property_without_type() {
    let input = "property name = \"default\"";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "name");
        assert!(prop.type_.is_none());
        assert!(prop.default.is_some());
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}

#[test]
fn test_property_without_default() {
    let input = "property age: int";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "age");
        assert!(prop.type_.is_some());
        assert!(prop.default.is_none());
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}

#[test]
fn test_explicit_getter_property() {
    let input = r#"
property _dimensions(self) -> int:
    pass
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "dimensions");
        assert_eq!(prop.access_modifier, Some("protected".to_string()));
        assert!(prop.type_.is_some());
        assert!(prop.default.is_none());
        assert!(prop.getter.is_some());
        assert!(prop.setter.is_none());
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}

#[test]
fn test_explicit_setter_property() {
    let input = r#"
property __dimensions(self, v: int):
    pass
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "dimensions");
        assert_eq!(prop.access_modifier, Some("private".to_string()));
        assert!(prop.type_.is_none()); // No return type for setter
        assert!(prop.default.is_none());
        assert!(prop.getter.is_none());
        assert!(prop.setter.is_some());
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}

#[test]
fn test_private_property() {
    let input = "property __internal_value: int = 42";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "internal_value");
        assert_eq!(prop.access_modifier, Some("private".to_string()));
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}

#[test]
fn test_internal_property() {
    let input = "property $shared_value: int = 0";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "shared_value");
        assert_eq!(prop.access_modifier, Some("internal".to_string()));
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}

#[test]
fn test_complex_property_type() {
    let input = "property items: List[Dict[str, int]] = []";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::PropertyDef(prop) = &statements[0] {
        assert_eq!(prop.name, "items");
        assert!(prop.type_.is_some());
        assert!(prop.default.is_some());
    } else {
        panic!("Expected PropertyDef node, got {:?}", statements[0]);
    }
}
