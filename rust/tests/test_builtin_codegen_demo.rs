/// Demo of how code generation would use the builtin symbol system
use sharpy_compiler_toolchain::semantic::builtins::{
    create_builtin_functions, create_builtin_methods,
};
use sharpy_compiler_toolchain::semantic::{BuiltinType, SemanticType, SymbolTable};

#[test]
fn demo_code_generation_for_print() {
    let builtins = create_builtin_functions();
    let print_builtin = builtins.iter().find(|b| b.symbol.name == "print").unwrap();

    // Code generator would use:
    println!("Sharpy name: {}", print_builtin.symbol.name); // "print"
    println!("C# name: {}", print_builtin.csharp_mapping.csharp_name); // "Print"
    println!(
        "C# namespace: {}",
        print_builtin.csharp_mapping.csharp_namespace
    ); // "Sharpy"
    println!("Is static: {}", print_builtin.csharp_mapping.is_static); // true

    // Generated C# code would be:
    // Sharpy.__Exports__.Print(...)
    let generated_call = format!(
        "{}.{}.{}(...)",
        print_builtin.csharp_mapping.csharp_namespace,
        print_builtin.csharp_mapping.csharp_type.as_ref().unwrap(),
        print_builtin.csharp_mapping.csharp_name
    );
    println!("Generated C# call: {generated_call}");
    assert_eq!(generated_call, "Sharpy.__Exports__.Print(...)");
}

#[test]
fn demo_code_generation_for_str_upper() {
    let builtin_methods = create_builtin_methods();
    let str_methods = builtin_methods.get("str").unwrap();
    let upper = str_methods
        .iter()
        .find(|m| m.symbol.name == "upper")
        .unwrap();

    // Code generator would use:
    println!("Sharpy name: {}", upper.symbol.name); // "upper"
    println!("C# name: {}", upper.csharp_mapping.csharp_name); // "Upper"
    println!(
        "C# type: {}",
        upper.csharp_mapping.csharp_type.as_ref().unwrap()
    ); // "Str"
    println!("Is static: {}", upper.csharp_mapping.is_static); // false

    // For: name.upper()
    // Generated C# code would be: name.Upper()
    let var_name = "name";
    let generated_call = format!("{}.{}()", var_name, upper.csharp_mapping.csharp_name);
    println!("Generated C# call: {generated_call}");
    assert_eq!(generated_call, "name.Upper()");
}

#[test]
fn demo_code_generation_for_list_append() {
    let builtin_methods = create_builtin_methods();
    let list_methods = builtin_methods.get("list").unwrap();
    let append = list_methods
        .iter()
        .find(|m| m.symbol.name == "append")
        .unwrap();

    // Code generator would use:
    println!("Sharpy name: {}", append.symbol.name); // "append"
    println!("C# name: {}", append.csharp_mapping.csharp_name); // "Append"
    println!("Is static: {}", append.csharp_mapping.is_static); // false

    // For: items.append(42)
    // Generated C# code would be: items.Append(42)
    let var_name = "items";
    let generated_call = format!("{}.{}(42)", var_name, append.csharp_mapping.csharp_name);
    println!("Generated C# call: {generated_call}");
    assert_eq!(generated_call, "items.Append(42)");
}

#[test]
fn demo_type_checking_for_int() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    let int_func = symbol_table.lookup_symbol("int").unwrap();

    // Type checker would use:
    match &int_func.symbol_type {
        SemanticType::Function {
            params,
            return_type,
        } => {
            println!("Function: int");
            println!("Parameters: {} param(s)", params.len());

            if let Some(ret) = return_type {
                match ret.as_ref() {
                    SemanticType::Builtin(BuiltinType::Int) => {
                        println!("Return type: int");
                    }
                    _ => panic!("Unexpected return type"),
                }
            }

            // Type checker can validate:
            // 1. Correct number of arguments (1)
            // 2. Return type is int
            assert_eq!(params.len(), 1);
            assert!(return_type.is_some());
        }
        _ => panic!("int should be a function"),
    }
}

#[test]
fn demo_looking_up_method_on_type() {
    let mut symbol_table = SymbolTable::new();
    symbol_table.add_builtin_functions();

    // When analyzing: my_string.upper()
    // 1. Determine type of my_string (str)
    // 2. Look up method on that type
    let method_id = "builtin::str.upper";
    let upper_method = symbol_table.symbols.get(method_id);

    assert!(upper_method.is_some(), "Should find str.upper method");
    let method = upper_method.unwrap();

    println!("Found method: {}.{}", "str", method.name);
    println!("Return type: {:?}", method.symbol_type);

    // Type checker knows this returns str
    match &method.symbol_type {
        SemanticType::Function { return_type, .. } => {
            assert!(return_type.is_some());
            match return_type.as_ref().unwrap().as_ref() {
                SemanticType::Builtin(BuiltinType::Str) => {
                    println!("✓ Correctly returns str");
                }
                _ => panic!("Expected str return type"),
            }
        }
        _ => panic!("Method should have function type"),
    }
}

#[test]
fn demo_dunder_method_codegen() {
    let builtin_methods = create_builtin_methods();
    let str_methods = builtin_methods.get("str").unwrap();
    let str_dunder = str_methods
        .iter()
        .find(|m| m.symbol.name == "__str__")
        .unwrap();

    // For Python: str(obj)  or  obj.__str__()
    // C# name is __Str__ (preserves dunder, but PascalCase)
    assert_eq!(str_dunder.csharp_mapping.csharp_name, "__Str__");

    // Generated C# code would be: obj.__Str__()
    // Or: ((IStringRepresentable)obj).__Str__() with interface cast
    println!("Dunder method: {}", str_dunder.symbol.name);
    println!("C# dunder name: {}", str_dunder.csharp_mapping.csharp_name);
}

#[test]
fn demo_generic_list_method() {
    let builtin_methods = create_builtin_methods();
    let list_methods = builtin_methods.get("list").unwrap();
    let getitem = list_methods
        .iter()
        .find(|m| m.symbol.name == "__getitem__")
        .unwrap();

    // list.__getitem__(index: int) -> T
    // The return type is the generic element type
    println!("Method: {}", getitem.symbol.name);
    println!("C# name: {}", getitem.csharp_mapping.csharp_name);

    match &getitem.symbol.symbol_type {
        SemanticType::Function {
            params,
            return_type,
        } => {
            println!("Parameters: {}", params.len());
            assert_eq!(params.len(), 1); // Just the index parameter

            // Return type is SemanticType::Unknown("item") for now
            // In the future, this will be a generic type parameter T
            assert!(return_type.is_some());
        }
        _ => panic!("Should be a function type"),
    }
}

#[test]
fn demo_full_code_generation_workflow() {
    // Simulate code generation for: print(items.pop())

    let builtins = create_builtin_functions();
    let builtin_methods = create_builtin_methods();

    // Step 1: Generate list.pop() call
    let list_methods = builtin_methods.get("list").unwrap();
    let pop = list_methods
        .iter()
        .find(|m| m.symbol.name == "pop")
        .unwrap();
    let pop_call = format!("items.{}", pop.csharp_mapping.csharp_name); // "items.Pop"

    // Step 2: Generate print() call with result
    let print_fn = builtins.iter().find(|b| b.symbol.name == "print").unwrap();
    let print_call = format!(
        "{}.{}.{}({}())",
        print_fn.csharp_mapping.csharp_namespace,
        print_fn.csharp_mapping.csharp_type.as_ref().unwrap(),
        print_fn.csharp_mapping.csharp_name,
        pop_call
    );

    println!("Sharpy code: print(items.pop())");
    println!("Generated C# code: {print_call};");

    // Should be: Sharpy.__Exports__.Print(items.Pop());
    assert_eq!(print_call, "Sharpy.__Exports__.Print(items.Pop())");
}
