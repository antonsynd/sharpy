---
name: add-test-fixture
description: Create a file-based integration test (.spy + .expected/.error pair)
argument-hint: <description of test>
---

Create a new file-based integration test in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`.

## File formats

- **Success test**: `category/test_name.spy` + `category/test_name.expected` (exact stdout match)
- **Error test**: `category/test_name.spy` + `category/test_name.error` (substring match in error output)
- **Warning test**: `category/test_name.warning` (empty = no warnings, non-empty lines = expected substrings)
- **Skip**: Add a `.skip` file to disable a test

## Existing categories

access_modifiers, basics, builtins, class_with_init, classes, collections, control_flow, cross_module_inheritance, enums, errors, exception_handling, exceptions, expressions, fstrings, functions, generic_function, imports, inheritance, interface_definition, interfaces, module_imports, modules, multi_file, name_collision, named_tuples, optional_result, pattern_matching, properties, statements, strings, structs, structs_enums, type_aliases, type_shorthand, type_system, variable_name_collision, warnings, with_statement

## Steps

1. Pick the appropriate category (or create a new one if none fits)
2. Write the `.spy` file testing one feature, kept minimal
3. Write the `.expected` or `.error` file
4. Run: `dotnet test --filter "DisplayName~test_name"` to verify
