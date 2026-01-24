# Walkthrough: CompilationUnitFactory.cs

**Source File**: `src/Sharpy.Compiler/Model/CompilationUnitFactory.cs`

---

## Overview

`CompilationUnitFactory` is a static factory class that serves as the primary orchestrator for the early stages of the Sharpy compilation pipeline. It provides convenient methods for:

1. **Creating** compilation units from source files
2. **Lexing** source text into tokens
3. **Parsing** tokens into an Abstract Syntax Tree (AST)
4. **Computing** module paths from file system locations
5. **Managing** compilation unit dependencies

Think of this class as a "compilation unit lifecycle manager" that takes a raw `.spy` file and shepherds it through the first two critical phases: **Lexer → Parser**. The factory pattern encapsulates the complexity of creating and initializing `CompilationUnit` objects, ensuring they're properly configured for downstream compilation phases.

### Position in the Compiler Pipeline

```
Source File (.spy)
    ↓
CompilationUnitFactory.CreateFromFile()  ← Loads source, computes module path
    ↓
CompilationUnitFactory.Lex()             ← Tokenizes source
    ↓
CompilationUnitFactory.Parse()           ← Builds AST
    ↓
Semantic Analysis (elsewhere)            ← Type checking, name resolution
    ↓
Code Generation (elsewhere)              ← Emits C#
```

---

## Class Structure

`CompilationUnitFactory` is a **static class** with no state. All methods are pure functions that operate on `CompilationUnit` instances passed as parameters.

### Key Design Decisions

1. **Static Factory Pattern**: No instantiation needed; all methods are class-level
2. **Separation of Concerns**: Each method handles one phase of early compilation
3. **Progressive Enhancement**: Methods build upon each other (Lex → Parse → LexAndParse)
4. **Error Isolation**: Exceptions are caught and converted to diagnostics in the `CompilationUnit`

---

## Key Methods

### 1. `ComputeModulePath(string filePath, string projectRoot)`

**Purpose**: Translates a file system path into a Python-style dotted module path.

**Example**:
```csharp
// Input:
//   filePath:    "/Users/dev/myproject/src/utils/helpers.spy"
//   projectRoot: "/Users/dev/myproject"
// Output:
//   "src.utils.helpers"
```

**Algorithm**:
```csharp
public static string ComputeModulePath(string filePath, string projectRoot)
{
    // 1. Get relative path: "src/utils/helpers.spy"
    var relativePath = Path.GetRelativePath(projectRoot, filePath);
    
    // 2. Remove extension: "src/utils/helpers"
    var withoutExtension = Path.ChangeExtension(relativePath, null);
    
    // 3. Replace directory separators with dots: "src.utils.helpers"
    var modulePath = withoutExtension
        .Replace(Path.DirectorySeparatorChar, '.')
        .Replace(Path.AltDirectorySeparatorChar, '.');
    
    // 4. Clean up leading dots
    while (modulePath.StartsWith('.'))
        modulePath = modulePath.Substring(1);
    
    return modulePath;
}
```

**Key Details**:
- Handles both Unix (`/`) and Windows (`\`) path separators
- Strips file extension (`.spy`)
- Removes leading dots that might occur with relative paths
- Thread-safe (no shared state)

**Connects To**:
- Used by `CreateFromFile()` to populate `CompilationUnit.ModulePath`
- The module path becomes the namespace in generated C# code
- Import statements use these paths to resolve dependencies

---

### 2. `CreateFromFile(string filePath, string projectRoot)`

**Purpose**: Factory method that creates a fully initialized `CompilationUnit` from a source file.

**What It Does**:
```csharp
public static CompilationUnit CreateFromFile(string filePath, string projectRoot)
{
    // 1. Read entire file contents
    var sourceText = File.ReadAllText(filePath);
    
    // 2. Compute the module path
    var modulePath = ComputeModulePath(filePath, projectRoot);
    
    // 3. Create and return new CompilationUnit
    return new CompilationUnit(filePath, modulePath, sourceText);
}
```

**Side Effects**:
- **File I/O**: Reads from disk (may throw `IOException`)
- **Memory Allocation**: Entire file loaded into memory

**Connects To**:
- Entry point for single-file compilation
- Used by `AssemblyCompiler` for multi-file projects
- The created `CompilationUnit` has `Phase = CompilationPhase.Created`

**Typical Usage**:
```csharp
var unit = CompilationUnitFactory.CreateFromFile(
    "/path/to/myfile.spy",
    "/path/to/project/root"
);
// unit.SourceText contains file contents
// unit.ModulePath = "mypackage.myfile"
// unit.Phase = CompilationPhase.Created
```

---

### 3. `Lex(CompilationUnit unit, ICompilerLogger? logger = null)`

**Purpose**: Performs **lexical analysis** (tokenization) on a compilation unit's source text.

**How It Works**:
```csharp
public static bool Lex(CompilationUnit unit, ICompilerLogger? logger = null)
{
    ArgumentNullException.ThrowIfNull(unit);
    
    try
    {
        // 1. Create lexer with source text
        var lexer = new Lexer.Lexer(unit.SourceText, logger ?? NullLogger.Instance);
        
        // 2. Tokenize entire source
        var tokens = lexer.TokenizeAll();
        
        // 3. Store tokens in unit
        unit.Tokens = tokens;
        
        // 4. Advance phase
        unit.Phase = CompilationPhase.Lexed;
        
        return true;
    }
    catch (LexerError ex)
    {
        // 5. Capture error diagnostics
        unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, unit.FilePath);
        unit.Phase = CompilationPhase.Failed;
        return false;
    }
}
```

**Key Points**:
- **Mutates the CompilationUnit**: Sets `unit.Tokens` and `unit.Phase`
- **Returns Boolean**: `true` = success, `false` = errors occurred
- **Error Handling**: Catches `LexerError` and stores it in `unit.Diagnostics`
- **Logger**: Optional; uses `NullLogger.Instance` if not provided

**What Gets Created**:
```csharp
// unit.Tokens contains:
List<Token> {
    Token { Type = TokenType.Def, Lexeme = "def", Line = 1, Column = 0 },
    Token { Type = TokenType.Identifier, Lexeme = "main", Line = 1, Column = 4 },
    Token { Type = TokenType.LeftParen, Lexeme = "(", Line = 1, Column = 8 },
    // ... more tokens
}
```

**Connects To**:
- **Upstream**: Requires `unit.SourceText` to be populated
- **Downstream**: `Parse()` requires `unit.Tokens`
- **Lexer Component**: `src/Sharpy.Compiler/Lexer/Lexer.cs`

---

### 4. `Parse(CompilationUnit unit, ICompilerLogger? logger = null)`

**Purpose**: Performs **parsing** to build an Abstract Syntax Tree (AST) from tokens.

**Precondition**: `Lex()` must be called first to populate `unit.Tokens`.

**How It Works**:
```csharp
public static bool Parse(CompilationUnit unit, ICompilerLogger? logger = null)
{
    ArgumentNullException.ThrowIfNull(unit);
    
    // 1. Validate preconditions
    if (unit.Tokens == null)
        throw new InvalidOperationException("Cannot parse without tokens. Call Lex() first.");
    
    try
    {
        // 2. Create parser with token stream
        var parser = new Parser.Parser(unit.Tokens.ToList(), logger ?? NullLogger.Instance);
        
        // 3. Parse into Module (top-level AST node)
        var ast = parser.ParseModule();
        unit.Ast = ast;
        
        // 4. Extract import statements
        var imports = new List<ImportStatement>();
        var fromImports = new List<FromImportStatement>();
        
        foreach (var statement in ast.Body)
        {
            if (statement is ImportStatement import)
                imports.Add(import);
            else if (statement is FromImportStatement fromImport)
                fromImports.Add(fromImport);
        }
        
        // 5. Store imports for dependency resolution
        unit.Imports = imports;
        unit.FromImports = fromImports;
        
        // 6. Advance phase
        unit.Phase = CompilationPhase.Parsed;
        return true;
    }
    catch (ParserError ex)
    {
        // 7. Capture error diagnostics
        unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, unit.FilePath);
        unit.Phase = CompilationPhase.Failed;
        return false;
    }
}
```

**Key Points**:
- **Import Extraction**: Walks the top-level AST body to find import statements
- **Validation**: Throws if called before `Lex()`
- **AST Storage**: `unit.Ast` is a `Module` record (immutable)
- **Phase Tracking**: Sets `Phase = CompilationPhase.Parsed` on success

**What Gets Created**:
```csharp
// unit.Ast (Module) contains:
Module {
    Body = [
        ImportStatement { Module = "sys" },
        FunctionDef {
            Name = "main",
            Parameters = [ Parameter { Name = "args", Type = "list[str]" } ],
            Body = [ ... ]
        }
    ]
}

// unit.Imports contains:
[ ImportStatement { Module = "sys" } ]

// unit.FromImports contains:
[ FromImportStatement { Module = "collections", Names = ["defaultdict"] } ]
```

**Connects To**:
- **Upstream**: Requires `unit.Tokens` from `Lex()`
- **Downstream**: Semantic analysis uses `unit.Ast`, `unit.Imports`, `unit.FromImports`
- **Parser Component**: `src/Sharpy.Compiler/Parser/Parser.cs`
- **AST Nodes**: `src/Sharpy.Compiler/Parser/Ast/*.cs`

---

### 5. `LexAndParse(CompilationUnit unit, ICompilerLogger? logger = null)`

**Purpose**: Convenience method that performs both lexing and parsing in sequence.

**Implementation**:
```csharp
public static bool LexAndParse(CompilationUnit unit, ICompilerLogger? logger = null)
{
    return Lex(unit, logger) && Parse(unit, logger);
}
```

**Short-Circuit Behavior**:
- If `Lex()` returns `false`, `Parse()` is **not called** (C# `&&` short-circuits)
- Returns `true` only if both phases succeed
- Errors from either phase are stored in `unit.Diagnostics`

**Typical Usage**:
```csharp
var unit = CompilationUnitFactory.CreateFromFile(filePath, projectRoot);

if (CompilationUnitFactory.LexAndParse(unit, logger))
{
    // Success: unit.Ast is available
    ProcessAst(unit.Ast);
}
else
{
    // Failure: check unit.Diagnostics for errors
    foreach (var error in unit.Diagnostics.GetErrors())
        Console.WriteLine(error);
}
```

---

### 6. `SetDependencies(CompilationUnit unit, IEnumerable<string> dependencies)`

**Purpose**: Populates the compilation unit's dependency list after import resolution.

**Implementation**:
```csharp
public static void SetDependencies(CompilationUnit unit, IEnumerable<string> dependencies)
{
    ArgumentNullException.ThrowIfNull(unit);
    ArgumentNullException.ThrowIfNull(dependencies);
    
    unit.DirectDependencies = dependencies.ToImmutableHashSet();
}
```

**When It's Called**:
- **After** import resolution determines which files this unit depends on
- Typically during the semantic analysis phase
- Used by build systems to determine compilation order

**Example**:
```csharp
// After resolving imports in "main.spy":
//   import utils.helpers
//   from data import models

SetDependencies(unit, new[] {
    "/project/src/utils/helpers.spy",
    "/project/src/data/models.spy"
});

// unit.DirectDependencies now contains these file paths
```

**Why Immutable**:
- Uses `ImmutableHashSet` for thread-safety
- Prevents accidental modification during parallel compilation
- Ensures dependency graph integrity

---

## Dependencies

### Internal Sharpy Dependencies

```csharp
using Sharpy.Compiler.Lexer;           // Lexer class, Token types
using Sharpy.Compiler.Parser;          // Parser class
using Sharpy.Compiler.Parser.Ast;      // AST node types (Module, ImportStatement, etc.)
using Sharpy.Compiler.Logging;         // ICompilerLogger, NullLogger
```

### .NET Framework Dependencies

```csharp
using System.Collections.Immutable;    // ImmutableHashSet for dependencies
using System.IO;                       // File I/O, path operations
```

### Component Connections

| Component | How It Connects |
|-----------|----------------|
| **CompilationUnit** | Data structure modified by factory methods |
| **Lexer** | `Lex()` creates and invokes `Lexer.Lexer` |
| **Parser** | `Parse()` creates and invokes `Parser.Parser` |
| **DiagnosticBag** | Errors stored in `unit.Diagnostics` |
| **AssemblyCompiler** | Uses `CreateFromFile()` and `LexAndParse()` |

---

## Patterns and Design Decisions

### 1. Static Factory Pattern

**Why Static?**
- No per-instance state needed
- Simple API for consumers
- Clear separation from `CompilationUnit` itself

**Benefits**:
```csharp
// Simple, discoverable API:
var unit = CompilationUnitFactory.CreateFromFile(path, root);
CompilationUnitFactory.LexAndParse(unit);

// vs. requiring instantiation:
var factory = new CompilationUnitFactory(config, options);
var unit = factory.CreateFromFile(path, root);
```

### 2. Progressive Enhancement

Methods build on each other in a natural pipeline:

```
CreateFromFile()  →  Lex()  →  Parse()
                       ↓         ↓
                    Tokens     AST
```

Each method advances the `CompilationPhase` enum:
```
Created → Lexed → Parsed → ... (continued elsewhere)
```

### 3. Error Handling Strategy

**Catch and Convert**:
```csharp
try {
    var tokens = lexer.TokenizeAll();
    unit.Tokens = tokens;
    return true;
}
catch (LexerError ex) {
    unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, unit.FilePath);
    unit.Phase = CompilationPhase.Failed;
    return false;
}
```

**Why This Approach?**
- Compilation can continue for other files in a multi-file project
- All diagnostics centralized in `unit.Diagnostics`
- Boolean return allows `if (Lex(unit)) { ... }` patterns
- No exceptions escape to caller

### 4. Null Safety and Validation

Every public method validates arguments:
```csharp
ArgumentNullException.ThrowIfNull(unit);
ArgumentNullException.ThrowIfNull(dependencies);
```

Parser precondition enforced explicitly:
```csharp
if (unit.Tokens == null)
    throw new InvalidOperationException("Cannot parse without tokens. Call Lex() first.");
```

### 5. Optional Logger Pattern

```csharp
public static bool Lex(CompilationUnit unit, ICompilerLogger? logger = null)
{
    var effectiveLogger = logger ?? NullLogger.Instance;
    // ...
}
```

- Callers can omit logger for simple cases
- `NullLogger.Instance` provides no-op implementation
- No null checks needed throughout method

---

## Debugging Tips

### 1. Inspecting Compilation Phases

Add breakpoints after each factory method call and check `unit.Phase`:

```csharp
var unit = CompilationUnitFactory.CreateFromFile(path, root);
// unit.Phase == CompilationPhase.Created

CompilationUnitFactory.Lex(unit, logger);
// unit.Phase == CompilationPhase.Lexed (or Failed)

CompilationUnitFactory.Parse(unit, logger);
// unit.Phase == CompilationPhase.Parsed (or Failed)
```

### 2. Examining Diagnostics

If a method returns `false`, check the diagnostic bag:

```csharp
if (!CompilationUnitFactory.Lex(unit, logger))
{
    foreach (var diag in unit.Diagnostics.GetErrors())
    {
        Console.WriteLine($"{diag.FilePath}:{diag.Line}:{diag.Column} - {diag.Message}");
    }
}
```

### 3. Verifying Token Output

After `Lex()`, inspect the token stream:

```csharp
CompilationUnitFactory.Lex(unit, logger);

foreach (var token in unit.Tokens ?? Enumerable.Empty<Token>())
{
    Console.WriteLine($"{token.Type,-20} {token.Lexeme,-15} L{token.Line}:C{token.Column}");
}
```

### 4. Visualizing the AST

After `Parse()`, use the AST visitor pattern:

```csharp
CompilationUnitFactory.Parse(unit, logger);

if (unit.Ast != null)
{
    var visitor = new AstPrinter();
    visitor.Visit(unit.Ast);
}
```

### 5. Common Issues

| Issue | Symptoms | Solution |
|-------|----------|----------|
| **Parse called before Lex** | `InvalidOperationException` | Always call `Lex()` first or use `LexAndParse()` |
| **File not found** | `IOException` from `CreateFromFile()` | Check file path and permissions |
| **Module path has leading dots** | `.src.main` instead of `src.main` | Handled automatically by `ComputeModulePath()` |
| **Silent failures** | Returns `false` but no visible errors | Check `unit.Diagnostics` collection |

### 6. CLI Debugging Commands

Use the Sharpy CLI to debug factory behavior:

```bash
# View tokens (tests Lex)
dotnet run --project src/Sharpy.Cli -- emit tokens myfile.spy

# View AST (tests LexAndParse)
dotnet run --project src/Sharpy.Cli -- emit ast myfile.spy

# View diagnostics
dotnet run --project src/Sharpy.Cli -- build myfile.spy 2>&1 | grep error
```

---

## Contribution Guidelines

### When to Modify This File

✅ **Good reasons to change `CompilationUnitFactory`**:
- Adding new factory methods for different input sources (e.g., `CreateFromString()`)
- Enhancing import extraction logic in `Parse()`
- Adding new compilation phases (update phase progression)
- Improving error messages or diagnostic information
- Adding telemetry/metrics collection

❌ **Don't change this file for**:
- Lexer bugs → Fix in `src/Sharpy.Compiler/Lexer/`
- Parser bugs → Fix in `src/Sharpy.Compiler/Parser/`
- AST structure changes → Fix in `src/Sharpy.Compiler/Parser/Ast/`
- Semantic analysis → Belongs in `src/Sharpy.Compiler/Semantic/`

### Adding a New Factory Method

**Example**: Creating units from in-memory strings (for testing):

```csharp
/// <summary>
/// Creates a CompilationUnit from in-memory source text.
/// </summary>
public static CompilationUnit CreateFromText(
    string sourceText,
    string modulePath = "test_module")
{
    return new CompilationUnit(
        filePath: $"<string:{modulePath}>",
        modulePath: modulePath,
        sourceText: sourceText
    );
}
```

**Testing**:
```csharp
[Fact]
public void TestCreateFromText()
{
    var unit = CompilationUnitFactory.CreateFromText("print('hello')", "test");
    Assert.Equal("test", unit.ModulePath);
    Assert.Equal("print('hello')", unit.SourceText);
}
```

### Modifying Import Extraction

The `Parse()` method extracts imports by walking the AST body:

```csharp
foreach (var statement in ast.Body)
{
    if (statement is ImportStatement import)
        imports.Add(import);
    else if (statement is FromImportStatement fromImport)
        fromImports.Add(fromImport);
}
```

**To add a new import type**:
1. Define the AST node in `src/Sharpy.Compiler/Parser/Ast/`
2. Update parser to recognize the syntax
3. Add `else if` branch here to extract it
4. Add corresponding property to `CompilationUnit`

### Testing Changes

Always add tests to `src/Sharpy.Compiler.Tests/`:

```csharp
public class CompilationUnitFactoryTests : IntegrationTestBase
{
    [Fact]
    public void TestComputeModulePath_HandlesNestedDirectories()
    {
        var modulePath = CompilationUnitFactory.ComputeModulePath(
            "/project/src/utils/helpers.spy",
            "/project"
        );
        Assert.Equal("src.utils.helpers", modulePath);
    }
    
    [Fact]
    public void TestLex_InvalidSyntax_ReturnsErrorDiagnostic()
    {
        var unit = CompilationUnitFactory.CreateFromText("def 123invalid");
        var success = CompilationUnitFactory.Lex(unit);
        
        Assert.False(success);
        Assert.True(unit.HasErrors);
        Assert.Equal(CompilationPhase.Failed, unit.Phase);
    }
}
```

### Performance Considerations

- `CreateFromFile()` reads entire file into memory → okay for typical source files (<1MB)
- `Lex()` processes entire source → linear in file size
- `Parse()` builds full AST → quadratic in worst case (deeply nested expressions)
- For large files, consider streaming or incremental compilation

---

## Cross-References

### Related Sharpy Files

| File | Relationship |
|------|--------------|
| **[CompilationUnit.cs](CompilationUnit.md)** | Data structure manipulated by this factory |
| **ProjectModel.cs** | Uses factory to create units in multi-file projects |
| **src/Sharpy.Compiler/Lexer/Lexer.cs** | Invoked by `Lex()` method |
| **src/Sharpy.Compiler/Parser/Parser.cs** | Invoked by `Parse()` method |
| **src/Sharpy.Compiler/AssemblyCompiler.cs** | Orchestrates factory for multi-file compilation |

### Upstream Components (Inputs)

- **Source Files (.spy)**: Loaded by `CreateFromFile()`
- **File System**: Paths used by `ComputeModulePath()`

### Downstream Components (Outputs)

- **Semantic Analysis**: Consumes `unit.Ast`, `unit.Imports`
- **Code Generation**: Uses fully analyzed `CompilationUnit`
- **Build System**: Uses `unit.DirectDependencies` for ordering

### External Documentation

- **Lexer Guide**: `.github/instructions/Sharpy.Compiler/Lexer/HOW_TO_CONTRIBUTE.md`
- **Parser Guide**: `.github/instructions/Sharpy.Compiler/Parser/HOW_TO_CONTRIBUTE.md`
- **Testing Guide**: `.github/instructions/Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.md`

---

## Summary

`CompilationUnitFactory` is the **gateway** to the Sharpy compiler pipeline. It:

1. **Creates** compilation units from files or text
2. **Orchestrates** the Lexer → Parser transformation
3. **Captures** errors in a centralized diagnostic bag
4. **Extracts** import metadata for dependency resolution
5. **Tracks** compilation phases for debugging

**Key Takeaway**: This is where raw `.spy` files become structured data (tokens and AST) ready for semantic analysis. Think of it as the "frontend factory" that prepares compilation units for the "backend" (semantic analysis and code generation).

When debugging compilation issues, start here:
- Check if `Lex()` succeeds → token issues
- Check if `Parse()` succeeds → syntax issues
- Inspect `unit.Diagnostics` → error details
- Verify `unit.Phase` → pipeline progress

The factory pattern keeps the complexity of initialization separate from the `CompilationUnit` data structure, making the codebase more maintainable and testable.
