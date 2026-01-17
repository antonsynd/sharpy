# Test Fixtures

This directory contains file-based integration tests for the Sharpy compiler.

## Structure

```
TestFixtures/
├── basics/              # Basic language features
│   ├── hello_world.spy
│   ├── hello_world.expected
│   ├── arithmetic.spy
│   └── arithmetic.expected
├── functions/           # Function-related tests
├── control_flow/        # Control flow tests (if/elif/else, loops)
├── errors/              # Tests that expect compilation errors
└── README.md
```

## Adding a New Test

### Success Test (compilation should succeed)

1. Create a `.spy` file with your Sharpy source code
2. Create a matching `.expected` file with the exact expected stdout output

Example:
```
my_feature/
├── test_name.spy        # Your Sharpy code
└── test_name.expected   # Expected stdout output (including newlines)
```

### Error Test (compilation should fail)

1. Create a `.spy` file with Sharpy code that should fail to compile
2. Create a matching `.error` file with a substring that should appear in the error message

Example:
```
errors/
├── undefined_var.spy    # Code with an undefined variable
└── undefined_var.error  # Contains: "undefined_var" (substring to match)
```

## Running Tests

```bash
# Run all file-based tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Run a specific test (by name)
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests&DisplayName~hello_world"
```

## Converting Dogfood Outputs

To convert a successful dogfood output to a test fixture:

1. Copy the `source.spy` file to an appropriate subdirectory
2. Rename it to a descriptive name (e.g., `abstract_class_basic.spy`)
3. Create the matching `.expected` file with the expected output
4. Run the test to verify

## Notes

- Expected output files should match stdout exactly, including trailing newlines
- Error files use case-insensitive substring matching
- Tests are discovered automatically at runtime - no need to register them
- Test names in the runner are based on the file path relative to TestFixtures/
