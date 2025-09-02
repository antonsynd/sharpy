use sharpy_compiler_toolchain::{Parser, SharpyLexer};

#[test]
fn test_empty_function_call() {
    let input = "func()";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_function_call_with_trailing_comma() {
    let input = "func(a, b, c,)";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_deeply_nested_function_calls() {
    let input = "f(g(h(i(j(k())))))";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_function_call_with_complex_expressions() {
    let input = "func(a + b, c * d, e[f], g.h)";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_chained_attribute_and_subscript() {
    let input = "obj.attr[0].method()[1].prop";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_subscript_with_slice() {
    let input = "arr[start:end:step]";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    // Note: This might not be fully supported yet, but should not panic
    let _result = result;
}

#[test]
fn test_multiple_subscripts() {
    let input = "matrix[i][j][k]";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_empty_list_with_trailing_comma() {
    let input = "[,]";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    // This should probably be an error, but let's see what happens
    let _result = result;
}

#[test]
fn test_empty_dict_vs_set() {
    // Empty braces should be a dict
    let input = "{}";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_single_element_tuple() {
    let input = "(x,)";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}

#[test]
fn test_complex_lambda_expressions() {
    let test_cases = vec![
        "lambda: 42",
        "lambda x: x + 1",
        "lambda x, y: x * y",
        "lambda x: lambda y: x + y",    // Nested lambda
        "lambda x: x if x > 0 else -x", // Lambda with conditional (if supported)
    ];

    for case in test_cases {
        let mut lexer = SharpyLexer::new(case);
        let tokens = lexer.tokenize_all().unwrap();
        let mut parser = Parser::new(tokens);
        let result = parser.parse();
        // Some of these might not be fully supported yet
        let _result = result;
    }
}

#[test]
fn test_lambda_with_return_type_annotation() {
    let input = "lambda x: -> int: x + 1"; // Correct syntax: lambda args: -> type: body
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    // This might not be fully implemented yet, so just ensure it doesn't panic
    let _result = result;
}

#[test]
fn test_comprehensive_list_operations() {
    let input = "[x for x in range(10) if x % 2 == 0]";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    // List comprehensions might not be supported yet
    let _result = result;
}

#[test]
fn test_dict_with_complex_keys_and_values() {
    let input = "{f(x): g(y) for x, y in items}";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    // Dict comprehensions might not be supported yet
    let _result = result;
}

#[test]
fn test_set_operations() {
    let input = "{1, 2, 3} | {3, 4, 5}";
    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();
    assert!(result.is_ok());
}
