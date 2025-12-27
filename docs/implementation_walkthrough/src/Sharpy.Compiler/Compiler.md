# Walkthrough: Compiler.cs

**Source File**: `src/Sharpy.Compiler/Compiler.cs`

---

## Overview

`Compiler.cs` is the **orchestration hub** for the entire Sharpy compilation pipeline. Think of it as the conductor of an orchestra—it doesn't play any instruments itself, but coordinates all the different stages of compilation in the correct order and ensures they work together harmoniously.

This file provides two main entry points:
1. **Single-file compilation** (`Compile()`) - For compiling individual `.spy` files
2. **Project compilation** (`CompileProject()`) - For compiling multi-file projects from `.spyproj` files

Both methods drive the same underlying 4-phase pipeline:
```
Source Code → Lexical Analysis → Syntax Analysis → Semantic Analysis → Code Generation → C#
```

## Class/Type Structure

### Main Class: `Compiler`

The `Compiler` class is the public-facing API for compilation. It's instantiated with optional configuration and then drives the entire compilation process.

**Key Fields:**
- `_logger: ICompilerLogger` - Logs compilation progress, errors, and warnings
- `_moduleRegistry: ModuleRegistry?` - Manages module imports and .NET assembly references (null for simple single-file compilation)

**Constructors:**
```csharp
// Simple constructor for single-file compilation
public Compiler(ICompilerLogger? logger = null)

// Full constructor with options for project compilation
public Compiler(CompilerOptions options, ICompilerLogger? logger = null)
```

The second constructor is more powerful—it sets up module search paths and loads .NET assembly references that the Sharpy code might import.

### Result Classes

#### `CompilationResult`
Returned by single-file `Compile()`. Contains:
- `Success` - Did compilation succeed?
- `Errors` - List of error messages if failed
- `Module` - The parsed AST (Abstract Syntax Tree)
- `SymbolTable` - All declared symbols (functions, classes, variables)
- `SemanticInfo` - Type information resolved during semantic analysis
- `GeneratedCSharpCode` - The C# code produced
- `Metrics` - Performance timing for each phase

#### `ProjectCompilationResult`
Returned by `CompileProject()`. Contains:
- `Success`, `Errors`, `Warnings` - Status information
- `OutputAssemblyPath` - Path to the compiled .NET assembly
- `GeneratedCSharpFiles` - Dictionary mapping file paths to generated C# code
- `Metrics` - Project-wide performance metrics

#### `CompilerOptions`
Configuration for the compiler:
- `ModulePaths` - Directories to search for Sharpy modules
- `References` - .NET assemblies to reference (e.g., System.dll, custom libraries)

## Key Methods

### `Compile(string sourceCode, string filePath)`

This is the **single-file compilation entry point**. It's simpler than `CompileProject()` and perfect for understanding the core pipeline.

**Pipeline Stages:**

#### Phase 1: Lexical Analysis (Lines 363-368)
```csharp
var lexer = new Lexer.Lexer(sourceCode, _logger);
var tokens = lexer.TokenizeAll();
```
Breaks source code into tokens (keywords, identifiers, operators, etc.). Think of this as converting a sentence into individual words.

**Example**: `def foo(x: int):` → `[DEF, IDENTIFIER("foo"), LPAREN, IDENTIFIER("x"), COLON, ...]`

#### Phase 2: Syntax Analysis (Lines 370-375)
```csharp
var parser = new Parser.Parser(tokens, _logger);
var module = parser.ParseModule();
```
Builds an Abstract Syntax Tree (AST) from tokens. This understands the grammatical structure of Sharpy code.

**Example**: Token stream → `FunctionDef` node with name="foo", parameters=[Parameter(name="x", type=int)], body=[...]

#### Phase 3: Semantic Analysis (Lines 377-429)

This is the **most complex phase** with three sub-passes:

**a) Name Resolution (Lines 395-409)**
```csharp
var nameResolver = new NameResolver(symbolTable, _logger);
nameResolver.ResolveDeclarations(module);
nameResolver.ResolveInheritance();
```
- Builds a `SymbolTable` with all declarations (functions, classes, variables)
- Resolves inheritance relationships between classes
- Catches errors like "variable used before declaration" or "duplicate function name"

**b) Type Resolution (Lines 412-414)**
```csharp
var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
```
Creates a resolver that can look up and validate type annotations.

**c) Type Checking (Lines 416-429)**
```csharp
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
typeChecker.CheckModule(module);
```
- Verifies type correctness (e.g., can't assign `str` to `int` variable)
- Infers types where not explicitly annotated
- Handles type narrowing (e.g., after `if x is not None:`, x is no longer optional)
- Populates `SemanticInfo` with resolved types for every expression

**Important Design Decision**: Semantic information is stored *separately* in `SemanticInfo`, not on the AST nodes themselves. This keeps the AST immutable.

#### Phase 4: Code Generation (Lines 434-443)
```csharp
var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry);
var emitter = new RoslynEmitter(codeGenContext);
var compilationUnit = emitter.GenerateCompilationUnit(module);
var csharpCode = compilationUnit.ToFullString();
```
Uses Roslyn (the C# compiler API) to generate C# code from the AST. This is where Sharpy's `list[int]` becomes `Sharpy.Core.List<int>`, and `snake_case` becomes `PascalCase`.

**Error Handling**: After each phase, the method checks for errors and returns early if any are found. This **fail-fast** approach prevents cascading errors.

### `CompileProject(ProjectConfig projectConfig)`

This is the **multi-file compilation entry point**. It's more complex because it must:
1. Coordinate compilation across multiple source files
2. Resolve imports between modules
3. Compile everything into a single .NET assembly

**5-Phase Pipeline:**

#### Phase 1: Parse All Source Files (Lines 71-132)
```csharp
foreach (var sourceFile in projectConfig.SourceFiles)
{
    var lexer = new Lexer.Lexer(source, _logger);
    var tokens = lexer.TokenizeAll();
    var parser = new Parser.Parser(tokens, _logger);
    var module = parser.ParseModule();
    parsedModules[sourceFile] = module;
}
```
Lexes and parses all `.spy` files independently. Stores results in `parsedModules` dictionary.

**Key Pattern**: Each file is wrapped in try-catch to isolate errors. If one file fails to parse, others continue, and all errors are collected for comprehensive feedback.

#### Phase 2: Resolve Imports (Lines 135-203)
```csharp
var importResolver = new ImportResolver(_logger, _moduleRegistry);
foreach (var (sourceFile, module) in parsedModules)
{
    foreach (var statement in module.Body)
    {
        if (statement is ImportStatement import)
            // Resolve and add symbols to shared symbol table
        else if (statement is FromImportStatement fromImport)
            // Resolve specific imported symbols
    }
}
```
This is **critical** for multi-file projects:
- Resolves `import foo` and `from bar import baz` statements
- Builds a **shared symbol table** across all modules
- Handles import aliases (`from foo import bar as baz`)
- Supports "import all" (`from foo import *`)

**Why separate phase?** Because Module A might import from Module B, which imports from Module A (circular dependencies). We need all modules parsed before resolving cross-references.

#### Phase 3: Semantic Analysis (Lines 205-254)
```csharp
foreach (var (sourceFile, module) in parsedModules)
{
    var nameResolver = new NameResolver(symbolTable, _logger);
    nameResolver.ResolveDeclarations(module);
    var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
    typeChecker.CheckModule(module);
}
```
Same 3-pass semantic analysis as single-file, but now with the **shared symbol table** from Phase 2. This allows type checking to see symbols from other modules.

#### Phase 4: Code Generation (Lines 268-308)
```csharp
foreach (var (sourceFile, module) in parsedModules)
{
    var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
    {
        ProjectNamespace = projectConfig.RootNamespace,
        ProjectRootPath = Path.Combine(projectConfig.ProjectDirectory, "src")
    };
    var emitter = new RoslynEmitter(codeGenContext);
    var compilationUnit = emitter.GenerateCompilationUnit(module);
    generatedCSharp[csharpFileName] = csharpCode;
}
```
Generates C# for each Sharpy file. Note the **additional context** compared to single-file:
- `ProjectNamespace` - Places all generated C# in a common namespace
- `ProjectRootPath` - Used for computing relative namespaces

#### Phase 5: Assembly Compilation (Lines 311-331)
```csharp
var assemblyCompiler = new AssemblyCompiler(_logger);
var assemblyResult = assemblyCompiler.CompileToAssembly(generatedCSharp, projectConfig);
```
Takes all the generated C# code and uses the C# compiler (Roslyn) to produce a .NET assembly (`.dll` or `.exe`).

This is where we finally get an executable!

## Dependencies

### Internal Dependencies
- `Sharpy.Compiler.Lexer` - Tokenization
- `Sharpy.Compiler.Parser` - AST generation
- `Sharpy.Compiler.Semantic` - Name resolution, type checking
  - `SymbolTable` - Stores all declared symbols
  - `NameResolver` - Resolves declarations and inheritance
  - `TypeResolver` - Looks up and validates types
  - `TypeChecker` - Verifies type correctness
  - `ImportResolver` - Handles module imports
- `Sharpy.Compiler.CodeGen` - C# generation
  - `RoslynEmitter` - Generates C# using Roslyn
  - `CodeGenContext` - Provides context for code generation
- `Sharpy.Compiler.Diagnostics` - Performance metrics
- `Sharpy.Compiler.Logging` - Structured logging

### External Dependencies
- `Microsoft.CodeAnalysis.CSharp` - Roslyn, the C# compiler API
- Standard .NET BCL (System.IO, System.Collections.Generic, etc.)

### Data Flow
```
Source → Lexer → Tokens → Parser → AST → NameResolver → SymbolTable
                                              ↓
                                         TypeChecker → SemanticInfo
                                              ↓
                                     RoslynEmitter → C# Code
```

## Patterns and Design Decisions

### 1. **Pipeline Architecture**
The compiler follows a classic **multi-pass compiler design**:
- Each phase is independent and communicates through well-defined data structures (tokens, AST, SymbolTable, SemanticInfo)
- Phases are **sequential and ordered** - you can't skip ahead
- Each phase validates its inputs before proceeding

### 2. **Fail-Fast Error Handling**
```csharp
if (allErrors.Any())
{
    return new ProjectCompilationResult { Success = false, Errors = allErrors };
}
```
After each major phase, compilation stops if errors are found. This prevents confusing cascading errors (e.g., type errors caused by syntax errors).

### 3. **Immutable AST with Side Tables**
The AST (`Module`) is immutable—once created by the parser, it never changes. Semantic information (types, symbol references) is stored in `SemanticInfo` and `SymbolTable`, separate from the AST.

**Why?** This separation allows:
- Multiple semantic passes without AST mutation
- Easier parallel processing (future optimization)
- Cleaner architecture—each component owns its data

### 4. **Metrics Collection**
Every phase is wrapped with timing:
```csharp
metrics.StartPhase("Lexical Analysis");
// ... do work ...
metrics.EndPhase();
```
This provides **performance visibility** for optimization and debugging. Metrics are included in compilation results.

### 5. **Null Logger Pattern**
```csharp
_logger = logger ?? NullLogger.Instance;
```
If no logger is provided, a no-op logger is used. This avoids null checks throughout the code.

### 6. **Result Objects over Exceptions**
Rather than throwing exceptions for compilation errors, methods return `CompilationResult` or `ProjectCompilationResult` with `Success` flag. This makes error handling explicit and allows collecting multiple errors.

### 7. **Shared Symbol Table in Projects**
For multi-file projects, a **single shared `SymbolTable`** is used across all modules after import resolution. This allows cross-module references to work correctly.

## Debugging Tips

### 1. **Enable Debug Logging**
```csharp
var logger = new ConsoleLogger(CompilerLogLevel.Debug);
var compiler = new Compiler(logger);
```
You'll see detailed timing for each file and phase, which helps identify slow compilation or where errors occur.

### 2. **Inspect Intermediate Artifacts**
The `CompilationResult` includes:
- `Module` - Inspect the parsed AST
- `SymbolTable` - See what symbols were declared
- `SemanticInfo` - Check resolved types
- `GeneratedCSharpCode` - Examine the output C#

**Debugging technique**: Compile a simple test case and inspect each artifact to understand the pipeline's behavior.

### 3. **Check Metrics for Performance Issues**
```csharp
var result = compiler.Compile(source, filePath);
if (result.Metrics != null)
{
    foreach (var phase in result.Metrics.Phases)
    {
        Console.WriteLine($"{phase.Name}: {phase.Duration.TotalMilliseconds}ms");
    }
}
```
If compilation is slow, metrics pinpoint which phase is the bottleneck.

### 4. **Look at Error Line/Column Numbers**
Errors include location information:
```csharp
$"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}"
```
This matches Visual Studio's error format, so you can click to jump to the problem.

### 5. **Test Single-File First**
If `CompileProject()` fails mysteriously, try compiling each file individually with `Compile()`. This isolates whether the issue is in parsing/semantic analysis vs. import resolution.

### 6. **Examine Generated C#**
When code generation produces unexpected results, inspect `GeneratedCSharpCode`. Understanding what C# is produced helps debug both the emitter and semantic analysis.

### 7. **Common Error Patterns**

| Error Phase | Likely Cause | Debug Strategy |
|------------|--------------|----------------|
| Lexer | Invalid character, unclosed string | Check source encoding, look for special characters |
| Parser | Syntax error | Verify Sharpy grammar is correct, check for missing colons/indentation |
| Name Resolution | Undefined variable, duplicate declaration | Check symbol table contents, verify declaration order |
| Type Checking | Type mismatch, incompatible assignment | Examine `SemanticInfo.GetType()` for expressions |
| Code Generation | Missing mapping, unsupported feature | Check `RoslynEmitter` for the relevant AST node type |

## Contribution Guidelines

### Adding a New Compilation Phase

If you need to add a new phase (e.g., optimization, linting):

1. **Create the phase class** in an appropriate namespace (e.g., `Sharpy.Compiler.Optimization`)
2. **Add phase timing** to `CompilationMetrics`
3. **Insert into pipeline** in both `Compile()` and `CompileProject()`
4. **Return early on errors** if the phase can fail
5. **Update result classes** if the phase produces new artifacts
6. **Add tests** for the new phase

**Example insertion point** (after semantic analysis, before code generation):
```csharp
// Phase 3.5: Optimization
metrics.StartPhase("Optimization");
var optimizer = new Optimizer(symbolTable, semanticInfo);
optimizer.OptimizeModule(module);
metrics.EndPhase();

if (optimizer.Errors.Any())
{
    return new CompilationResult { Success = false, Errors = optimizer.Errors };
}
```

### Improving Error Messages

Error messages are constructed in multiple places:
- Lexer/Parser exceptions are caught and formatted (lines 106-120)
- Semantic errors come from `nameResolver.Errors` and `typeChecker.Errors` (lines 228-247)

**To improve error messages:**
1. Modify the error generation in the specific phase (Lexer, Parser, TypeChecker, etc.)
2. Ensure errors include `Line` and `Column` for IDE integration
3. Test with intentionally broken code to verify message quality

### Adding Compiler Options

To add new configuration:

1. **Add property to `CompilerOptions`**:
```csharp
public bool EnableOptimizations { get; set; }
```

2. **Read in constructor**:
```csharp
public Compiler(CompilerOptions options, ICompilerLogger? logger = null)
{
    _enableOptimizations = options.EnableOptimizations;
    // ...
}
```

3. **Use in compilation pipeline**:
```csharp
if (_enableOptimizations)
{
    // ... optimization code ...
}
```

4. **Update CLI** (`Sharpy.Cli`) to expose the option

### Supporting New Import Mechanisms

Import resolution happens in Phase 2 of `CompileProject()`:

1. **Add new import statement type** to `Parser.Ast`
2. **Handle in import resolution loop** (lines 144-196)
3. **Extend `ImportResolver`** with new resolution logic
4. **Add symbols to shared symbol table**
5. **Test with integration tests** that compile multi-file projects

### Performance Optimization

If compilation is slow:

1. **Check metrics** to identify the slow phase
2. **Profile with a real profiler** (dotnet-trace, Visual Studio Profiler)
3. **Common optimization opportunities**:
   - Parallel file parsing (Phase 1 of `CompileProject`)
   - Cache parsed modules for incremental compilation
   - Optimize SymbolTable lookups (currently linear in some cases)
   - Lazy semantic analysis (only analyze what's used)

### Testing Guidelines

When modifying `Compiler.cs`:

1. **Add unit tests** for any new public methods
2. **Add integration tests** that compile actual Sharpy code end-to-end
3. **Test error cases** - verify that errors are caught and reported correctly
4. **Test metrics collection** - ensure new phases are timed
5. **Test both single-file and project compilation** if changes affect both

**Test locations**:
- `src/Sharpy.Compiler.Tests/CompilerTests.cs` - Unit tests for `Compiler`
- `src/Sharpy.Compiler.Tests/Integration/` - End-to-end compilation tests

### Code Style Conventions

- **Use object initializers** for result objects
- **Log at appropriate levels**: Debug for detailed info, Info for phase transitions, Error for failures
- **Prefer LINQ** for collection processing (e.g., `nameResolver.Errors.Select(e => e.Message)`)
- **Keep phases independent** - avoid tight coupling between pipeline stages
- **Early returns for errors** - don't continue after phase failure

---

## Quick Reference

### Compilation Pipeline Summary

**Single-File (`Compile`):**
```
Source → Lex → Parse → Name Resolution → Type Resolution → Type Checking → Code Gen → C#
```

**Project (`CompileProject`):**
```
Sources → Parse All → Resolve Imports → Name Resolution (all files) 
       → Type Checking (all files) → Code Gen (all files) → Assembly Compilation → .dll/.exe
```

### Key Data Structures

- **`Module`** - Root AST node representing a Sharpy file
- **`SymbolTable`** - Dictionary of all declared symbols (functions, classes, variables)
- **`SemanticInfo`** - Maps AST expressions to their resolved types
- **`CompilationMetrics`** - Performance timing for each phase
- **`ModuleRegistry`** - Manages imported modules and .NET assemblies

### Related Files to Explore

- `src/Sharpy.Compiler/Parser/Ast/Node.cs` - AST node definitions
- `src/Sharpy.Compiler/Semantic/SymbolTable.cs` - Symbol management
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs` - Type checking logic
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` - C# generation
- `src/Sharpy.Compiler/AssemblyCompiler.cs` - Phase 5 of project compilation

---

**Welcome to the Sharpy compiler! This orchestrator is your entry point to understanding the entire compilation process.** 🚀
