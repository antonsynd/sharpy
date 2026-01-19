# Run Tests

Run tests for a specific component or feature.

## Target

$ARGUMENTS

## Commands

```bash
# Run all tests
dotnet test

# Filter by component
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "FullyQualifiedName~CodeGen"

# File-based integration tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Specific test by display name
dotnet test --filter "DisplayName~test_name"
```

## Test Types

| Type | Location | Description |
|------|----------|-------------|
| Unit | `src/Sharpy.Compiler.Tests/` | Component isolation tests |
| Integration | `src/Sharpy.Compiler.Tests/Integration/` | End-to-end compilation |
| File-based | `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` | `.spy` + `.expected` pairs |
| Core Library | `src/Sharpy.Core.Tests/` | Runtime library tests |

## Adding New Tests

### File-Based Tests
Create paired files in `TestFixtures/`:
- `category/test_name.spy` - Source code
- `category/test_name.expected` - Expected stdout (exact match)
- `category/test_name.error` - Expected error substring (for error cases)

### Integration Tests
```csharp
[Fact]
public void TestFeature()
{
    var source = @"...";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    Assert.Equal("expected output", result.StandardOutput);
}
```

## Critical Rule

**NEVER modify expected values to make tests pass. Fix the implementation.**
