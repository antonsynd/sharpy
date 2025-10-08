/// Integration tests for builtin symbol registration
use sharpy_compiler_toolchain::semantic::{BuiltinType, SemanticType, SymbolKind, SymbolTable};

#[test]
fn test_builtin_functions_are_registered() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    // Verify core builtin functions are registered
    let print = symbol_table.lookup_symbol("print");
    assert!(print.is_some(), "print function should be registered");
    assert_eq!(print.unwrap().kind, SymbolKind::Function);

    let len = symbol_table.lookup_symbol("len");
    assert!(len.is_some(), "len function should be registered");
    assert_eq!(len.unwrap().kind, SymbolKind::Function);

    let range = symbol_table.lookup_symbol("range");
    assert!(range.is_some(), "range function should be registered");
    assert_eq!(range.unwrap().kind, SymbolKind::Function);
}

#[test]
fn test_type_conversion_functions() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    for func_name in [
        "int", "float", "str", "bool", "list", "dict", "set", "tuple",
    ] {
        let func = symbol_table.lookup_symbol(func_name);
        assert!(func.is_some(), "{func_name} function should be registered");
        assert_eq!(func.unwrap().kind, SymbolKind::Function);
    }
}

#[test]
fn test_collection_functions() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    for func_name in ["len", "range", "enumerate", "zip", "reversed", "sorted"] {
        let func = symbol_table.lookup_symbol(func_name);
        assert!(func.is_some(), "{func_name} function should be registered");
    }
}

#[test]
fn test_math_functions() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    for func_name in ["abs", "min", "max", "sum", "round", "pow"] {
        let func = symbol_table.lookup_symbol(func_name);
        assert!(func.is_some(), "{func_name} function should be registered");
    }
}

#[test]
fn test_iterator_functions() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    for func_name in ["iter", "next", "all", "any", "filter", "map"] {
        let func = symbol_table.lookup_symbol(func_name);
        assert!(func.is_some(), "{func_name} function should be registered");
    }
}

#[test]
fn test_introspection_functions() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    for func_name in ["type", "isinstance", "hasattr", "getattr", "setattr"] {
        let func = symbol_table.lookup_symbol(func_name);
        assert!(func.is_some(), "{func_name} function should be registered");
    }
}

#[test]
fn test_string_methods_are_registered() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    // String methods should be registered under builtin::str namespace
    let upper = symbol_table.symbols.get("builtin::str.upper");
    assert!(upper.is_some(), "str.upper method should be registered");
    assert_eq!(upper.unwrap().kind, SymbolKind::Method);

    let lower = symbol_table.symbols.get("builtin::str.lower");
    assert!(lower.is_some(), "str.lower method should be registered");

    let split = symbol_table.symbols.get("builtin::str.split");
    assert!(split.is_some(), "str.split method should be registered");
}

#[test]
fn test_list_methods_are_registered() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    let append = symbol_table.symbols.get("builtin::list.append");
    assert!(append.is_some(), "list.append method should be registered");
    assert_eq!(append.unwrap().kind, SymbolKind::Method);

    let pop = symbol_table.symbols.get("builtin::list.pop");
    assert!(pop.is_some(), "list.pop method should be registered");

    let extend = symbol_table.symbols.get("builtin::list.extend");
    assert!(extend.is_some(), "list.extend method should be registered");
}

#[test]
fn test_dict_methods_are_registered() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    let keys = symbol_table.symbols.get("builtin::dict.keys");
    assert!(keys.is_some(), "dict.keys method should be registered");

    let values = symbol_table.symbols.get("builtin::dict.values");
    assert!(values.is_some(), "dict.values method should be registered");

    let items = symbol_table.symbols.get("builtin::dict.items");
    assert!(items.is_some(), "dict.items method should be registered");
}

#[test]
fn test_set_methods_are_registered() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    let add = symbol_table.symbols.get("builtin::set.add");
    assert!(add.is_some(), "set.add method should be registered");

    let union = symbol_table.symbols.get("builtin::set.union");
    assert!(union.is_some(), "set.union method should be registered");

    let intersection = symbol_table.symbols.get("builtin::set.intersection");
    assert!(
        intersection.is_some(),
        "set.intersection method should be registered"
    );

    let issubset = symbol_table.symbols.get("builtin::set.issubset");
    assert!(
        issubset.is_some(),
        "set.issubset method should be registered"
    );
}

#[test]
fn test_dunder_methods_are_registered() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    // String dunder methods
    let str_str = symbol_table.symbols.get("builtin::str.__str__");
    assert!(
        str_str.is_some(),
        "str.__str__ dunder method should be registered"
    );

    let str_len = symbol_table.symbols.get("builtin::str.__len__");
    assert!(
        str_len.is_some(),
        "str.__len__ dunder method should be registered"
    );

    let str_eq = symbol_table.symbols.get("builtin::str.__eq__");
    assert!(
        str_eq.is_some(),
        "str.__eq__ dunder method should be registered"
    );

    // List dunder methods
    let list_len = symbol_table.symbols.get("builtin::list.__len__");
    assert!(
        list_len.is_some(),
        "list.__len__ dunder method should be registered"
    );

    let list_getitem = symbol_table.symbols.get("builtin::list.__getitem__");
    assert!(
        list_getitem.is_some(),
        "list.__getitem__ dunder method should be registered"
    );

    // Dict dunder methods
    let dict_len = symbol_table.symbols.get("builtin::dict.__len__");
    assert!(
        dict_len.is_some(),
        "dict.__len__ dunder method should be registered"
    );

    let dict_contains = symbol_table.symbols.get("builtin::dict.__contains__");
    assert!(
        dict_contains.is_some(),
        "dict.__contains__ dunder method should be registered"
    );
}

#[test]
fn test_builtin_function_has_correct_type() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    let int_func = symbol_table.lookup_symbol("int").unwrap();
    match &int_func.symbol_type {
        SemanticType::Function {
            params,
            return_type,
        } => {
            assert_eq!(params.len(), 1, "int() should take 1 parameter");
            assert!(return_type.is_some(), "int() should have return type");
            if let Some(ret_type) = return_type {
                match ret_type.as_ref() {
                    SemanticType::Builtin(BuiltinType::Int) => {
                        // Correct!
                    }
                    _ => panic!("int() should return SemanticType::Builtin(BuiltinType::Int)"),
                }
            }
        }
        _ => panic!("int should be a Function type"),
    }
}

#[test]
fn test_builtin_method_has_correct_metadata() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    let append = symbol_table.symbols.get("builtin::list.append").unwrap();
    assert_eq!(append.kind, SymbolKind::Method);
    assert_eq!(append.name, "append");
    assert!(!append.is_static, "Instance method should not be static");
}

#[test]
fn test_total_registered_symbols_count() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    let total_symbols = symbol_table.symbols.len();

    // We should have at least:
    // - 40+ builtin functions
    // - 15+ str methods (including dunder methods)
    // - 15+ list methods (including dunder methods)
    // - 10+ dict methods (including dunder methods)
    // - 15+ set methods (including dunder methods)
    // Total: 90+ symbols
    assert!(
        total_symbols >= 90,
        "Should have at least 90 builtin symbols, found {total_symbols}"
    );

    println!("Total builtin symbols registered: {total_symbols}");
}

#[test]
fn test_all_functions_are_static() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    for (id, symbol) in &symbol_table.symbols {
        if symbol.kind == SymbolKind::Function && id.starts_with("builtin::") {
            assert!(symbol.is_static, "Builtin function '{id}' should be static");
        }
    }
}

#[test]
fn test_all_methods_are_not_static() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    for (id, symbol) in &symbol_table.symbols {
        if symbol.kind == SymbolKind::Method && id.contains('.') {
            assert!(
                !symbol.is_static,
                "Builtin method '{id}' should not be static"
            );
        }
    }
}
