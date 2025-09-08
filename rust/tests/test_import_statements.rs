use sharpy_compiler_toolchain::*;

#[test]
fn test_simple_import() {
    let input = "import math";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::Import(import) = &statements[0] {
        assert_eq!(import.names.len(), 1);
        assert_eq!(import.names[0].name, "math");
        assert_eq!(import.names[0].as_name, None);
    } else {
        panic!("Expected Import node, got {:?}", statements[0]);
    }
}

#[test]
fn test_import_with_alias() {
    let input = "import math as m";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::Import(import) = &statements[0] {
        assert_eq!(import.names.len(), 1);
        assert_eq!(import.names[0].name, "math");
        assert_eq!(import.names[0].as_name, Some("m".to_string()));
    } else {
        panic!("Expected Import node, got {:?}", statements[0]);
    }
}

#[test]
fn test_multiple_imports() {
    let input = "import math, collections, os";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::Import(import) = &statements[0] {
        assert_eq!(import.names.len(), 3);
        assert_eq!(import.names[0].name, "math");
        assert_eq!(import.names[1].name, "collections");
        assert_eq!(import.names[2].name, "os");
        assert!(import.names.iter().all(|alias| alias.as_name.is_none()));
    } else {
        panic!("Expected Import node, got {:?}", statements[0]);
    }
}

#[test]
fn test_dotted_import() {
    let input = "import collections.abc";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::Import(import) = &statements[0] {
        assert_eq!(import.names.len(), 1);
        assert_eq!(import.names[0].name, "collections.abc");
        assert_eq!(import.names[0].as_name, None);
    } else {
        panic!("Expected Import node, got {:?}", statements[0]);
    }
}

#[test]
fn test_simple_from_import() {
    let input = "from math import sin";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::ImportFrom(import_from) = &statements[0] {
        assert_eq!(import_from.module, Some("math".to_string()));
        assert_eq!(import_from.names.len(), 1);
        assert_eq!(import_from.names[0].name, "sin");
        assert_eq!(import_from.names[0].as_name, None);
        assert_eq!(import_from.level, 0);
    } else {
        panic!("Expected ImportFrom node, got {:?}", statements[0]);
    }
}

#[test]
fn test_from_import_with_alias() {
    let input = "from math import sin as sine";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::ImportFrom(import_from) = &statements[0] {
        assert_eq!(import_from.module, Some("math".to_string()));
        assert_eq!(import_from.names.len(), 1);
        assert_eq!(import_from.names[0].name, "sin");
        assert_eq!(import_from.names[0].as_name, Some("sine".to_string()));
    } else {
        panic!("Expected ImportFrom node, got {:?}", statements[0]);
    }
}

#[test]
fn test_multiple_from_imports() {
    let input = "from math import sin, cos, tan";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::ImportFrom(import_from) = &statements[0] {
        assert_eq!(import_from.module, Some("math".to_string()));
        assert_eq!(import_from.names.len(), 3);
        assert_eq!(import_from.names[0].name, "sin");
        assert_eq!(import_from.names[1].name, "cos");
        assert_eq!(import_from.names[2].name, "tan");
        assert!(
            import_from
                .names
                .iter()
                .all(|alias| alias.as_name.is_none())
        );
    } else {
        panic!("Expected ImportFrom node, got {:?}", statements[0]);
    }
}

#[test]
fn test_star_import() {
    let input = "from math import *";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::ImportFrom(import_from) = &statements[0] {
        assert_eq!(import_from.module, Some("math".to_string()));
        assert_eq!(import_from.names.len(), 1);
        assert_eq!(import_from.names[0].name, "*");
        assert_eq!(import_from.names[0].as_name, None);
    } else {
        panic!("Expected ImportFrom node, got {:?}", statements[0]);
    }
}

#[test]
fn test_dotted_from_import() {
    let input = "from collections.abc import Mapping";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::ImportFrom(import_from) = &statements[0] {
        assert_eq!(import_from.module, Some("collections.abc".to_string()));
        assert_eq!(import_from.names.len(), 1);
        assert_eq!(import_from.names[0].name, "Mapping");
        assert_eq!(import_from.names[0].as_name, None);
    } else {
        panic!("Expected ImportFrom node, got {:?}", statements[0]);
    }
}

#[test]
fn test_complex_import_mix() {
    let input = "from sharpy.collections import List as SharpyList, Dict";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let statements = parser.parse().expect("Parsing should succeed");

    assert_eq!(statements.len(), 1);

    if let Node::ImportFrom(import_from) = &statements[0] {
        assert_eq!(import_from.module, Some("sharpy.collections".to_string()));
        assert_eq!(import_from.names.len(), 2);
        assert_eq!(import_from.names[0].name, "List");
        assert_eq!(import_from.names[0].as_name, Some("SharpyList".to_string()));
        assert_eq!(import_from.names[1].name, "Dict");
        assert_eq!(import_from.names[1].as_name, None);
    } else {
        panic!("Expected ImportFrom node, got {:?}", statements[0]);
    }
}
