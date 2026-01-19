# Add Test Fixture

Create a new file-based integration test for Sharpy.

## Test Description

$ARGUMENTS

## File Structure

Test fixtures go in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`:

```
TestFixtures/
├── basics/           # Core language features
├── classes/          # Class-related tests
├── control_flow/     # Control flow tests
├── enums/            # Enum tests
├── errors/           # Error cases (.error files)
├── functions/        # Function-related tests
├── imports/          # Import/module tests
├── inheritance/      # Inheritance tests
├── interfaces/       # Interface tests
├── structs/          # Struct tests
├── type_system/      # Type system tests
└── type_shorthand/   # Type shorthand tests
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
