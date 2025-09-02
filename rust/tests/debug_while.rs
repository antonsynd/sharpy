#[cfg(test)]
mod debug_while_test {
    use sharpy_compiler_toolchain::*;

    #[test]
    fn debug_while_parsing() {
        let code = "while x < 10:\n    x = x + 1\nelse:\n    print('done')";
        println!("Code: {code:?}");

        let mut lexer = SharpyLexer::new(code);
        let tokens = lexer.tokenize_all().expect("Lexing should succeed");

        println!("\nTokens:");
        for (i, token) in tokens.iter().enumerate() {
            println!("{i}: {token:?}");
        }

        let mut parser = Parser::new(tokens);
        let result = parser.parse();

        match result {
            Ok(nodes) => {
                println!("\nParsed {} top-level nodes:", nodes.len());
                for (i, node) in nodes.iter().enumerate() {
                    println!("Node {i}: {node:?}");

                    if let Node::While(while_node) = node {
                        println!("  While body has {} statements:", while_node.body.len());
                        for (j, stmt) in while_node.body.iter().enumerate() {
                            println!("    Body[{j}]: {stmt:?}");
                        }

                        println!("  While else has {} statements:", while_node.else_.len());
                        for (j, stmt) in while_node.else_.iter().enumerate() {
                            println!("    Else[{j}]: {stmt:?}");
                        }
                    }
                }
            }
            Err(e) => {
                println!("Parse error: {e:?}");
            }
        }

        // Don't fail the test, just print debug info
    }
}
