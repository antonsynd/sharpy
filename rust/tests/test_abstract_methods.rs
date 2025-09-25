use sharpy_compiler_toolchain::{
    Parser, SharpyLexer,
    ast::node::{ConstantValue, Node},
};

#[test]
fn test_protocol_method_with_ellipsis() {
    let input = r#"
protocol Drawable:
    def draw(self): ...
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Failed to parse");

    assert!(!ast.is_empty());

    // Check that we have a protocol definition
    match &ast[0] {
        Node::ProtocolDef(protocol) => {
            assert_eq!(protocol.name, "Drawable");
            assert!(!protocol.body.is_empty());

            // Check that the method has an ellipsis body
            match &protocol.body[0] {
                Node::FunctionDef(func) => {
                    assert_eq!(func.name, "draw");
                    assert_eq!(func.body.len(), 1);

                    // The body should contain a single ellipsis constant
                    match &func.body[0] {
                        Node::Constant(constant) => {
                            assert_eq!(constant.value, ConstantValue::Ellipsis);
                        }
                        _ => panic!("Expected Constant node with ellipsis in function body"),
                    }
                }
                _ => panic!("Expected FunctionDef in protocol body"),
            }
        }
        _ => panic!("Expected ProtocolDef node"),
    }
}

#[test]
fn test_class_method_with_ellipsis() {
    let input = r#"
class AbstractBase:
    def abstract_method(self): ...

    def concrete_method(self):
        pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Failed to parse");

    assert!(!ast.is_empty());

    // Check that we have a class definition
    match &ast[0] {
        Node::ClassDef(class) => {
            assert_eq!(class.name, "AbstractBase");
            assert_eq!(class.body.len(), 2);

            // Check first method has ellipsis
            match &class.body[0] {
                Node::FunctionDef(func) => {
                    assert_eq!(func.name, "abstract_method");
                    assert_eq!(func.body.len(), 1);

                    match &func.body[0] {
                        Node::Constant(constant) => {
                            assert_eq!(constant.value, ConstantValue::Ellipsis);
                        }
                        _ => panic!("Expected Constant node with ellipsis in function body"),
                    }
                }
                _ => panic!("Expected FunctionDef in class body"),
            }

            // Check second method has pass statement
            match &class.body[1] {
                Node::FunctionDef(func) => {
                    assert_eq!(func.name, "concrete_method");
                    assert_eq!(func.body.len(), 1);

                    match &func.body[0] {
                        Node::Pass(_) => {
                            // This is expected for concrete methods
                        }
                        _ => panic!("Expected Pass node in concrete method body"),
                    }
                }
                _ => panic!("Expected FunctionDef in class body"),
            }
        }
        _ => panic!("Expected ClassDef node"),
    }
}

#[test]
fn test_function_with_return_type_and_ellipsis() {
    let input = r#"
protocol Calculator:
    def compute(self, x: int, y: int) -> int: ...
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().expect("Failed to tokenize");

    let mut parser = Parser::new(tokens);
    let ast = parser.parse().expect("Failed to parse");

    assert!(!ast.is_empty());

    // Check that we have a protocol definition
    match &ast[0] {
        Node::ProtocolDef(protocol) => {
            assert_eq!(protocol.name, "Calculator");
            assert!(!protocol.body.is_empty());

            // Check that the method has an ellipsis body and return type
            match &protocol.body[0] {
                Node::FunctionDef(func) => {
                    assert_eq!(func.name, "compute");
                    assert!(func.return_type.is_some());
                    assert_eq!(func.body.len(), 1);

                    // The body should contain a single ellipsis constant
                    match &func.body[0] {
                        Node::Constant(constant) => {
                            assert_eq!(constant.value, ConstantValue::Ellipsis);
                        }
                        _ => panic!("Expected Constant node with ellipsis in function body"),
                    }
                }
                _ => panic!("Expected FunctionDef in protocol body"),
            }
        }
        _ => panic!("Expected ProtocolDef node"),
    }
}
