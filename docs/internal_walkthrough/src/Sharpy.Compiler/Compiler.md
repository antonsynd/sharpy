# Walkthrough: Compiler.cs

**Source File**: `src/Sharpy.Compiler/Compiler.cs`

---

## Overview

The `Compiler.cs` file is the **orchestration hub** of the Sharpy compiler. It acts as the main driver that coordinates all compilation phases, transforming Sharpy source code into executable .NET assemblies. Think of it as the conductor of an orchestra—it doesn't play any instruments itself, but ensures each section (Lexer, Parser, Semantic Analyzer, Code Generator) performs at the right time in the right sequence.

### Primary Responsibilities

1. **Single-file compilation** - Compiles individual `.spy` files to C# code
2. **Multi-file/project compilation** - Compiles entire `.spyproj` projects with multiple source files
3. **Pipeline orchestration** - Coordinates the 4-5 phase compilation pipeline
4. **Error aggregation** - Collects and reports errors from all compilation phases
5. **Metrics tracking** - Monitors performance and timing of each compilation phase
6. **Module management** - Handles external .NET assembly references and module search paths

### Key Insight

There are **two main entry points** in this file:
- `Compile()` - For single `.spy` files (simpler, used in CLI for one-off compilations)
- `CompileProject()` - For `.spyproj` projects (complex, handles multiple files and cross-module references)

Both follow the same multi-phase pipeline, but `CompileProject()` adds cross-module symbol resolution and aggregates results from multiple files.

---

## Class/Type Structure

### Main Class: `Compiler`

```csharp
public class Compiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;
}
```

**Purpose**: Orchestrates the compilation pipeline from source code to .NET assembly.

**State**:
- `_logger` - Logging interface for diagnostics and debugging
- `_moduleRegistry` - Optional registry for managing external .NET module references

**Constructors**:
1. **Simple constructor** - `Compiler(ICompilerLogger? logger = null)`
   - Creates a basic compiler without module registry
   - Used for simple single-file compilations

2. **Full constructor** - `Compiler(CompilerOptions options, ICompilerLogger? logger = null)`
   - Creates compiler with module registry
   - Loads module search paths and external assembly references
   - Used when you need to import third-party .NET libraries

### Data Classes

#### `CompilationResult`

```csharp
public class CompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public Module? Module { get; init; }                    // Parsed AST
    public SymbolTable? SymbolTable { get; init; }         // Resolved symbols
    public SemanticInfo? SemanticInfo { get; init; }       // Type information
    public ModuleRegistry? ModuleRegistry { get; init; }   // External modules
    public string? GeneratedCSharpCode { get; init; }      // Generated C# code
    public CompilationMetrics? Metrics { get; init; }      // Performance metrics
}
```

**Purpose**: Encapsulates the result of a single-file compilation. Contains all artifacts produced during compilation, including errors, AST, symbol table, and generated C# code.

**Key Properties**:
- `Success` - Quick check: did compilation succeed?
- `Errors` - Human-readable error messages (empty if successful)
- `Module` - The Abstract Syntax Tree (AST) representing the parsed code
- `SymbolTable` - Maps identifiers to their types and scopes
- `GeneratedCSharpCode` - The final C# code output (what gets compiled to .NET)

#### `ProjectCompilationResult`

```csharp
public class ProjectCompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string? OutputAssemblyPath { get; init; }              // Path to compiled .dll
    public Dictionary<string, string> GeneratedCSharpFiles { get; init; } = new();  // File -> C# code
    public ProjectCompilationMetrics? Metrics { get; init; }
}
```

**Purpose**: Encapsulates the result of a multi-file project compilation.

**Key Differences from `CompilationResult`**:
- Returns an **assembly path** instead of just C# code
- Includes **warnings** (from the C# compiler)
- Contains a **dictionary** of generated C# files (not just a single string)
- Uses `ProjectCompilationMetrics` which aggregates metrics from all files

#### `CompilerOptions`

```csharp
public class CompilerOptions
{
    public string[]? ModulePaths { get; set; }    // Paths to search for module assemblies
    public string[]? References { get; set; }     // Paths to .NET assemblies to reference
}
```

**Purpose**: Configuration for the compiler, primarily for specifying external dependencies.

**Usage Example**:
```csharp
var options = new CompilerOptions
{
    ModulePaths = new[] { "/usr/lib/sharpy/modules", "./lib" },
    References = new[] { "MyLibrary.dll", "System.Data.dll" }
};
var compiler = new Compiler(options, logger);
```

---

## Key Methods

### `Compile(string sourceCode, string filePath)` - Single File Compilation

**Signature**:
```csharp
public CompilationResult Compile(string sourceCode, string filePath)
```

**Purpose**: Compiles a single Sharpy source file through the complete pipeline, producing C# code ready for compilation.

**Parameters**:
- `sourceCode` - The Sharpy source code as a string
- `filePath` - The path to the source file (used for error reporting and context)

**Returns**: `CompilationResult` containing the AST, symbol table, generated C# code, or errors

**The 4-Phase Pipeline**:

```
Source Code
    ↓
Phase 1: Lexical Analysis (Tokenization)
    ↓
Phase 2: Syntax Analysis (AST Generation)
    ↓
Phase 3: Semantic Analysis (Type Checking)
    ├─ Name Resolution (declarations, inheritance)
    ├─ Type Resolution
    └─ Type Checking
    ↓
Phase 4: Code Generation (C# via Roslyn)
    ↓
CompilationResult
```

**Detailed Phase Breakdown**:

#### Phase 1: Lexical Analysis (Lines 363-368)

```csharp
metrics.StartPhase("Lexical Analysis");
var lexer = new Lexer.Lexer(sourceCode, _logger);
var tokens = lexer.TokenizeAll();
metrics.EndPhase();
```

**What happens**: The source code string is broken into tokens (keywords, identifiers, operators, literals, etc.)

**Example**:
```python
# Input: "x: int = 42"
# Tokens: [IDENTIFIER("x"), COLON, TYPE("int"), EQUALS, NUMBER(42)]
```

#### Phase 2: Syntax Analysis (Lines 370-375)

```csharp
metrics.StartPhase("Syntax Analysis");
var parser = new Parser.Parser(tokens, _logger);
var module = parser.ParseModule();
metrics.EndPhase();
```

**What happens**: Tokens are parsed into an Abstract Syntax Tree (AST) representing the program structure.

**Example**:
```python
# Input: "x: int = 42"
# AST: AnnAssign(target=Name("x"), annotation=Name("int"), value=Constant(42))
```

#### Phase 3: Semantic Analysis (Lines 377-429)

This is the **most complex phase**, broken into multiple sub-passes:

**3a. Initialize Core Components** (Lines 379-381):
```csharp
var builtinRegistry = new BuiltinRegistry();      // Python built-ins: len(), print(), etc.
var symbolTable = new SymbolTable(builtinRegistry);  // Maps names to types
var semanticInfo = new SemanticInfo();            // Stores type narrowing info
```

**3b. Name Resolution** (Lines 395-408):
```csharp
var nameResolver = new NameResolver(symbolTable, _logger);
nameResolver.ResolveDeclarations(module);  // First pass: find all declarations
nameResolver.ResolveInheritance();         // Second pass: resolve class inheritance
```

**Purpose**: Builds the symbol table by:
- Finding all function, class, and variable declarations
- Resolving class inheritance hierarchies
- Checking for duplicate definitions

**Example**:
```python
class Animal:
    pass

class Dog(Animal):  # ResolveInheritance() links Dog -> Animal
    pass
```

**3c. Type Resolution and Checking** (Lines 412-428):
```csharp
var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
typeChecker.CheckModule(module);
```

**Purpose**:
- Infers types where not explicitly annotated
- Validates type correctness (e.g., can't assign `str` to `int`)
- Performs type narrowing (e.g., after `isinstance()` checks)

**Example**:
```python
x: int = 42        # Type checking: 42 is compatible with int ✓
y: str = 42        # Type checking: 42 is NOT compatible with str ✗
```

#### Phase 4: Code Generation (Lines 434-443)

```csharp
var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
{
    SourceFilePath = filePath
};
var emitter = new RoslynEmitter(codeGenContext);
var compilationUnit = emitter.GenerateCompilationUnit(module);
var csharpCode = compilationUnit.ToFullString();
```

**What happens**: The validated AST is transformed into C# code using Roslyn (Microsoft's C# compiler API).

**Example**:
```python
# Sharpy input:
def greet(name: str) -> str:
    return f"Hello, {name}!"

# Generated C# output:
public static string greet(string name)
{
    return $"Hello, {name}!";
}
```

**Important Details**:
- Uses Roslyn's `SyntaxFactory` to build C# syntax trees programmatically
- Handles operator overloads, dunder methods (`__add__`, `__init__`), etc.
- Maps Sharpy types to .NET types (e.g., `list[int]` → `List<int>`)

**Error Handling**:

Each phase can fail. When it does, the method returns early with `Success = false`:

```csharp
if (typeChecker.Errors.Any())
{
    return new CompilationResult
    {
        Success = false,
        Errors = typeChecker.Errors.Select(e => e.Message).ToList(),
        Metrics = metrics
    };
}
```

**Key Insight**: Errors are **accumulated** but compilation **stops** at the first failing phase. This prevents cascading errors (e.g., no point in type-checking if parsing failed).

---

### `CompileProject(ProjectConfig projectConfig)` - Multi-File Compilation

**Signature**:
```csharp
public ProjectCompilationResult CompileProject(ProjectConfig projectConfig)
```

**Purpose**: Compiles an entire Sharpy project with multiple source files, resolving cross-module imports and producing a .NET assembly.

**Parameters**:
- `projectConfig` - Configuration loaded from `.spyproj` file, includes:
  - `SourceFiles` - List of `.spy` files to compile
  - `RootNamespace` - Project namespace (e.g., "MyApp")
  - `OutputType` - "exe" or "library"
  - `Configuration` - "Debug" or "Release"

**Returns**: `ProjectCompilationResult` containing the compiled assembly path, generated C# files, or errors

**The 5-Phase Pipeline**:

```
.spyproj + Source Files
    ↓
Phase 1: Parse All Files → Dictionary<string, Module>
    ↓
Phase 2: Resolve Imports → Shared SymbolTable
    ↓
Phase 3: Semantic Analysis (All Files)
    ↓
Phase 4: Code Generation (All Files) → Dictionary<string, CSharpCode>
    ↓
Phase 5: Assembly Compilation → .dll or .exe
    ↓
ProjectCompilationResult
```

**Key Differences from Single-File Compilation**:

1. **Batch Processing**: Parses all files before semantic analysis
2. **Shared Symbol Table**: All files share one symbol table (enables cross-module references)
3. **Import Resolution**: Special phase to handle `import` and `from ... import` statements
4. **Assembly Output**: Actually produces a runnable `.dll` or `.exe`, not just C# code

**Detailed Phase Breakdown**:

#### Phase 1: Parse All Source Files (Lines 71-121)

```csharp
var parsedModules = new Dictionary<string, Module>();

foreach (var sourceFile in projectConfig.SourceFiles)
{
    var source = File.ReadAllText(sourceFile);
    var lexer = new Lexer.Lexer(source, _logger);
    var tokens = lexer.TokenizeAll();
    var parser = new Parser.Parser(tokens, _logger);
    var module = parser.ParseModule();
    
    parsedModules[sourceFile] = module;
}
```

**Purpose**: Load and parse every `.spy` file in the project into ASTs.

**Error Handling**: Errors in any file are collected but don't immediately stop the process—we want to report **all parsing errors** at once, not just the first file.

**Why not stop early?**: Better developer experience. If you have 10 syntax errors across 5 files, you want to see all of them, not fix one and recompile to see the next.

#### Phase 2: Build Shared Symbol Table and Resolve Imports (Lines 134-202)

This is the **most complex phase** and unique to project compilation.

```csharp
var builtinRegistry = new BuiltinRegistry();
var symbolTable = new SymbolTable(builtinRegistry);  // SHARED across all files
var semanticInfo = new SemanticInfo();
var importResolver = new ImportResolver(_logger, _moduleRegistry);
```

**Key Insight**: Unlike single-file compilation, all files share **one symbol table**. This enables:
- File A can reference a class defined in File B
- Cross-module function calls
- Proper namespace handling

**Import Resolution Logic** (Lines 142-197):

For each module, iterate through import statements:

**Case 1: `import` statement** (Lines 148-159):
```python
import math
import sys
```

```csharp
if (statement is ImportStatement import)
{
    var modules = importResolver.ResolveImport(import, projectConfig.ProjectDirectory);
    foreach (var moduleInfo in modules)
    {
        foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
        {
            symbolTable.Define(symbol);  // Add to shared symbol table
        }
    }
}
```

**Case 2: `from ... import` statement** (Lines 160-196):
```python
from collections import List, Dict
from mymodule import MyClass
```

```csharp
else if (statement is FromImportStatement fromImport)
{
    var moduleInfo = importResolver.ResolveFromImport(fromImport, projectConfig.ProjectDirectory);
    
    if (fromImport.ImportAll)  // from foo import *
    {
        // Add all exported symbols
    }
    else
    {
        foreach (var importAlias in fromImport.Names)
        {
            // Add specific symbols, handling aliases:
            // from foo import bar as baz
            var symbolName = importAlias.AsName ?? importAlias.Name;
        }
    }
}
```

**Alias Handling**: The code properly handles `as` aliases by cloning symbols with new names:
```python
from math import sqrt as square_root
# symbolTable now has "square_root" pointing to math.sqrt's implementation
```

#### Phase 3: Semantic Analysis for All Modules (Lines 205-254)

```csharp
foreach (var (sourceFile, module) in parsedModules)
{
    var nameResolver = new NameResolver(symbolTable, _logger);
    nameResolver.ResolveDeclarations(module);
    nameResolver.ResolveInheritance();
    
    var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
    var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
    typeChecker.CheckModule(module);
}
```

**Important**: Each file is analyzed **using the same shared symbol table**, so:
- File A can reference types from File B
- All files see imported symbols
- Name conflicts across files are detected

#### Phase 4: Code Generation for All Modules (Lines 267-308)

```csharp
var generatedCSharp = new Dictionary<string, string>();

foreach (var (sourceFile, module) in parsedModules)
{
    var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
    {
        SourceFilePath = sourceFile,
        ProjectNamespace = projectConfig.RootNamespace,
        ProjectRootPath = Path.Combine(projectConfig.ProjectDirectory, "src")
    };
    
    var emitter = new RoslynEmitter(codeGenContext);
    var compilationUnit = emitter.GenerateCompilationUnit(module);
    var csharpCode = compilationUnit.ToFullString();
    
    var csharpFileName = Path.ChangeExtension(relativePath, ".cs");
    generatedCSharp[csharpFileName] = csharpCode;
}
```

**Output**: A dictionary mapping C# file names to their generated code:
```
{
    "src/main.cs": "namespace MyApp { public class Program { ... } }",
    "src/utils.cs": "namespace MyApp { public class Utils { ... } }",
    ...
}
```

**Important Context Setup**:
- `ProjectNamespace` - Ensures all generated C# classes are in the same namespace
- `ProjectRootPath` - Used for calculating relative paths and organizing output

#### Phase 5: Compile to Assembly (Lines 310-343)

```csharp
var assemblyCompiler = new AssemblyCompiler(_logger);
var assemblyResult = assemblyCompiler.CompileToAssembly(generatedCSharp, projectConfig);
```

**What happens**:
1. All generated C# code is passed to `AssemblyCompiler`
2. Roslyn compiles the C# code to IL (Intermediate Language)
3. A `.dll` or `.exe` file is written to the output directory
4. Compilation warnings/errors from the C# compiler are collected

**Output Example**:
```
bin/Debug/net9.0/MyApp.dll
bin/Debug/net9.0/MyApp.pdb  (debug symbols)
```

**Important**: This phase can fail even if all previous phases succeeded (e.g., if the generated C# is invalid, though this should be rare).

---

## Dependencies

### Internal Dependencies (Other Sharpy Compiler Components)

1. **`Sharpy.Compiler.Lexer`**
   - `Lexer` class - Tokenizes source code
   - `LexerError` - Exception for lexical errors

2. **`Sharpy.Compiler.Parser`**
   - `Parser` class - Parses tokens into AST
   - `ParserError` - Exception for syntax errors
   - `Ast` namespace - AST node definitions (`Module`, `ImportStatement`, etc.)

3. **`Sharpy.Compiler.Semantic`**
   - `NameResolver` - Resolves names to symbols
   - `TypeResolver` - Resolves type annotations
   - `TypeChecker` - Validates type correctness
   - `SymbolTable` - Maps names to types and scopes
   - `SemanticInfo` - Stores type narrowing information
   - `ModuleRegistry` - Manages external .NET modules
   - `ImportResolver` - Resolves import statements
   - `BuiltinRegistry` - Provides built-in Python functions

4. **`Sharpy.Compiler.CodeGen`**
   - `RoslynEmitter` - Generates C# code from AST
   - `CodeGenContext` - Context for code generation

5. **`Sharpy.Compiler.Diagnostics`**
   - `CompilationMetrics` - Tracks performance metrics
   - `ProjectCompilationMetrics` - Aggregates metrics for projects

6. **`Sharpy.Compiler.Logging`**
   - `ICompilerLogger` - Logging interface
   - `NullLogger` - No-op logger implementation

### External Dependencies

1. **Microsoft.CodeAnalysis.CSharp** (Roslyn)
   - Used by `RoslynEmitter` for generating C# syntax trees
   - Provides `SyntaxFactory` for programmatic C# code generation

2. **.NET BCL**
   - `System.IO` - File reading
   - `System.Collections.Generic` - Collections
   - `System.Linq` - LINQ queries

---

## Patterns and Design Decisions

### 1. **Pipeline Pattern**

The compiler follows a strict **sequential pipeline** architecture:

```
Input → Phase 1 → Phase 2 → Phase 3 → Phase 4 → Output
```

**Benefits**:
- Clear separation of concerns (each phase has one job)
- Easy to understand and debug
- Each phase can be tested independently
- Errors in early phases prevent later phases from running on invalid data

**Trade-offs**:
- Can't parallelize phases (must be sequential)
- Each phase processes the entire input before the next starts

### 2. **Fail-Fast Error Handling**

```csharp
if (nameResolver.Errors.Any())
{
    return new CompilationResult
    {
        Success = false,
        Errors = nameResolver.Errors.Select(e => e.Message).ToList(),
        Metrics = metrics
    };
}
```

**Design Decision**: Stop at the first phase that has errors.

**Rationale**: Cascading errors are confusing. If parsing fails, the AST is invalid, so semantic analysis would produce nonsensical errors. Better to fix parsing errors first.

**Exception**: In project compilation, parsing errors are **accumulated** across all files before stopping. This provides better feedback when multiple files have syntax errors.

### 3. **Dependency Injection for Logging**

```csharp
public Compiler(ICompilerLogger? logger = null)
{
    _logger = logger ?? NullLogger.Instance;
}
```

**Pattern**: Constructor accepts an optional logger, defaults to `NullLogger` (no-op).

**Benefits**:
- Testability: Tests can inject a mock logger to verify log messages
- Flexibility: Production uses `ConsoleLogger`, tests use `NullLogger` or `MemoryLogger`
- No null checks: `_logger` is always non-null

### 4. **Immutable Result Objects**

```csharp
public class CompilationResult
{
    public bool Success { get; init; }  // init-only properties
    public List<string> Errors { get; init; } = new();
    // ...
}
```

**Pattern**: All properties use `init` setters (C# 9.0 feature).

**Benefits**:
- Results can't be accidentally modified after creation
- Thread-safe (immutable objects are inherently thread-safe)
- Clear ownership: The compiler creates the result, callers consume it

### 5. **Optional Module Registry**

```csharp
private readonly ModuleRegistry? _moduleRegistry;
```

**Design Decision**: `ModuleRegistry` is nullable and only created when `CompilerOptions` is provided.

**Rationale**:
- Simple single-file compilations don't need external module support (saves memory/startup time)
- Project compilations need it for resolving imports
- Graceful degradation: Works fine without it for basic use cases

### 6. **Metrics Tracking**

```csharp
metrics.StartPhase("Lexical Analysis");
// ... do work ...
metrics.EndPhase();
```

**Pattern**: Explicit phase boundaries for performance tracking.

**Use Cases**:
- Profiling: Identify slow compilation phases
- Progress reporting: Show users what's happening
- Debugging: Understand where time is spent
- Regression testing: Catch performance regressions

### 7. **Shared Symbol Table in Project Compilation**

```csharp
var symbolTable = new SymbolTable(builtinRegistry);  // Created once

foreach (var (sourceFile, module) in parsedModules)
{
    var nameResolver = new NameResolver(symbolTable, _logger);  // Reused
    // ...
}
```

**Design Decision**: One symbol table for the entire project, not per-file.

**Benefits**:
- Cross-module references work naturally
- Simpler import resolution
- Consistent with how .NET namespaces work

**Trade-off**: Higher memory usage (all symbols in memory), but acceptable for compilation.

---

## Debugging Tips

### 1. **Enable Verbose Logging**

```csharp
var logger = new ConsoleLogger(CompilerLogLevel.Debug);
var compiler = new Compiler(logger);
```

This will show:
- Per-file metrics (timing for each phase)
- Module search paths being added
- Import resolution steps
- Detailed error contexts

### 2. **Check Metrics for Performance Issues**

```csharp
var result = compiler.Compile(source, path);
Console.WriteLine($"Total: {result.Metrics.TotalDuration.TotalMilliseconds} ms");
foreach (var phase in result.Metrics.Phases)
{
    Console.WriteLine($"{phase.Name}: {phase.Duration.TotalMilliseconds} ms");
}
```

**Common Issues**:
- Lexing/Parsing slow? → Complex nested structures or large files
- Semantic analysis slow? → Lots of types or complex inheritance
- Code generation slow? → Inefficient Roslyn usage (check `RoslynEmitter`)

### 3. **Inspect the Generated C# Code**

```csharp
var result = compiler.Compile(source, path);
if (result.Success)
{
    Console.WriteLine("=== Generated C# ===");
    Console.WriteLine(result.GeneratedCSharpCode);
}
```

**Why**: Many bugs manifest as incorrect C# generation. Seeing the actual output helps identify:
- Type mapping errors (wrong .NET types)
- Missing imports/using statements
- Incorrect operator overload synthesis

### 4. **Check Symbol Table Contents**

```csharp
var result = compiler.Compile(source, path);
if (result.SymbolTable != null)
{
    foreach (var symbol in result.SymbolTable.GetAllSymbols())
    {
        Console.WriteLine($"{symbol.Name}: {symbol.Type}");
    }
}
```

**Use Case**: Verify that types are being resolved correctly.

### 5. **Test Phases Individually**

You can test each phase in isolation:

```csharp
// Test just lexing
var lexer = new Lexer(source);
var tokens = lexer.TokenizeAll();

// Test just parsing
var parser = new Parser(tokens);
var module = parser.ParseModule();

// etc.
```

**Benefit**: Narrow down which phase is causing issues.

### 6. **Common Error Patterns**

| Error Pattern | Likely Cause | Where to Look |
|---------------|--------------|---------------|
| `LexerError` early in compilation | Syntax issue: unclosed string, invalid character | Lexer.cs, input source |
| `ParserError` in Phase 2 | Grammar violation, unexpected token | Parser.cs, grammar rules |
| `Success = false` with empty `Errors` | Exception caught and swallowed | Try/catch blocks, check for generic `Exception` |
| C# compilation fails (Phase 5) | Invalid generated C#, missing using statements | RoslynEmitter.cs, CodeGenContext |
| Import resolution errors | Missing module, incorrect path | ImportResolver.cs, ModuleRegistry.cs |

### 7. **Debugging Import Issues**

```csharp
var options = new CompilerOptions
{
    ModulePaths = new[] { "/path/to/modules" }
};
var compiler = new Compiler(options, logger);

// After compilation, check:
if (compiler._moduleRegistry != null)
{
    foreach (var error in compiler._moduleRegistry.Errors)
    {
        Console.WriteLine($"Module error: {error.Message}");
    }
}
```

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made

#### 1. **Adding New Compilation Phases**

**Example**: Add a Phase 3.5 for "Definite Assignment Analysis" (ensure variables are assigned before use)

**Where to Add**:
- Single-file: Between lines 419 and 434 (after type checking, before code gen)
- Project: Between lines 254 and 267

**Template**:
```csharp
metrics.StartPhase("Definite Assignment Analysis");
var assignmentChecker = new DefiniteAssignmentChecker(symbolTable, _logger);
assignmentChecker.CheckModule(module);
metrics.EndPhase();

if (assignmentChecker.Errors.Any())
{
    return new CompilationResult
    {
        Success = false,
        Errors = assignmentChecker.Errors.Select(e => e.Message).ToList(),
        Metrics = metrics
    };
}
```

#### 2. **Supporting New Compiler Options**

**Example**: Add `--optimize` flag

**Steps**:
1. Add property to `CompilerOptions`:
   ```csharp
   public bool EnableOptimizations { get; set; }
   ```

2. Pass to code generator:
   ```csharp
   var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
   {
       EnableOptimizations = options.EnableOptimizations
   };
   ```

3. Use in `RoslynEmitter` to emit different code

#### 3. **Improving Error Messages**

**Current** (Line 108):
```csharp
allErrors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
```

**Improved**:
```csharp
var relativePath = Path.GetRelativePath(projectConfig.ProjectDirectory, sourceFile);
var snippet = GetCodeSnippet(sourceFile, ex.Line);
allErrors.Add($"{relativePath}({ex.Line},{ex.Column}): error: {ex.Message}\n{snippet}");
```

**Benefits**: Show code context with errors (like Rust's error messages)

#### 4. **Adding Caching/Incremental Compilation**

**Idea**: Don't recompile files that haven't changed

**Where to Add**: Beginning of `CompileProject()`, after loading source files:

```csharp
var compilationCache = new CompilationCache(projectConfig.CacheDirectory);

foreach (var sourceFile in projectConfig.SourceFiles)
{
    var hash = ComputeFileHash(sourceFile);
    if (compilationCache.TryGetCachedModule(sourceFile, hash, out var cachedModule))
    {
        parsedModules[sourceFile] = cachedModule;
        continue;  // Skip lexing/parsing
    }
    
    // Otherwise parse as normal...
}
```

**Complexity**: Medium (need hash computation, cache invalidation)

#### 5. **Better Parallel Compilation**

**Current**: Files are processed sequentially

**Improvement**: Parse files in parallel (Phase 1 only)

```csharp
var parsedModules = new ConcurrentDictionary<string, Module>();

Parallel.ForEach(projectConfig.SourceFiles, sourceFile =>
{
    var source = File.ReadAllText(sourceFile);
    var lexer = new Lexer.Lexer(source, _logger);
    var tokens = lexer.TokenizeAll();
    var parser = new Parser.Parser(tokens, _logger);
    var module = parser.ParseModule();
    
    parsedModules[sourceFile] = module;
});
```

**Caution**: Phases 2-5 must remain sequential (they share state)

#### 6. **Adding Watch Mode**

**Idea**: Automatically recompile when files change

**Where to Add**: New method in `Compiler` class

```csharp
public void WatchProject(ProjectConfig projectConfig, Action<ProjectCompilationResult> onCompiled)
{
    var watcher = new FileSystemWatcher(projectConfig.ProjectDirectory, "*.spy");
    watcher.Changed += (sender, e) =>
    {
        var result = CompileProject(projectConfig);
        onCompiled(result);
    };
    watcher.EnableRaisingEvents = true;
}
```

### Testing Your Changes

#### 1. **Unit Tests**

**Location**: `src/Sharpy.Compiler.Tests/`

**Example Test**:
```csharp
[Fact]
public void TestSingleFileCompilation_Success()
{
    var source = "x: int = 42";
    var compiler = new Compiler();
    var result = compiler.Compile(source, "test.spy");
    
    Assert.True(result.Success);
    Assert.Empty(result.Errors);
    Assert.NotNull(result.GeneratedCSharpCode);
}
```

#### 2. **Integration Tests**

**Location**: `src/Sharpy.Compiler.Tests/Integration/`

**Example Test**:
```csharp
[Fact]
public void TestProjectCompilation_MultipleFiles()
{
    var projectConfig = new ProjectConfig
    {
        RootNamespace = "TestProject",
        SourceFiles = new List<string> { "file1.spy", "file2.spy" },
        ProjectDirectory = "."
    };
    
    var compiler = new Compiler();
    var result = compiler.CompileProject(projectConfig);
    
    Assert.True(result.Success);
    Assert.NotNull(result.OutputAssemblyPath);
    Assert.True(File.Exists(result.OutputAssemblyPath));
}
```

### Best Practices

1. **Preserve the Pipeline**: Don't skip phases or change their order without careful consideration
2. **Collect All Errors**: When possible, collect multiple errors before failing (better UX)
3. **Use Metrics**: Wrap new phases with `StartPhase()`/`EndPhase()` for tracking
4. **Log Appropriately**:
   - `LogDebug`: Detailed info, only for debugging
   - `LogInfo`: High-level progress (phase starts/ends)
   - `LogWarning`: Non-fatal issues
   - `LogError`: Fatal errors
5. **Handle Exceptions**: Wrap risky operations in try/catch to provide better error messages
6. **Update Both Methods**: Changes to `Compile()` often need similar changes to `CompileProject()`

### Code Style Notes

- **File-scoped namespaces**: Use `namespace Sharpy.Compiler;` (not braces)
- **Expression-bodied members**: Use for simple getters: `public bool IsReady => _state == State.Ready;`
- **Nullable annotations**: Mark nullable reference types explicitly: `ModuleRegistry?`
- **Init-only properties**: Use `init` for result objects (immutability)
- **LINQ for transformations**: Use `.Select()`, `.Any()`, `.ToList()` for collections

---

## Summary

The `Compiler.cs` file is the **orchestration hub** that coordinates all compilation phases. It provides two main entry points:

1. **`Compile()`** - Single-file compilation to C# code (4 phases)
2. **`CompileProject()`** - Multi-file compilation to .NET assembly (5 phases)

Both follow a **fail-fast pipeline** architecture where each phase must succeed before the next begins. The shared symbol table approach in project compilation enables cross-module references and import resolution.

Key architectural decisions include:
- Sequential pipeline for clarity
- Dependency injection for testability
- Immutable result objects for safety
- Explicit metrics tracking for performance monitoring
- Optional module registry for flexible external dependencies

When contributing, focus on:
- Maintaining the phase structure
- Providing good error messages
- Adding comprehensive tests
- Tracking performance with metrics

The file is ~500 lines but conceptually simple: it's glue code that connects the real work (lexing, parsing, semantic analysis, code generation) into a cohesive compilation process.
