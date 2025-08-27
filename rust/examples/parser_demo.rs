use sharpy_compiler_toolchain::*;

fn main() {
    let examples = vec![
        "x = 42",
        "name: str = \"hello\"",
        "numbers = [1, 2, 3, 4, 5]",
        "matrix = [[1, 2], [3, 4]]",
        "flag = True",
        "value = None",
    ];

    for code in examples {
        println!("Parsing: {}", code);

        // Tokenize
        let mut lexer = SharpyLexer::new(code);
        match lexer.tokenize_all() {
            Ok(tokens) => {
                println!("  Tokens: {} tokens", tokens.len());

                // Parse
                let mut parser = Parser::new(tokens);
                match parser.parse() {
                    Ok(nodes) => {
                        println!("  Success: {} AST nodes", nodes.len());
                        for (i, node) in nodes.iter().enumerate() {
                            println!("    Node {}: {:?}", i, get_node_type(node));
                        }
                    }
                    Err(err) => {
                        println!("  Parse error: {}", err);
                    }
                }
            }
            Err(err) => {
                println!("  Lexer error: {:?}", err);
            }
        }
        println!();
    }
}

fn get_node_type(node: &Node) -> &'static str {
    match node {
        Node::Assign(_) => "Assignment",
        Node::Constant(_) => "Constant",
        Node::Name(_) => "Name",
        Node::List(_) => "List",
        _ => "Other",
    }
}
