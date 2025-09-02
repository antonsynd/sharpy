#[cfg(test)]
mod integration_test {
    use sharpy_compiler_toolchain::Node;
    use sharpy_compiler_toolchain::Parser;
    use sharpy_compiler_toolchain::lexer::SharpyLexer;

    #[test]
    fn test_comprehensive_type_system() {
        // Test simple type
        let simple = "x: int = 5";
        let mut lexer = SharpyLexer::new(simple);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);
        let result = parser.parse().unwrap();
        assert_eq!(result.len(), 1);

        // Test qualified type
        let qualified = "config: app.Config = None";
        let mut lexer = SharpyLexer::new(qualified);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);
        let result = parser.parse().unwrap();
        assert_eq!(result.len(), 1);

        // Test generic type
        let generic = "items: List[str] = []";
        let mut lexer = SharpyLexer::new(generic);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);
        let result = parser.parse().unwrap();
        assert_eq!(result.len(), 1);

        // Test complex nested generic
        let complex = "cache: Dict[str, List[int]] = None";
        let mut lexer = SharpyLexer::new(complex);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);
        let result = parser.parse().unwrap();
        assert_eq!(result.len(), 1);

        // Verify the nested generic is parsed correctly
        match &result[0] {
            Node::Assign(assign) => {
                match &*assign.target {
                    Node::TypedName(typed_name) => {
                        match &*typed_name.type_ {
                            Node::GenericType(generic_type) => {
                                // Dict base type
                                match &*generic_type.base_type {
                                    Node::TypeName(type_name) => {
                                        assert_eq!(type_name.name, "Dict");
                                    }
                                    _ => panic!("Expected TypeName for Dict"),
                                }

                                // Two type arguments
                                assert_eq!(generic_type.type_args.len(), 2);

                                // First: str
                                match &generic_type.type_args[0] {
                                    Node::TypeName(type_name) => {
                                        assert_eq!(type_name.name, "str");
                                    }
                                    _ => panic!("Expected str type"),
                                }

                                // Second: List[int]
                                match &generic_type.type_args[1] {
                                    Node::GenericType(inner_generic) => {
                                        match &*inner_generic.base_type {
                                            Node::TypeName(type_name) => {
                                                assert_eq!(type_name.name, "List");
                                            }
                                            _ => panic!("Expected List base type"),
                                        }

                                        assert_eq!(inner_generic.type_args.len(), 1);
                                        match &inner_generic.type_args[0] {
                                            Node::TypeName(type_name) => {
                                                assert_eq!(type_name.name, "int");
                                            }
                                            _ => panic!("Expected int type"),
                                        }
                                    }
                                    _ => panic!("Expected nested generic List[int]"),
                                }
                            }
                            _ => panic!("Expected GenericType"),
                        }
                    }
                    _ => panic!("Expected TypedName"),
                }
            }
            _ => panic!("Expected Assign"),
        }
    }
}
