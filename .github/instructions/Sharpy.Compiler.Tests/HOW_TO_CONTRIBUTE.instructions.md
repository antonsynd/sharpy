# Sharpy.Compiler.Tests

Compiler test suite. Location: `src/Sharpy.Compiler.Tests/`

## Directory Structure

```
Sharpy.Compiler.Tests/
├── Lexer/           # Tokenization tests
├── Parser/          # AST generation tests
├── Ast/             # AST node tests
├── Semantic/        # Type checking, name resolution
├── CodeGen/         # C# generation tests
├── Integration/     # End-to-end: Sharpy → C# → execute
│   ├── TestFixtures/   # File-based tests (.spy + .expected)
│   └── IntegrationTestBase.cs
├── Discovery/       # Module import tests
├── Analysis/        # Control flow analysis tests
├── Diagnostics/     # Diagnostic tests
├── Fuzz/            # Fuzzing tests
├── Helpers/         # ProjectCompilationHelper for multi-file tests
├── Logging/         # Logging tests
├── Model/           # Model tests
├── Performance/     # Performance tests
├── Project/         # Project compilation tests
├── Services/        # Compiler services tests
├── Stress/          # Stress tests
├── Text/            # Text/source tests
└── Utilities/       # Utility tests
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

**Integration test (inherit `IntegrationTestBase`):**
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

**Multi-file project test (use `ProjectCompilationHelper`):**
```csharp
using var helper = new ProjectCompilationHelper(output);
helper.WithRootNamespace("Test")
    .AddSourceFile("main.spy", "def main(): print('hello')")
    .AddSourceFile("lib.spy", "def helper() -> int: return 42")
    .CreateProjectFile();
var result = helper.Compile();
Assert.True(result.Success);
```

## File-Based Tests (`Integration/TestFixtures/`)

Auto-discovered tests via `.spy` + `.expected` (or `.error`) pairs:

```
TestFixtures/
├── basics/hello_world.spy      # Source
├── basics/hello_world.expected # Expected stdout (exact match)
├── errors/undefined_var.spy    # Error case
└── errors/undefined_var.error  # Substring to match in error
```

**Skip test:** Add `.skip` file with reason.

**Warning tests:** `.warning` file — empty means expect no warnings, non-empty lines are expected substrings. Can combine with `.expected`.

**Multi-file tests:** A subdirectory with multiple `.spy` files and a `main.spy` entry point, plus `main.expected` or `main.error`.

**Test categories:** `access_modifiers/`, `basics/`, `class_with_init/`, `classes/`, `collections/`, `control_flow/`, `cross_module_inheritance/`, `enums/`, `errors/`, `fstrings/`, `functions/`, `generic_function/`, `imports/`, `inheritance/`, `interface_definition/`, `interfaces/`, `module_imports/`, `multi_file/`, `strings/`, `structs/`, `structs_enums/`, `type_shorthand/`, `type_system/`, `warnings/`

## Critical Rules

1. **NEVER change test expectations to match bugs** — fix the implementation
2. **Skip with reason if blocked:**
   ```csharp
   [Fact(Skip = "TODO: Implement feature. See issue #42")]
   ```
3. **Test names describe behavior:** `TestParser_Parses_IfElseStatement`
4. **Newline sensitivity:** `.expected` files must have exact trailing newlines

## Adding a File-Based Test

1. Create `my_feature.spy` in appropriate `TestFixtures/` subdirectory
2. Create `my_feature.expected` with exact expected stdout (including trailing newlines)
3. For error tests: create `my_feature.error` with substring to match
4. Tests are auto-discovered — no registration needed
5. Organize by feature: `basics/`, `functions/`, `classes/`, etc.

## Debugging Failed Tests

```bash
# Run specific test and see output
dotnet test --filter "DisplayName~my_test_name" --logger "console;verbosity=detailed"

# Debug codegen for a test file
dotnet run --project src/Sharpy.Cli -- emit csharp path/to/test.spy

# Debug AST
dotnet run --project src/Sharpy.Cli -- emit ast path/to/test.spy
```

```bash
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```
