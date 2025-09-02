#[cfg(test)]
mod tests {
    use sharpy_compiler_toolchain::Node;
    use sharpy_compiler_toolchain::Parser;
    use sharpy_compiler_toolchain::lexer::SharpyLexer;

    #[test]
    fn test_simple_type_annotation() {
        let source = "x: int = 5";
        let mut lexer = SharpyLexer::new(source);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);

        let result = parser.parse();
        assert!(result.is_ok());

        let ast = result.unwrap();
        assert_eq!(ast.len(), 1);

        // The parsed statement should be an assignment with a typed target
        match &ast[0] {
            Node::Assign(assign) => match &*assign.target {
                Node::TypedName(typed_name) => match &*typed_name.type_ {
                    Node::TypeName(type_name) => {
                        assert_eq!(type_name.name, "int");
                    }
                    _ => panic!("Expected TypeName, got {:?}", typed_name.type_),
                },
                _ => panic!("Expected TypedName, got {:?}", assign.target),
            },
            _ => panic!("Expected Assign, got {:?}", ast[0]),
        }
    }

    #[test]
    fn test_qualified_type_annotation() {
        let source = "config: app.Config = None";
        let mut lexer = SharpyLexer::new(source);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);

        let result = parser.parse();
        assert!(result.is_ok());

        let ast = result.unwrap();
        match &ast[0] {
            Node::Assign(assign) => match &*assign.target {
                Node::TypedName(typed_name) => match &*typed_name.type_ {
                    Node::QualifiedType(qualified_type) => {
                        assert_eq!(qualified_type.module_path, vec!["app"]);
                        assert_eq!(qualified_type.name, "Config");
                    }
                    _ => panic!("Expected QualifiedType, got {:?}", typed_name.type_),
                },
                _ => panic!("Expected TypedName"),
            },
            _ => panic!("Expected Assign"),
        }
    }

    #[test]
    fn test_generic_type_annotation() {
        let source = "items: List[str] = []";
        let mut lexer = SharpyLexer::new(source);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);

        let result = parser.parse();
        assert!(result.is_ok());

        let ast = result.unwrap();
        match &ast[0] {
            Node::Assign(assign) => match &*assign.target {
                Node::TypedName(typed_name) => match &*typed_name.type_ {
                    Node::GenericType(generic_type) => {
                        match &*generic_type.base_type {
                            Node::TypeName(base_name) => {
                                assert_eq!(base_name.name, "List");
                            }
                            _ => panic!("Expected TypeName as base type"),
                        }
                        assert_eq!(generic_type.type_args.len(), 1);
                        match &generic_type.type_args[0] {
                            Node::TypeName(type_name) => {
                                assert_eq!(type_name.name, "str");
                            }
                            _ => panic!("Expected TypeName in generic argument"),
                        }
                    }
                    _ => panic!("Expected GenericType, got {:?}", typed_name.type_),
                },
                _ => panic!("Expected TypedName"),
            },
            _ => panic!("Expected Assign"),
        }
    }
}
