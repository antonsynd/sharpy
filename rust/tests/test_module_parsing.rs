use sharpy_compiler_toolchain::{Parser, SharpyLexer};

#[test]
fn test_empty_module() {
    let input = "";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module().expect("Failed to parse empty module");

    match result {
        sharpy_compiler_toolchain::Node::Module(module) => {
            assert!(module.body.is_empty(), "Empty module should have no body");
        }
        _ => panic!("Expected Module node"),
    }
}

#[test]
fn test_simple_import_module() {
    let input = r#"
import math
from collections import defaultdict
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module().expect("Failed to parse import module");

    match result {
        sharpy_compiler_toolchain::Node::Module(module) => {
            assert_eq!(module.body.len(), 2, "Module should have 2 import statements");
        }
        _ => panic!("Expected Module node"),
    }
}

#[test]
fn test_class_definition_module() {
    let input = r#"
class MyClass:
    pass

struct MyStruct:
    pass

protocol MyProtocol:
    pass
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module().expect("Failed to parse class module");

    match result {
        sharpy_compiler_toolchain::Node::Module(module) => {
            assert_eq!(module.body.len(), 3, "Module should have 3 type definitions");
        }
        _ => panic!("Expected Module node"),
    }
}

#[test]
fn test_function_definition_module() {
    let input = r#"
def my_function(x: int) -> str:
    return str(x)

def another_function():
    pass
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module().expect("Failed to parse function module");

    match result {
        sharpy_compiler_toolchain::Node::Module(module) => {
            assert_eq!(module.body.len(), 2, "Module should have 2 function definitions");
        }
        _ => panic!("Expected Module node"),
    }
}

#[test]
fn test_module_level_constants() {
    let input = r#"
MY_CONSTANT: int = 42
global_var: str = "hello"
another_var: float = 3.14
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module().expect("Failed to parse constants module");

    match result {
        sharpy_compiler_toolchain::Node::Module(module) => {
            assert_eq!(module.body.len(), 3, "Module should have 3 constant assignments");
        }
        _ => panic!("Expected Module node"),
    }
}

#[test]
fn test_decorated_function_module() {
    let input = r#"
@static
def utility_function() -> bool:
    return True

@override
def special_method(self, x: int):
    pass
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module().expect("Failed to parse decorated function module");

    match result {
        sharpy_compiler_toolchain::Node::Module(module) => {
            assert_eq!(module.body.len(), 2, "Module should have 2 decorated functions");
        }
        _ => panic!("Expected Module node"),
    }
}

#[test]
fn test_comprehensive_module() {
    let input = r#"
# Complete Sharpy module example
import math
from collections import defaultdict, OrderedDict

# Module constants
PI: float = 3.14159
DEBUG_MODE: bool = True

class Calculator:
    property result: int = 0

    def add(self, x: int, y: int) -> int:
        self.result = x + y
        return self.result

    @static
    def create_default() -> Calculator:
        return Calculator()

struct Point:
    x: int = 0
    y: int = 0

protocol Drawable:
    def draw(self):
        pass

@static
def main():
    calc = Calculator.create_default()
    result = calc.add(5, 3)
    print("Result: " + str(result))
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module().expect("Failed to parse comprehensive module");

    match result {
        sharpy_compiler_toolchain::Node::Module(module) => {
            // Should have: 2 imports, 2 constants, 1 class, 1 struct, 1 protocol, 1 decorated function
            assert_eq!(module.body.len(), 8, "Module should have 8 top-level statements");
        }
        _ => panic!("Expected Module node"),
    }
}

#[test]
fn test_control_flow_in_module() {
    let input = r#"
# Control flow statements are now allowed at parse time
# Semantic analysis will handle module-level restrictions
if True:
    print("This is now allowed at parse time")
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module();

    // Should now parse successfully - semantic analysis will validate
    assert!(result.is_ok(), "Should successfully parse control flow statements");

    if let Ok(sharpy_compiler_toolchain::Node::Module(module)) = result {
        assert_eq!(module.body.len(), 1, "Should have 1 if statement");
    }
}

#[test]
fn test_loops_in_module() {
    let input = r#"
# Loops are now allowed at parse time
# Semantic analysis will handle module-level restrictions
for i in range(10):
    print(i)
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module();

    // Should now parse successfully - semantic analysis will validate
    assert!(result.is_ok(), "Should successfully parse loop statements");

    if let Ok(sharpy_compiler_toolchain::Node::Module(module)) = result {
        assert_eq!(module.body.len(), 1, "Should have 1 for statement");
    }
}

#[test]
fn test_mixed_valid_module_statements() {
    let input = r#"
from typing import List, Dict
import os

# Constants
MAX_SIZE: int = 1000
DEFAULT_NAME: str = "unnamed"

# Function
def helper_function(data: List[int]) -> int:
    return sum(data)

# Class with decorated methods
class DataProcessor:
    _cache: Dict[str, int] = {}

    @static
    def clear_cache():
        DataProcessor._cache.clear()

    def process(self, items: List[int]) -> int:
        return helper_function(items)

# Struct
struct Config:
    debug: bool = False
    max_retries: int = 3

# Protocol
protocol Processable:
    def process(self, data: List[int]) -> int:
        pass
"#;
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module().expect("Failed to parse mixed module");

    match result {
        sharpy_compiler_toolchain::Node::Module(module) => {
            // Should have: 2 imports, 2 constants, 1 function, 1 class, 1 struct, 1 protocol
            assert_eq!(module.body.len(), 8, "Module should have 8 top-level statements");
        }
        _ => panic!("Expected Module node"),
    }
}
