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
    var lexer = new Lexer("hello_world");
    var tokens = lexer.Tokenize();
    Assert.Equal(TokenType.Identifier, tokens[0].Type);
}
```

**Parser test:**
```csharp
[Fact]
public void TestParseIfStatement()
{
    var parser = new Parser("if x > 0:\n    print(x)");
    var module = parser.Parse();
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
    protected ExecutionResult CompileAndExecute(string source) { ... }
}
```
