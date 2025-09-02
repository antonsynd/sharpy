use sharpy_compiler_toolchain::*;

#[test]
fn test_new_type_system_examples() {
    // Example: Creating type nodes manually to show the new structure

    // Simple type: int
    let simple_type = Node::TypeName(TypeName::new("int".to_string()));
    println!("Simple type: {simple_type:?}");

    // Qualified type: collections.defaultdict
    let qualified_type = Node::QualifiedType(QualifiedType::new(
        vec!["collections".to_string()],
        "defaultdict".to_string(),
    ));
    println!("Qualified type: {qualified_type:?}");

    // Generic type: list[int]
    let int_type = Node::TypeName(TypeName::new("int".to_string()));
    let list_type = Node::TypeName(TypeName::new("list".to_string()));
    let generic_type = Node::GenericType(GenericType::new(Box::new(list_type), vec![int_type]));
    println!("Generic type: {generic_type:?}");

    // Complex generic: dict[str, list[int]]
    let str_type = Node::TypeName(TypeName::new("str".to_string()));
    let dict_type = Node::TypeName(TypeName::new("dict".to_string()));
    let list_int_type = Node::GenericType(GenericType::new(
        Box::new(Node::TypeName(TypeName::new("list".to_string()))),
        vec![Node::TypeName(TypeName::new("int".to_string()))],
    ));
    let complex_generic = Node::GenericType(GenericType::new(
        Box::new(dict_type),
        vec![str_type, list_int_type],
    ));
    println!("Complex generic: {complex_generic:?}");

    // Optional type: int?
    let optional_type = Node::OptionalType(OptionalType::new(Box::new(Node::TypeName(
        TypeName::new("int".to_string()),
    ))));
    println!("Optional type: {optional_type:?}");

    // Union type: int | str
    let union_type = Node::UnionType(UnionType::new(vec![
        Node::TypeName(TypeName::new("int".to_string())),
        Node::TypeName(TypeName::new("str".to_string())),
    ]));
    println!("Union type: {union_type:?}");

    // This test always passes - it's just for demonstration
}
