# Add Test Fixture

Create a new file-based integration test for Sharpy.

## Test Description

$ARGUMENTS

## File Structure

Test fixtures go in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`:

```
TestFixtures/
├── access_modifiers/          # Access modifier tests
├── basics/                    # Core language features
├── class_with_init/           # Class initialization tests
├── classes/                   # Class-related tests
├── collections/               # Collection type tests
├── control_flow/              # Control flow tests
├── cross_module_inheritance/  # Cross-module inheritance tests
├── enums/                     # Enum tests
├── errors/                    # Error cases (.error files)
├── fstrings/                  # F-string tests
├── functions/                 # Function-related tests
├── generic_function/          # Generic function tests
├── imports/                   # Import/module tests
├── inheritance/               # Inheritance tests
├── interfaces/                # Interface tests
├── module_imports/            # Module import tests
├── strings/                   # String operation tests
├── structs/                   # Struct tests
├── structs_enums/             # Struct and enum combined tests
├── type_shorthand/            # Type shorthand tests
├── type_system/               # Type system tests
└── warnings/                  # Warning tests
```

## Creating a Success Test

1. Create `.spy` file with test code:
```python
# tests/.../TestFixtures/category/test_name.spy
x: int = 42
print(x)
```

2. Create `.expected` file with exact output:
```
42
```

## Creating an Error Test

1. Create `.spy` file with code that should fail:
```python
# tests/.../TestFixtures/errors/invalid_type.spy
x: int = "not an int"
```

2. Create `.error` file with substring to match:
```
Cannot assign
```

## Running Fixture Tests

```bash
# All file-based tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Specific test by name
dotnet test --filter "DisplayName~test_name"
```

## Best Practices

- Test one feature per fixture
- Use descriptive names: `list_negative_indexing.spy`
- Keep tests minimal and focused
- Include edge cases in separate fixtures
- Error tests should have clear, specific error messages
