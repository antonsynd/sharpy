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
└── Discovery/      # Module import tests
```

## Running Tests

```bash
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "FullyQualifiedName~CodeGen"
dotnet test --filter "FullyQualifiedName~Integration"
```

## Test Patterns

**Lexer test:**
```csharp
[Fact]
public void TestTokenizeIdentifier()
{
    var lexer = new Lexer("hello_world", logger);
    var tokens = lexer.TokenizeAll();
    Assert.Equal(TokenType.Identifier, tokens[0].Type);
}
```

**Parser test:**
```csharp
[Fact]
public void TestParseIfStatement()
{
    var tokens = new Lexer("if x > 0:\n    print(x)", logger).TokenizeAll();
    var parser = new Parser(tokens, logger);
    var module = parser.ParseModule();
    Assert.IsType<IfStmt>(module.Body[0]);
}
```

**Integration test:**
```csharp
[Fact]
public void CompileAndExecute()
{
    var source = "print(1 + 2)";
    var result = CompileAndExecute(source);  // Uses IntegrationTestBase
    Assert.Equal("3\n", result.StandardOutput);
}
```

## CRITICAL Rules

1. **Never change test expectations to match bugs** - Fix the implementation
2. **Skip with reason if blocked:**
   ```csharp
   [Fact(Skip = "TODO: Implement tuple unpacking. See issue #42")]
   ```
3. **Test names describe behavior:** `TestParser_Parses_IfElseStatement`

## Integration Test Base

`IntegrationTestBase` compiles Sharpy → C# → IL → executes in-memory:
```csharp
public class MyTests : IntegrationTestBase
{
    [Fact]
    public void MyFeature_Works()
    {
        var result = CompileAndExecute("print(42)");
        Assert.True(result.Success);
        Assert.Equal("42\n", result.StandardOutput);
    }
}
```

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
