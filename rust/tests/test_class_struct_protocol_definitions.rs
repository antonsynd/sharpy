use sharpy_compiler_toolchain::ast::node::Node;
use sharpy_compiler_toolchain::lexer::SharpyLexer;
use sharpy_compiler_toolchain::parser::Parser;

/// Test basic class definition
#[test]
fn test_basic_class_definition() {
    let input = r#"
class Person:
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::ClassDef(class) = &nodes[0] {
        assert_eq!(class.name, "Person");
        assert_eq!(class.access_modifier, None); // Public
        assert!(class.type_params.is_empty());
        assert!(class.bases.is_empty());
        assert_eq!(class.body.len(), 1); // pass statement
    } else {
        panic!("Expected ClassDef node");
    }
}

/// Test class with access modifier
#[test]
fn test_protected_class_definition() {
    let input = r#"
class _ProtectedClass:
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::ClassDef(class) = &nodes[0] {
        assert_eq!(class.name, "ProtectedClass");
        assert_eq!(class.access_modifier, Some("protected".to_string()));
        assert!(class.type_params.is_empty());
        assert!(class.bases.is_empty());
    } else {
        panic!("Expected ClassDef node");
    }
}

/// Test class with generic parameters
#[test]
fn test_generic_class_definition() {
    let input = r#"
class Container[T]:
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::ClassDef(class) = &nodes[0] {
        assert_eq!(class.name, "Container");
        assert_eq!(class.access_modifier, None);
        assert_eq!(class.type_params, vec!["T"]);
        assert!(class.bases.is_empty());
    } else {
        panic!("Expected ClassDef node");
    }
}

/// Test class with multiple generic parameters
#[test]
fn test_multi_generic_class_definition() {
    let input = r#"
class Dict[K, V]:
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::ClassDef(class) = &nodes[0] {
        assert_eq!(class.name, "Dict");
        assert_eq!(class.type_params, vec!["K", "V"]);
    } else {
        panic!("Expected ClassDef node");
    }
}

/// Test class with inheritance
#[test]
fn test_class_with_inheritance() {
    let input = r#"
class Child(Parent):
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::ClassDef(class) = &nodes[0] {
        assert_eq!(class.name, "Child");
        assert_eq!(class.bases.len(), 1);
        // Base types are parsed as type expressions, so they should be TypeName nodes
        if let Node::TypeName(base_name) = &class.bases[0] {
            assert_eq!(base_name.name, "Parent");
        } else {
            panic!(
                "Expected TypeName node for base class, got: {:?}",
                &class.bases[0]
            );
        }
    } else {
        panic!("Expected ClassDef node");
    }
}

/// Test class with multiple bases (inheritance and protocols)
#[test]
fn test_class_with_multiple_bases() {
    let input = r#"
class Child(Parent, Protocol1, Protocol2):
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::ClassDef(class) = &nodes[0] {
        assert_eq!(class.name, "Child");
        assert_eq!(class.bases.len(), 3);
    } else {
        panic!("Expected ClassDef node");
    }
}

/// Test basic struct definition
#[test]
fn test_basic_struct_definition() {
    let input = r#"
struct Point:
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::StructDef(struct_def) = &nodes[0] {
        assert_eq!(struct_def.name, "Point");
        assert_eq!(struct_def.access_modifier, None);
        assert!(struct_def.type_params.is_empty());
        assert!(struct_def.bases.is_empty());
    } else {
        panic!("Expected StructDef node");
    }
}

/// Test struct with protocols
#[test]
fn test_struct_with_protocols() {
    let input = r#"
struct Point(Comparable, Serializable):
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::StructDef(struct_def) = &nodes[0] {
        assert_eq!(struct_def.name, "Point");
        assert_eq!(struct_def.bases.len(), 2);
    } else {
        panic!("Expected StructDef node");
    }
}

/// Test basic protocol definition
#[test]
fn test_basic_protocol_definition() {
    let input = r#"
protocol Drawable:
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::ProtocolDef(protocol) = &nodes[0] {
        assert_eq!(protocol.name, "Drawable");
        assert_eq!(protocol.access_modifier, None);
        assert!(protocol.type_params.is_empty());
        assert!(protocol.bases.is_empty());
    } else {
        panic!("Expected ProtocolDef node");
    }
}

/// Test protocol with inheritance
#[test]
fn test_protocol_with_inheritance() {
    let input = r#"
protocol AdvancedDrawable(Drawable, Serializable):
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::ProtocolDef(protocol) = &nodes[0] {
        assert_eq!(protocol.name, "AdvancedDrawable");
        assert_eq!(protocol.bases.len(), 2);
    } else {
        panic!("Expected ProtocolDef node");
    }
}

/// Test complex class with everything
#[test]
fn test_complex_class_definition() {
    let input = r#"
class __PrivateContainer[T, U](BaseClass, Protocol1):
    def method(self):
        pass
    
    x: int = 5
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    if let Err(ref err) = result {
        println!("Parse error: {:?}", err);
    }
    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::ClassDef(class) = &nodes[0] {
        assert_eq!(class.name, "PrivateContainer");
        assert_eq!(class.access_modifier, Some("private".to_string()));
        assert_eq!(class.type_params, vec!["T", "U"]);
        assert_eq!(class.bases.len(), 2);
        assert_eq!(class.body.len(), 2); // method and assignment
    } else {
        panic!("Expected ClassDef node");
    }
}
