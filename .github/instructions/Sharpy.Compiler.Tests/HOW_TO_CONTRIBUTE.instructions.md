# Contributing to Sharpy.Compiler.Tests

## Overview

**Sharpy.Compiler.Tests** contains comprehensive tests for all compiler components: Lexer, Parser, Semantic Analyzer, Code Generator, Discovery, and Integration tests.

**Location:** `src/Sharpy.Compiler.Tests/`

**Test Coverage:** 1,391 tests passing, 44 skipped ✅

## What's in This Directory

### Test Organization

```
Sharpy.Compiler.Tests/
├── Lexer/                      # Lexer/tokenization tests (237 tests)
│   ├── LexerTests.cs          # Core lexer tests
│   ├── StringTests.cs         # String literal tests
│   ├── NumberTests.cs         # Numeric literal tests
│   └── OperatorTests.cs       # Operator tokenization
├── Parser/                     # Parser/AST tests (~450 tests)
│   ├── ParserTests.cs         # Core parser tests
│   ├── ExpressionTests.cs     # Expression parsing
│   ├── StatementTests.cs      # Statement parsing
│   └── DeclarationTests.cs    # Declaration parsing
├── Semantic/                   # Semantic analyzer tests
│   ├── TypeCheckerTests.cs    # Type checking
│   ├── NameResolverTests.cs   # Name resolution
│   └── TypeNarrowingTests.cs  # Type narrowing
├── CodeGen/                    # Code generation tests (259 tests)
│   ├── RoslynEmitterTests.cs  # C# code generation
│   ├── TypeMapperTests.cs     # Type mapping
│   └── NameManglerTests.cs    # Name mangling
├── Discovery/                  # Module discovery tests
│   └── ModuleDiscoveryTests.cs
├── Integration/                # End-to-end tests (56 tests)
│   ├── BasicProgramTests.cs   # Simple programs
│   ├── ClassTests.cs          # Class compilation
│   └── ModuleTests.cs         # Multi-module programs
├── Performance/                # Performance benchmarks
│   └── CachingTests.cs        # Module caching performance
└── ProjectCompilationTests.cs # .spyproj compilation
```

## How to Build

```bash
# From repository root
dotnet build src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj

# From Sharpy.Compiler.Tests directory
cd src/Sharpy.Compiler.Tests
dotnet build
```

## How to Run Tests

### Run All Tests
```bash
# From repository root
dotnet test src/Sharpy.Compiler.Tests

# Expected: 1,391 passed, 44 skipped, 0 failed
```

### Run Tests by Component

```bash
# Lexer tests only (237 tests)
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "Namespace~Sharpy.Compiler.Tests.Lexer"

# Parser tests only (~450 tests)
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "Namespace~Sharpy.Compiler.Tests.Parser"

# Semantic analyzer tests
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "Namespace~Sharpy.Compiler.Tests.Semantic"

# Code generation tests (259 tests)
dotnet test --filter "FullyQualifiedName~CodeGen"
dotnet test --filter "Namespace~Sharpy.Compiler.Tests.CodeGen"

# Integration tests (56 tests)
dotnet test --filter "FullyQualifiedName~Integration"
dotnet test --filter "Namespace~Sharpy.Compiler.Tests.Integration"

# Discovery tests
dotnet test --filter "FullyQualifiedName~Discovery"

# Performance tests
dotnet test --filter "FullyQualifiedName~Performance"
```

### Run Specific Tests

```bash
# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestIfStatement"
dotnet test --filter "FullyQualifiedName~TestFStringInterpolation"

# Run tests matching a pattern
dotnet test --filter "DisplayName~String"
dotnet test --filter "DisplayName~Class"
```

### Debugging Tests

```bash
# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run a single test for debugging
dotnet test --filter "FullyQualifiedName~SpecificTestName" --logger "console;verbosity=detailed"
```

## Important Things to Note

### Test Categories

**Unit Tests:**
- Test single components in isolation
- Fast execution
- Mock dependencies
- Examples: `LexerTests`, `TypeMapperTests`

**Integration Tests:**
- Test entire compilation pipeline
- End-to-end scenarios
- Real dependencies
- Examples: `BasicProgramTests`, `ClassTests`

**Performance Tests:**
- Measure optimization effectiveness
- Compare with/without caching
- Track regression
- Examples: `CachingTests`

### Testing Best Practices

**CRITICAL: Rules for Writing Tests**

1. **NEVER artificially make tests pass:**
   ```csharp
   // ❌ WRONG: Changing expected value to match bug
   [Fact]
   public void TestAddition()
   {
       var result = Compile("x = 1 + 1");
       Assert.Equal("x = 1 + 2", result);  // BUG: Should be "1 + 1"
   }
   
   // ✅ CORRECT: Fix the compiler bug
   [Fact]
   public void TestAddition()
   {
       var result = Compile("x = 1 + 1");
       Assert.Equal("x = 1 + 1", result);  // Fix code generator
   }
   ```

2. **Fix the root cause:**
   - Debug the failing test
   - Identify the bug in the implementation
   - Fix the bug in `Sharpy.Compiler`
   - Verify the test passes
   - Check for regressions in related tests

3. **Mark tests as skipped ONLY when appropriate:**
   ```csharp
   [Fact(Skip = "TODO: Implement tuple unpacking in parser. Not in AST yet. See issue #42")]
   public void TestTupleUnpacking()
   {
       var source = "a, b = 1, 2";
       var result = Parse(source);
       // Test implementation...
   }
   ```
   
   **When to skip:**
   - Feature not yet implemented (planned for future)
   - Blocked by another issue
   - Known limitation documented in specs
   
   **Include:**
   - Specific reason ("tuple unpacking not in AST")
   - What needs to happen ("implement in parser")
   - Issue reference if available ("See issue #42")

4. **Write tests that match Sharpy's semantics:**
   - Consult language reference in `docs/specs/`
   - Match Python behavior where applicable
   - Document intentional differences from Python

### Test Naming Conventions

```csharp
// Good test names - describe what is being tested
[Fact]
public void TestLexer_Tokenizes_SingleLineString()
public void TestParser_Parses_IfElseStatement()
public void TestCodeGen_Generates_FStringInterpolation()
public void TestSemantic_Reports_TypeMismatch()

// Integration tests - describe the scenario
[Fact]
public void Compile_BasicProgram_WithFunctions()
public void Compile_ClassWithMethods()
public void Compile_ModuleWithImports()
```

### Common Testing Patterns

**Lexer Tests:**
```csharp
[Fact]
public void TestTokenizeIdentifier()
{
    var source = "hello_world";
    var lexer = new Lexer(source);
    var tokens = lexer.Tokenize();
    
    Assert.Equal(2, tokens.Count);  // identifier + EOF
    Assert.Equal(TokenType.Identifier, tokens[0].Type);
    Assert.Equal("hello_world", tokens[0].Value);
}
```

**Parser Tests:**
```csharp
[Fact]
public void TestParseIfStatement()
{
    var source = """
        if x > 0:
            print("positive")
        """;
    var parser = new Parser(source);
    var module = parser.Parse();
    
    Assert.Single(module.Body);
    var ifStmt = Assert.IsType<IfStmt>(module.Body[0]);
    Assert.IsType<Compare>(ifStmt.Test);
    Assert.Single(ifStmt.Body);
}
```

**Semantic Tests:**
```csharp
[Fact]
public void TestTypeChecker_ReportsTypeMismatch()
{
    var source = """
        x: int = "not an int"
        """;
    
    var analyzer = new SemanticAnalyzer();
    var ex = Assert.Throws<SemanticException>(() => analyzer.Analyze(source));
    Assert.Contains("type mismatch", ex.Message.ToLower());
}
```

**Code Generation Tests:**
```csharp
[Fact]
public void TestGenerateFString()
{
    var source = """
        name = "World"
        message = f"Hello, {name}!"
        """;
    
    var generated = CompileToCS(source);
    Assert.Contains("$\"Hello, {name}!\"", generated);
}
```

**Integration Tests:**
```csharp
[Fact]
public void CompileBasicProgram()
{
    var source = """
        def greet(name: str) -> str:
            return f"Hello, {name}!"
        
        result = greet("World")
        print(result)
        """;
    
    var assembly = Compile(source);
    Assert.NotNull(assembly);
    
    // Optionally: Execute and verify output
    var output = Execute(assembly);
    Assert.Equal("Hello, World!", output.Trim());
}
```

## Common Development Tasks

### Adding Tests for a New Feature

1. **Start with lexer tests** (if new syntax):
   ```csharp
   [Fact]
   public void TestNewKeyword()
   {
       var tokens = Tokenize("newkeyword");
       Assert.Equal(TokenType.NewKeyword, tokens[0].Type);
   }
   ```

2. **Add parser tests** (if new AST nodes):
   ```csharp
   [Fact]
   public void TestParseNewStatement()
   {
       var ast = Parse("newkeyword x");
       Assert.IsType<NewStmt>(ast.Body[0]);
   }
   ```

3. **Add semantic tests** (type checking):
   ```csharp
   [Fact]
   public void TestNewStatement_TypeChecking()
   {
       var analyzed = Analyze("newkeyword x: int");
       Assert.Equal(typeof(int), analyzed.GetSymbol("x").Type);
   }
   ```

4. **Add code generation tests**:
   ```csharp
   [Fact]
   public void TestGenerateNewStatement()
   {
       var cs = CompileToCS("newkeyword x");
       Assert.Contains("expected C# code", cs);
   }
   ```

5. **Add integration test**:
   ```csharp
   [Fact]
   public void CompileProgramWithNewStatement()
   {
       var assembly = Compile("newkeyword x");
       Assert.NotNull(assembly);
   }
   ```

### Debugging a Failing Test

1. **Isolate the test:**
   ```bash
   dotnet test --filter "FullyQualifiedName~FailingTestName"
   ```

2. **Add verbose logging:**
   ```csharp
   [Fact]
   public void FailingTest()
   {
       var source = "...";
       Console.WriteLine($"Source: {source}");
       
       var result = Process(source);
       Console.WriteLine($"Result: {result}");
       
       Assert.Equal(expected, result);
   }
   ```

3. **Use debugger:**
   - Set breakpoints in test and implementation
   - Step through execution
   - Inspect variables

4. **Check related tests:**
   ```bash
   dotnet test --filter "FullyQualifiedName~SimilarTest"
   ```

### Handling Skipped Tests

**Current skipped tests (44 total):**
- Features not yet implemented (tuple unpacking, generic function params)
- Planned for future versions (v1.0+)
- Edge cases that need design decisions

**When reviewing skipped tests:**
1. Read the skip reason
2. Check if the feature is now implemented
3. If yes, remove the `Skip` attribute and verify the test passes
4. If no, leave skipped with clear reason

### Adding Performance Tests

```csharp
[Fact]
public void TestModuleCaching_Performance()
{
    var stopwatch = Stopwatch.StartNew();
    
    // First compilation - cache miss
    Compile(sourceWithImports);
    var firstTime = stopwatch.Elapsed;
    
    stopwatch.Restart();
    
    // Second compilation - cache hit
    Compile(sourceWithImports);
    var secondTime = stopwatch.Elapsed;
    
    // Should be at least 2x faster with caching
    Assert.True(secondTime < firstTime / 2);
}
```

## Test Data Organization

### Test Files
- Keep test Sharpy source in strings (use raw strings `@"..."` or triple-quoted `"""..."""`)
- For large files, consider `TestData/` directory (create if needed)
- Use consistent formatting in test source code

### Assertions
- Use specific assertions: `Assert.Equal()`, `Assert.IsType<T>()`, not `Assert.True()`
- Provide helpful failure messages:
  ```csharp
  Assert.Equal(expected, actual, $"Type mismatch for symbol {symbolName}");
  ```

## Dependencies

- **Sharpy.Compiler** - Component under test
- **xUnit** - Testing framework
- **Microsoft.NET.Test.Sdk** - Test SDK
- **.NET 9.0/10.0** - Runtime

## Related Documentation

- **Compiler Guide:** `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`
- **Language Reference:** `docs/specs/language_reference.md`
- **Test Strategy:** (Look at existing tests for patterns)

## Known Issues and Limitations

- 44 tests currently skipped (documented with reasons)
- Some edge cases need additional coverage
- Performance tests could be more comprehensive

## Getting Help

- Look at existing tests for patterns
- Check what similar features do
- Consult language specs for expected behavior
- Run related tests to see what already works
