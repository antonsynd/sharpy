use sharpy_compiler_toolchain::*;

/// Test basic function definition without parameters or return type
#[test]
fn test_basic_function_definition() {
    let input = r#"
def hello():
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    if let Err(ref err) = result {
        println!("Parse error: {:?}", err);
    }
    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "hello");
        assert_eq!(func.access_modifier, None); // Public by default
        assert!(func.args.args.is_empty());
        assert!(func.return_type.is_none());
        assert_eq!(func.body.len(), 1);
    } else {
        panic!("Expected FunctionDef node, got: {:?}", &nodes[0]);
    }
}

/// Test function definition with parameters
#[test]
fn test_function_with_parameters() {
    let input = r#"
def greet(name, age):
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "greet");
        assert_eq!(func.args.args.len(), 2);
        assert_eq!(func.args.args[0].name, "name");
        assert_eq!(func.args.args[1].name, "age");
        assert!(func.args.args[0].type_.is_none());
        assert!(func.args.args[1].type_.is_none());
    } else {
        panic!("Expected FunctionDef node");
    }
}

/// Test function definition with typed parameters
#[test]
fn test_function_with_typed_parameters() {
    let input = r#"
def calculate(x: int, y: float):
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "calculate");
        assert_eq!(func.args.args.len(), 2);
        assert_eq!(func.args.args[0].name, "x");
        assert_eq!(func.args.args[1].name, "y");
        assert!(func.args.args[0].type_.is_some());
        assert!(func.args.args[1].type_.is_some());
    } else {
        panic!("Expected FunctionDef node");
    }
}

/// Test function definition with return type
#[test]
fn test_function_with_return_type() {
    let input = r#"
def add(a: int, b: int) -> int:
    return a + b
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "add");
        assert!(func.return_type.is_some());
        assert_eq!(func.args.args.len(), 2);
        assert_eq!(func.body.len(), 1);
    } else {
        panic!("Expected FunctionDef node");
    }
}

/// Test function definition with default parameters
#[test]
fn test_function_with_default_parameters() {
    let input = r#"
def greet(name: str, greeting: str = "Hello"):
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "greet");
        assert_eq!(func.args.args.len(), 2);
        assert_eq!(func.args.args[0].name, "name");
        assert_eq!(func.args.args[1].name, "greeting");
        assert!(func.args.args[0].default.is_none());
        assert!(func.args.args[1].default.is_some());
    } else {
        panic!("Expected FunctionDef node");
    }
}

/// Test protected function (single underscore prefix)
#[test]
fn test_protected_function() {
    let input = r#"
def _internal_helper():
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "internal_helper");
        assert_eq!(func.access_modifier, Some("protected".to_string()));
    } else {
        panic!("Expected FunctionDef node");
    }
}

/// Test private function (double underscore prefix)
#[test]
fn test_private_function() {
    let input = r#"
def __secret_method():
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "secret_method");
        assert_eq!(func.access_modifier, Some("private".to_string()));
    } else {
        panic!("Expected FunctionDef node");
    }
}

/// Test internal function (single dollar prefix)
#[test]
fn test_internal_function() {
    let input = r#"
def $project_helper():
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "project_helper");
        assert_eq!(func.access_modifier, Some("internal".to_string()));
    } else {
        panic!("Expected FunctionDef node");
    }
}

/// Test file-scoped function (double dollar prefix)
#[test]
fn test_file_scoped_function() {
    let input = r#"
def $$file_local():
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "file_local");
        assert_eq!(func.access_modifier, Some("file".to_string()));
    } else {
        panic!("Expected FunctionDef node");
    }
}

/// Test complex function with multiple features
#[test]
fn test_complex_function() {
    let input = r#"
def process_data(items: list[str], threshold: int = 10, debug: bool = False) -> dict[str, int]:
    result = {}
    for item in items:
        if len(item) > threshold:
            result[item] = len(item)
    return result
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    if let Err(ref err) = result {
        println!("Parse error: {:?}", err);
    }
    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "process_data");
        assert_eq!(func.access_modifier, None); // Public
        assert!(func.return_type.is_some());
        assert_eq!(func.args.args.len(), 3);

        // Check parameter details
        assert_eq!(func.args.args[0].name, "items");
        assert!(func.args.args[0].type_.is_some());
        assert!(func.args.args[0].default.is_none());

        assert_eq!(func.args.args[1].name, "threshold");
        assert!(func.args.args[1].type_.is_some());
        assert!(func.args.args[1].default.is_some());

        assert_eq!(func.args.args[2].name, "debug");
        assert!(func.args.args[2].type_.is_some());
        assert!(func.args.args[2].default.is_some());

        // Should have multiple statements in body
        assert!(func.body.len() > 1);
    } else {
        panic!("Expected FunctionDef node");
    }
}

/// Test function with trailing comma in parameter list
#[test]
fn test_function_with_trailing_comma() {
    let input = r#"
def test(a: int, b: str,):
    pass
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    assert!(result.is_ok());
    let nodes = result.unwrap();
    assert_eq!(nodes.len(), 1);

    if let Node::FunctionDef(func) = &nodes[0] {
        assert_eq!(func.name, "test");
        assert_eq!(func.args.args.len(), 2);
    } else {
        panic!("Expected FunctionDef node");
    }
}
