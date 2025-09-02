#[cfg(test)]
mod debug_test {
    use sharpy_compiler_toolchain::Parser;
    use sharpy_compiler_toolchain::lexer::SharpyLexer;

    #[test]
    fn debug_qualified_type() {
        let source = "config: app.Config = None";
        let mut lexer = SharpyLexer::new(source);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);

        let result = parser.parse();
        match result {
            Ok(ast) => {
                println!("Parsed successfully: {:#?}", ast);
            }
            Err(e) => {
                println!("Parse error: {:?}", e);
                panic!("Parse failed");
            }
        }
    }

    #[test]
    fn debug_generic_type() {
        let source = "items: List[str] = []";
        let mut lexer = SharpyLexer::new(source);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);

        let result = parser.parse();
        match result {
            Ok(ast) => {
                println!("Parsed successfully: {:#?}", ast);
            }
            Err(e) => {
                println!("Parse error: {:?}", e);
                panic!("Parse failed");
            }
        }
    }
}
