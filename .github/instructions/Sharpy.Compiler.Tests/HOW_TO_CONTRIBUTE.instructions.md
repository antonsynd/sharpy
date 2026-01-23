````instructions
# Sharpy.Compiler.Tests

Compiler test suite. Location: `src/Sharpy.Compiler.Tests/`

## Test Organization

```
Sharpy.Compiler.Tests/
├── Lexer/          # Tokenization tests
├── Parser/         # AST generation tests
├── Semantic/       # Type checking, name resolution
├── CodeGen/        # C# generation tests
├── Integration/    # End-to-end: Sharpy → C# → execute
│   ├── TestFixtures/  # File-based tests (.spy + .expected)
│   └── IntegrationTestBase.cs
├── Discovery/      # Module import tests
├── Analysis/       # Control flow analysis tests
└── Helpers/        # Test infrastructure (ProjectCompilationHelper)
```

## Running Tests

```bash
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "FullyQualifiedName~CodeGen"
dotnet test --filter "FullyQualifiedName~Integration"
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

## Test Patterns

**Unit test:**
```csharp
[Fact]
public void TestTokenizeIdentifier()
{
    var lexer = new Lexer("hello_world", logger);
    var tokens = lexer.TokenizeAll();
    Assert.Equal(TokenType.Identifier, tokens[0].Type);
}
```

**Integration test:**
```csharp
public class MyTests : IntegrationTestBase
{
    [Fact]
    public void FeatureWorks()
    {
        var result = CompileAndExecute("print(1 + 2)");
        Assert.True(result.Success);
        Assert.Equal("3\n", result.StandardOutput);
    }
}
```

**File-based test:** Add `.spy` + `.expected` pair to `TestFixtures/`:
```
TestFixtures/my_category/
├── my_test.spy       # Sharpy source
└── my_test.expected  # Expected stdout (exact match)
```

**Multi-file project test:**
```csharp
using var helper = new ProjectCompilationHelper(output);
helper.WithRootNamespace("Test")
    .AddSourceFile("main.spy", "...")
    .AddSourceFile("lib.spy", "...")
    .CreateProjectFile();
var result = helper.Compile();
```

## Critical Rules

1. **Never change test expectations to match bugs** — fix the implementation
2. **Skip with reason if blocked:**
   ```csharp
   [Fact(Skip = "TODO: Implement feature. See issue #42")]
   ```
3. **Test names describe behavior:** `TestParser_Parses_IfElseStatement`

## Test Fixture Categories

| Directory | Tests |
|-----------|-------|
| `basics/` | Hello world, simple expressions |
| `functions/` | Function definitions, calls |
| `classes/` | Class definitions, inheritance |
| `control_flow/` | if/while/for/match |
| `errors/` | Expected compilation failures (`.error` files) |
| `imports/` | Module imports |
| `generics/` | Generic types and functions |

## File-Based Tests

Auto-discovered tests in `Integration/TestFixtures/`:
```
TestFixtures/
├── basics/hello_world.spy      # Source
├── basics/hello_world.expected # Expected stdout (exact match)
├── errors/undefined_var.spy    # Error case
└── errors/undefined_var.error  # Substring to match in error
```

**Run file-based tests:**
```bash
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

````
