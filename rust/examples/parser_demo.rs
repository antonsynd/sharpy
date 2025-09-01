use sharpy_compiler_toolchain::*;

fn main() {
    let examples = vec![
        "x = 42",
        "name: str = \"hello\"",
        "numbers = [1, 2, 3, 4, 5]",
        "matrix = [[1, 2], [3, 4]]",
        "flag = True",
        "value = None",
        "x, y = (1, 2)",
        "a: int, b: float = (42, 3.14)",
        "x: int, y = (1, 2.5)",
        "coords, color = ([1, 2], \"red\")",
        "first, rest = ([1, 2, 3], [4, 5, 6])",
        // Comparison examples
        "x == 5",
        "age >= 18",
        "1 < x <= 10",
        "name in users",
        "obj is None",
        // If statement examples
        "if x == 5:\n    y = 10",
        "if age >= 18:\n    status = \"adult\"\nelse:\n    status = \"minor\"",
        // While loop examples
        "while x < 10:\n    x = 42",
        "while count > 0:\n    count = 0\nelse:\n    status = \"finished\"",
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
                            println!("    Node {}: {}", i, get_node_description(node));
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

fn get_node_description(node: &Node) -> String {
    match node {
        Node::Assign(assign) => {
            let target_type = match &*assign.target {
                Node::Name(_) => "Simple Assignment",
                Node::TypedName(_) => "Typed Assignment",
                Node::Tuple(tuple) => {
                    if tuple
                        .elements
                        .iter()
                        .any(|e| matches!(e, Node::TypedName(_)))
                    {
                        "Typed Destructuring Assignment"
                    } else {
                        "Destructuring Assignment"
                    }
                }
                _ => "Other Assignment",
            };
            target_type.to_string()
        }
        Node::Compare(compare) => {
            if compare.ops.len() == 1 {
                format!("Simple Comparison ({:?})", compare.ops[0])
            } else {
                format!("Chained Comparison ({} ops)", compare.ops.len())
            }
        }
        Node::If(_) => "If Statement".to_string(),
        Node::While(while_node) => {
            if while_node.else_.is_empty() {
                "While Loop".to_string()
            } else {
                "While Loop with Else".to_string()
            }
        }
        Node::Constant(_) => "Constant".to_string(),
        Node::Name(_) => "Name".to_string(),
        Node::List(_) => "List".to_string(),
        Node::Tuple(_) => "Tuple".to_string(),
        Node::TypedName(_) => "TypedName".to_string(),
        _ => "Other".to_string(),
    }
}
