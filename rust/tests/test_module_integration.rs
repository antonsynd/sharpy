use sharpy_compiler_toolchain::{Parser, SharpyLexer, Node};

/// Test the complete pipeline from Sharpy source code to Module AST
#[test]
fn test_complete_module_pipeline() {
    let sharpy_code = r#"
import math
from typing import List

MY_CONSTANT: int = 42

class Calculator:
    property result: int = 0

    def add(self, x: int, y: int) -> int:
        return x + y

@static
def utility_function() -> bool:
    return True

struct Point:
    x: int = 0
    y: int = 0

protocol Drawable:
    def draw(self):
        pass
"#;

    // Step 1: Tokenize
    let mut lexer = SharpyLexer::new(sharpy_code);
    let tokens = lexer.tokenize_all().expect("Tokenization should succeed");

    println!("Tokenized {} tokens", tokens.len());

    // Step 2: Parse to Module AST
    let mut parser = Parser::new(tokens);
    let module_ast = parser.parse_module().expect("Parsing should succeed");

    // Step 3: Verify the structure
    match module_ast {
        Node::Module(module) => {
            println!("Successfully parsed module with {} top-level statements", module.body.len());

            // Verify we have the expected statements
            // Should have: 2 imports, 1 constant, 1 class, 1 decorated function, 1 struct, 1 protocol
            assert_eq!(module.body.len(), 7, "Module should have 7 top-level statements");

            // Check each statement type
            let mut import_count = 0;
            let mut assign_count = 0;
            let mut class_count = 0;
            let mut function_count = 0;
            let mut struct_count = 0;
            let mut protocol_count = 0;

            for statement in &module.body {
                match statement {
                    Node::Import(_) | Node::ImportFrom(_) => import_count += 1,
                    Node::Assign(_) => assign_count += 1,
                    Node::ClassDef(_) => class_count += 1,
                    Node::FunctionDef(_) => function_count += 1,
                    Node::StructDef(_) => struct_count += 1,
                    Node::ProtocolDef(_) => protocol_count += 1,
                    _ => println!("Unexpected statement type: {:?}", statement),
                }
            }

            assert_eq!(import_count, 2, "Should have 2 import statements");
            assert_eq!(assign_count, 1, "Should have 1 assignment (constant)");
            assert_eq!(class_count, 1, "Should have 1 class definition");
            assert_eq!(function_count, 1, "Should have 1 function definition");
            assert_eq!(struct_count, 1, "Should have 1 struct definition");
            assert_eq!(protocol_count, 1, "Should have 1 protocol definition");

            println!("✅ Module parsing pipeline test passed!");
        }
        _ => panic!("Expected Module node, got {:?}", module_ast),
    }
}

/// Test parsing of previously restricted constructs (now allowed at parse time)
#[test]
fn test_module_parsing_flexibility() {
    let mixed_sharpy_code = r#"
import math

# Control flow statements are now parsed successfully
# Semantic analysis will validate module-level appropriateness
if True:
    print("Now allowed at parse time")

for i in range(3):
    print("Loop iteration " + str(i))

# Constants still work as before
MY_CONSTANT: int = 42
"#;

    let mut lexer = SharpyLexer::new(mixed_sharpy_code);
    let tokens = lexer.tokenize_all().expect("Tokenization should succeed");

    let mut parser = Parser::new(tokens);
    let result = parser.parse_module();

    match result {
        Ok(module_ast) => {
            println!("✅ Successfully parsed flexible module with control flow");
            if let sharpy_compiler_toolchain::Node::Module(module) = module_ast {
                // Should have: 1 import, 1 if, 1 for, 1 assignment
                assert_eq!(module.body.len(), 4, "Should have 4 statements");
            }
        }
        Err(error) => panic!("Should successfully parse mixed statements, got error: {}", error),
    }
}/// Test that we can handle empty modules
#[test]
fn test_empty_module_parsing() {
    let empty_code = "";

    let mut lexer = SharpyLexer::new(empty_code);
    let tokens = lexer.tokenize_all().expect("Tokenization should succeed");

    let mut parser = Parser::new(tokens);
    let module_ast = parser.parse_module().expect("Should parse empty module");

    match module_ast {
        Node::Module(module) => {
            assert!(module.body.is_empty(), "Empty module should have no statements");
            println!("✅ Empty module parsing test passed!");
        }
        _ => panic!("Expected Module node"),
    }
}

/// Test module parsing with only comments and whitespace
#[test]
fn test_comments_only_module() {
    let comments_code = r#"
# This is just a comment
# Another comment

# Yet another comment
"#;

    let mut lexer = SharpyLexer::new(comments_code);
    let tokens = lexer.tokenize_all().expect("Tokenization should succeed");

    let mut parser = Parser::new(tokens);
    let module_ast = parser.parse_module().expect("Should parse comments-only module");

    match module_ast {
        Node::Module(module) => {
            assert!(module.body.is_empty(), "Comments-only module should have no statements");
            println!("✅ Comments-only module parsing test passed!");
        }
        _ => panic!("Expected Module node"),
    }
}
