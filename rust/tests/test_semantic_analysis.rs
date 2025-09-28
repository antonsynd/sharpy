use sharpy_compiler_toolchain::{Parser, SemanticAnalyzer, SharpyLexer, SymbolKind};

#[test]
fn test_basic_symbol_table_creation() {
    let analyzer = SemanticAnalyzer::new();

    // Check that built-in types are available
    let symbol_table = analyzer.get_symbol_table();

    assert!(symbol_table.lookup_type("int").is_some());
    assert!(symbol_table.lookup_type("str").is_some());
    assert!(symbol_table.lookup_type("bool").is_some());
    assert!(symbol_table.lookup_type("float").is_some());
    assert!(symbol_table.lookup_type("list").is_some());
    assert!(symbol_table.lookup_type("dict").is_some());
    assert!(symbol_table.lookup_type("set").is_some());

    // Check that non-existent types return None
    assert!(symbol_table.lookup_type("NonExistentType").is_none());
}

#[test]
fn test_module_level_function_analysis() {
    let code = r#"
def hello_world() -> str:
    return "Hello, World!"

def add_numbers(x: int, y: int) -> int:
    return x + y
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    analyzer
        .analyze_module(&ast, Some("test_module".to_string()))
        .expect("Analysis should succeed");

    let symbol_table = analyzer.get_symbol_table();

    // Check that functions were added to symbol table
    let hello_symbol = symbol_table.lookup_symbol("hello_world");
    assert!(hello_symbol.is_some());
    let hello_symbol = hello_symbol.unwrap();
    assert_eq!(hello_symbol.kind, SymbolKind::Function);

    let add_symbol = symbol_table.lookup_symbol("add_numbers");
    assert!(add_symbol.is_some());
    let add_symbol = add_symbol.unwrap();
    assert_eq!(add_symbol.kind, SymbolKind::Function);

    // Debug print the symbol table
    symbol_table.debug_print();
}

#[test]
fn test_class_analysis() {
    let code = r#"
class Person:
    def __init__(self, name: str):
        pass

    def get_name(self) -> str:
        return "test"

    def _protected_method(self):
        pass

    def __private_method(self) -> int:
        return 42
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    analyzer
        .analyze_module(&ast, Some("test_module".to_string()))
        .expect("Analysis should succeed");

    let symbol_table = analyzer.get_symbol_table();

    // Check that class was added
    let person_symbol = symbol_table.lookup_symbol("Person");
    assert!(person_symbol.is_some());
    let person_symbol = person_symbol.unwrap();
    assert_eq!(person_symbol.kind, SymbolKind::Class);

    // Debug print the symbol table
    symbol_table.debug_print();
}

#[test]
fn test_protocol_with_abstract_methods() {
    let code = r#"
protocol Encodable:
    def encode(self) -> str: ...

    def decode(self, data: str):
        pass

    def is_valid(self) -> bool: ...
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    analyzer
        .analyze_module(&ast, Some("test_module".to_string()))
        .expect("Analysis should succeed");

    let symbol_table = analyzer.get_symbol_table();

    // Check that protocol was added
    let encodable_symbol = symbol_table.lookup_symbol("Encodable");
    assert!(encodable_symbol.is_some());
    let encodable_symbol = encodable_symbol.unwrap();
    assert_eq!(encodable_symbol.kind, SymbolKind::Protocol);

    // Debug print the symbol table
    symbol_table.debug_print();
}

#[test]
fn test_struct_analysis() {
    let code = r#"
struct Point:
    def distance(self, other: Point) -> float:
        return 0.0
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    analyzer
        .analyze_module(&ast, Some("test_module".to_string()))
        .expect("Analysis should succeed");

    let symbol_table = analyzer.get_symbol_table();

    // Check that struct was added
    let point_symbol = symbol_table.lookup_symbol("Point");
    assert!(point_symbol.is_some());
    let point_symbol = point_symbol.unwrap();
    assert_eq!(point_symbol.kind, SymbolKind::Struct);

    // Debug print the symbol table
    symbol_table.debug_print();
}

#[test]
fn test_property_analysis() {
    let code = r#"
class MyClass:
    property name: str = "default"

    property _value(self) -> int:
        return 42
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    analyzer
        .analyze_module(&ast, Some("test_module".to_string()))
        .expect("Analysis should succeed");

    let symbol_table = analyzer.get_symbol_table();

    // Check that class and properties were added
    let class_symbol = symbol_table.lookup_symbol("MyClass");
    assert!(class_symbol.is_some());

    // Debug print to see the structure
    symbol_table.debug_print();
}

#[test]
fn test_import_analysis() {
    let code = r#"
import sys
from collections import defaultdict, Counter
import json as js
from typing import List, Dict
"#;

    let mut lexer = SharpyLexer::new(code);
    let tokens = lexer.tokenize_all().expect("Lexing should succeed");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Parsing should succeed");

    let mut analyzer = SemanticAnalyzer::new();
    analyzer
        .analyze_module(&ast, Some("test_module".to_string()))
        .expect("Analysis should succeed");

    let symbol_table = analyzer.get_symbol_table();

    // Check that imports were registered
    assert!(symbol_table.lookup_symbol("sys").is_some());
    assert!(symbol_table.lookup_symbol("defaultdict").is_some());
    assert!(symbol_table.lookup_symbol("Counter").is_some());
    assert!(symbol_table.lookup_symbol("js").is_some()); // alias
    assert!(symbol_table.lookup_symbol("List").is_some());
    assert!(symbol_table.lookup_symbol("Dict").is_some());

    // Debug print
    symbol_table.debug_print();
}
