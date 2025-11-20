# Contributing to Sharpy.Compiler

## Overview

**Sharpy.Compiler** is the core compiler implementation for the Sharpy programming language. It transforms Sharpy source code into executable .NET assemblies through a multi-stage pipeline: Lexer → Parser → Semantic Analyzer → Code Generator.

**Location:** `src/Sharpy.Compiler/`

## What's in This Directory

### Directory Structure

```
Sharpy.Compiler/
├── Lexer/                 # Tokenization
│   ├── Lexer.cs          # Main lexer implementation
│   ├── Token.cs          # Token data structure
│   └── LexerError.cs     # Lexer error handling
├── Parser/                # AST generation
│   ├── Parser.cs         # Recursive descent parser
│   ├── ParserError.cs    # Parser error handling
│   ├── AstDumper.cs      # AST visualization
│   └── Ast/              # AST node definitions
├── Semantic/              # Type checking and analysis
│   ├── SemanticAnalyzer.cs
│   ├── TypeChecker.cs
│   ├── NameResolver.cs
│   └── SymbolTable.cs
├── CodeGen/               # C# code generation
│   ├── RoslynEmitter.cs  # Main code generator (using Roslyn)
│   ├── TypeMapper.cs     # Sharpy → C# type mapping
│   ├── NameMangler.cs    # Name collision handling
│   ├── CodeValidator.cs  # Validation of generated code
│   └── CodeGenContext.cs # Code generation context
├── Discovery/             # Module and import resolution
│   ├── ModuleDiscovery.cs
│   └── ModuleCache.cs
├── Diagnostics/           # Error and warning reporting
├── Logging/               # Compiler logging
├── Compiler.cs            # Single-file compilation
├── AssemblyCompiler.cs    # Multi-file/project compilation
└── ProjectConfig.cs       # .spyproj file parsing
```

### Key Components

#### Lexer (Tokenization)
- **Responsibility:** Convert source text into tokens
- **Status:** ✅ Complete (237 passing tests)
- **Features:**
  - All operators, keywords, literals (int, float, string)
  - F-strings, raw strings, triple-quoted strings
  - Indentation handling (significant whitespace)
  - Line continuation, comments
  - Binary/hex/octal literals, scientific notation

#### Parser (AST Generation)
- **Responsibility:** Convert tokens into Abstract Syntax Tree
- **Status:** ✅ Complete (~450 passing tests)
- **Features:**
  - All expression types (literals, operators, calls, indexing, slicing, lambdas)
  - All statement types (assignments, control flow, exception handling)
  - All declarations (functions, classes, structs, interfaces, enums)
  - Decorators, modifiers, imports, type annotations
  - Error recovery and reporting

#### Semantic Analyzer
- **Responsibility:** Type checking, name resolution, semantic validation
- **Status:** ✅ Complete (comprehensive test coverage)
- **Features:**
  - Type inference and checking
  - Name resolution with cross-scope lookup
  - Type narrowing (`is not None`, `isinstance()`)
  - Import resolution (.NET and Sharpy modules)
  - Symbol tables with scoped resolution
  - Definite assignment analysis

#### Code Generator
- **Responsibility:** Generate C# code from validated AST
- **Status:** ✅ 95% Complete (259 passing tests)
- **Features:**
  - All P0 (critical) features: 21/21 ✅
  - All P1 (important) features: 9/9 ✅
  - Operator overload synthesis from dunder methods
  - Constructor generation from `__init__`
  - F-string interpolation
  - Name mangling with collision detection
  - Full type mapping (primitives, collections, generics, nullables)

#### Discovery (Module Resolution)
- **Responsibility:** Find and cache imported modules
- **Status:** ✅ Complete with 4-7x performance improvement
- **Features:**
  - Cross-module imports
  - Module caching
  - `__init__.spy` package support
  - .NET assembly imports

## How to Build

```bash
# From repository root
dotnet build src/Sharpy.Compiler/Sharpy.Compiler.csproj

# From Sharpy.Compiler directory
cd src/Sharpy.Compiler
dotnet build

# Build in watch mode (auto-rebuild on changes)
dotnet watch --project src/Sharpy.Compiler
```

## How to Test

```bash
# Run all compiler tests
dotnet test src/Sharpy.Compiler.Tests

# Run tests for specific component
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "FullyQualifiedName~CodeGen"
dotnet test --filter "FullyQualifiedName~Integration"

# Run tests by namespace
dotnet test --filter "Namespace~Sharpy.Compiler.Tests.Lexer"
dotnet test --filter "Namespace~Sharpy.Compiler.Tests.Parser"
```

### Test Results (Expected)
- **Total:** 1,391 tests
- **Passing:** 1,391
- **Skipped:** 44 (marked with reason)
- **Failed:** 0

## Important Things to Note

### Test Writing Best Practices

**CRITICAL:** When writing or modifying tests:

1. **NEVER artificially make tests pass** by:
   - Changing inputs to match buggy outputs
   - Removing assertions that fail
   - Modifying expected values without understanding why
   - Commenting out failing tests

2. **ALWAYS fix the root cause:**
   - Debug why the test fails
   - Fix the implementation bug
   - Verify the fix resolves the issue
   - Run related tests to ensure no regressions

3. **Mark as skipped ONLY when necessary:**
   ```csharp
   [Fact(Skip = "TODO: Fix parser handling of tuple unpacking. Not in AST yet.")]
   public void TestTupleUnpacking()
   {
       // Test code that currently fails due to missing feature
   }
   ```
   - Include specific reason
   - Reference issue number if available
   - Describe what needs to be implemented/fixed

### Architecture Principles

**Compilation Pipeline:**
```
Source Code
    ↓
Lexer (Tokenization)
    ↓
Parser (AST Generation)
    ↓
Semantic Analyzer (Type Checking)
    ↓
Code Generator (C# Code via Roslyn)
    ↓
C# Compiler (via Roslyn)
    ↓
.NET Assembly
```

**Key Design Decisions:**
- **Immutable AST nodes** - Easier to reason about, thread-safe
- **Visitor pattern** for AST traversal
- **Symbol tables** for name resolution and type checking
- **Roslyn** for C# code generation and compilation
- **Incremental compilation** via module caching

### Common Patterns

**Error Handling:**
```csharp
// Lexer/Parser errors - collect all and report
var errors = new List<LexerError>();
// ... lexing/parsing ...
if (errors.Any())
    throw new CompilationException(errors);

// Semantic errors - fail fast or collect
if (!typeMatches)
    throw new SemanticException($"Type mismatch: expected {expected}, got {actual}");
```

**AST Visitor Pattern:**
```csharp
public override T Visit(FunctionDef node)
{
    // Process function
    foreach (var stmt in node.Body)
        Visit(stmt);
    return result;
}
```

## Common Development Tasks

### Adding a New Language Feature

**Example: Adding a new operator**

1. **Lexer:** Add token type to `Token.cs` and recognition to `Lexer.cs`
2. **Parser:** Add AST node and parsing logic to `Parser.cs`
3. **Semantic Analyzer:** Add type checking rules
4. **Code Generator:** Add C# code generation in `RoslynEmitter.cs`
5. **Tests:** Add tests for each component
6. **Documentation:** Update language reference

### Fixing a Bug

1. **Write a failing test** that reproduces the bug
2. **Debug** to find the root cause
3. **Fix** the implementation
4. **Verify** the test now passes
5. **Run all tests** to check for regressions
6. **Do NOT** just change the test to pass without fixing the bug

### Adding a Test

```csharp
[Fact]
public void TestNewFeature()
{
    // Arrange
    var source = "test code";
    var lexer = new Lexer(source);
    
    // Act
    var tokens = lexer.Tokenize();
    
    // Assert
    Assert.Equal(expectedTokenCount, tokens.Count);
    Assert.Equal(TokenType.Identifier, tokens[0].Type);
}
```

### Debugging Compilation

```csharp
// Enable detailed logging
var options = new CompilerOptions { Verbose = true };
var compiler = new Compiler(options);

// Use AstDumper to visualize AST
var dumper = new AstDumper();
var astString = dumper.Dump(module);
Console.WriteLine(astString);
```

## Performance Considerations

- **Module caching** - Reuse parsed/analyzed modules (4-7x speedup)
- **Lazy evaluation** - Don't analyze unused modules
- **Incremental compilation** - Only recompile changed files (planned)
- **Parallel processing** - Process independent modules concurrently (planned)

## Dependencies

- **Microsoft.CodeAnalysis.CSharp** (Roslyn) - C# code generation and compilation
- **Sharpy.Core** - Standard library references
- **.NET 9.0/10.0** - Runtime and BCL

## Known Limitations

- **Tuple unpacking** - Not in AST yet (parser enhancement needed)
- **Generic function type parameters** - Not in AST yet
- **Properties** - Deferred to v1.0
- **Comprehensions** - Deferred to v1.0
- **Pattern matching** - Deferred to v1.0

## Related Documentation

- **Main README:** `README.md` (repository root)
- **Compiler Tests Guide:** `.github/instructions/Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md`
- **Language Reference:** `docs/specs/language_reference.md`
- **Architecture Docs:** `docs/architecture/`

## Example: Adding Support for a New Operator

Let's walk through adding support for the `@` matrix multiplication operator:

### 1. Lexer (Tokenization)
```csharp
// In Token.cs
public enum TokenType
{
    // ... existing tokens ...
    At,  // @
}

// In Lexer.cs
case '@':
    AddToken(TokenType.At);
    Advance();
    break;
```

### 2. Parser (AST)
```csharp
// In Parser.cs
private Expr ParseBinaryOp()
{
    // ... existing operators ...
    if (Match(TokenType.At))
    {
        var op = Previous();
        var right = ParseUnary();
        return new BinaryOp(left, op, right);
    }
}
```

### 3. Semantic Analyzer
```csharp
// In SemanticAnalyzer.cs
case TokenType.At:
    // Verify operands support matrix multiplication
    ValidateMatrixMultiplication(node.Left, node.Right);
    break;
```

### 4. Code Generator
```csharp
// In RoslynEmitter.cs
case TokenType.At:
    // Generate C# code for matrix multiplication
    return MatrixMultiply(left, right);
```

### 5. Tests
```csharp
[Fact]
public void TestMatrixMultiplication()
{
    var source = "a @ b";
    var lexer = new Lexer(source);
    var tokens = lexer.Tokenize();
    Assert.Contains(tokens, t => t.Type == TokenType.At);
}
```

## Getting Help

- Review existing similar features for patterns
- Check tests for examples of usage
- Consult architecture documentation in `docs/architecture/`
- Look at integration tests for end-to-end examples
