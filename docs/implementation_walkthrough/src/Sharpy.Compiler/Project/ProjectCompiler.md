# Walkthrough: ProjectCompiler.cs

**Source File**: `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

---

## Overview

`ProjectCompiler` is the **orchestrator** for compiling multi-file Sharpy projects into .NET assemblies. Think of it as the "conductor" that coordinates all the different phases of compilation in the correct order.

**Role in the Pipeline**: This class sits at the highest level of abstraction in the compiler. It takes a project configuration (list of `.spy` files, output settings, etc.) and orchestrates the entire compilation process from raw source text to executable assembly.

**Key Responsibility**: Managing the complex multi-file compilation pipeline with proper dependency resolution and shared state across files.

---

## Architecture: The Seven-Phase Compilation Pipeline

The `ProjectCompiler.Compile()` method implements a **seven-phase pipeline** that transforms multiple `.spy` source files into a compiled .NET assembly:

```
Phase 1: Parse All Files          → AST modules
Phase 2: Initialize Shared State   → Symbol table, semantic info
Phase 3: Collect Type Declarations → Cross-file type visibility
Phase 4: Resolve Imports           → Build module dependencies
Phase 5: Semantic Analysis         → Type checking
Phase 6: Code Generation           → Generate C# code
Phase 7: Assembly Compilation      → Compile to .NET assembly
```

Each phase builds on the previous one, and **failure in any phase halts the entire compilation**.

---

## Class Structure

### Core Data Members

The `ProjectCompiler` maintains several categories of state:

#### **Shared Compilation State** (used across all files)
```csharp
private SymbolTable _symbolTable = null!;
private SemanticInfo _semanticInfo = null!;
private ImportResolver _importResolver = null!;
```
These are initialized **once** and shared across all source files, enabling cross-file references and type resolution.

#### **Per-File Tracking**
```csharp
private Dictionary<string, Module> _parsedModules = new();
private Dictionary<string, CompilationMetrics> _fileMetrics = new();
```
Maps file paths to their parsed AST representations and performance metrics.

#### **Error Aggregation**
```csharp
private List<string> _errors = new();
private List<string> _warnings = new();
```
Collects errors/warnings from all phases and files into a unified report.

#### **Services**
```csharp
private readonly ICompilerLogger _logger;
private readonly ModuleRegistry? _moduleRegistry;
```
- `_logger`: Diagnostic output throughout compilation
- `_moduleRegistry`: Optional external module registry for resolving imports to compiled assemblies

---

## Key Methods: The Seven Phases

### Phase 1: `ParseAllFiles()` - Lexing & Parsing

**What it does**: Reads each `.spy` file, tokenizes it, and parses it into an AST.

**Key Implementation Details**:
- Uses `Lexer.Lexer` to tokenize source text
- Uses `Parser.Parser` to build AST `Module` objects
- **Error handling**: Catches `LexerError` and `ParserError`, converting them to human-readable error messages with file locations
- **Metrics tracking**: Records timing for lexical and syntax analysis phases per file

```csharp
fileMetrics.StartPhase("Lexical Analysis");
var lexer = new Lexer.Lexer(source, _logger);
var tokens = lexer.TokenizeAll();
fileMetrics.EndPhase();

fileMetrics.StartPhase("Syntax Analysis");
var parser = new Parser.Parser(tokens, _logger);
var module = parser.ParseModule();
fileMetrics.EndPhase();
```

**Outcome**: `_parsedModules` dictionary is populated with all successfully parsed modules.

---

### Phase 2: `InitializeSharedState()` - Symbol Table Setup

**What it does**: Creates the global symbol table and semantic info structures that will be shared across all files.

```csharp
var builtinRegistry = new BuiltinRegistry();
_symbolTable = new SymbolTable(builtinRegistry);
_semanticInfo = new SemanticInfo();
_importResolver = new ImportResolver(_logger, _moduleRegistry);
```

**Why this matters**: The shared symbol table enables cross-file references. A class defined in `models.spy` can be referenced in `main.spy` because both files contribute to the same symbol table.

---

### Phase 3: `CollectTypeDeclarations()` - Cross-File Type Visibility

**What it does**: This is a **two-pass type declaration phase** that enables forward references and cross-file type usage.

**The Two Sub-Phases**:

1. **Pass 1 - Register Type Names** (lines 183-201):
   ```csharp
   var nameResolver = new NameResolver(_symbolTable, _logger);
   nameResolver.ResolveDeclarations(module);
   ```
   - Registers all class, interface, enum names in the symbol table
   - Does NOT resolve members or inheritance yet
   - This allows `class A` in file1.spy to reference `class B` from file2.spy

2. **Pass 2 - Resolve Inheritance** (lines 204-216):
   ```csharp
   nameResolver.ResolveInheritance();
   ```
   - Now that all type names are known, resolve base classes and interfaces
   - Errors here (e.g., `class A extends NonExistentClass`) are collected

**Design Pattern**: This is a classic **forward declaration** pattern from C/C++, adapted for cross-file compilation.

---

### Phase 4: `ResolveImports()` - Build Module Dependencies

**What it does**: Processes `import` and `from ... import` statements, loading external modules and adding their symbols to the current symbol table.

**Key Logic**:

#### **Import Statement Handling** (lines 232-296)
Handles both:
- **Aliased imports**: `import lib.math as m` → creates a `ModuleSymbol` named "m"
- **Nested imports**: `import lib.math` → creates nested structure: `lib.Exports["math"]`

```csharp
// For "import lib.math", build: lib -> math -> (exports)
var parts = importAlias.Name.Split('.');
var leafModule = new ModuleSymbol {
    Name = parts[^1],  // "math"
    Exports = moduleInfo.ExportedSymbols
};
```

**Module Merging** (lines 286-295):
If you import `lib.math` and `lib.collections` separately, the compiler **merges** them into a single `lib` module with both `math` and `collections` as exports.

#### **From-Import Statement Handling** (lines 298-333)
- `from lib.math import sin, cos` → adds `sin` and `cos` directly to the symbol table
- `from lib.math import *` → imports all exported symbols
- Supports aliasing: `from lib.math import sin as sine`

**Outcome**: `_symbolTable` now contains all imported symbols, enabling the type checker to resolve references to external modules.

---

### Phase 5: `PerformSemanticAnalysis()` - Type Checking

**What it does**: Runs semantic analysis (type checking) on all modules.

**Key Steps**:
1. **Type Resolution**: Resolves type annotations and type expressions
2. **Type Checking**: Validates that operations are type-safe

```csharp
var typeResolver = new TypeResolver(_symbolTable, _semanticInfo, _logger);
var typeChecker = new TypeChecker(_symbolTable, _semanticInfo, typeResolver, _logger);
typeChecker.CheckModule(module);
```

**Error Handling**: All type errors are collected and reported with file/line/column information.

---

### Phase 6: `GenerateCode()` - C# Code Generation

**What it does**: Translates each Sharpy AST module into equivalent C# source code.

**Entry Point Detection** (lines 406-420):
The compiler needs to know which file contains the program's entry point (`Main()` method):
- If `config.EntryPoint` is specified, use that file
- Otherwise, default to `main.spy` for executable projects

```csharp
var isEntryPoint = IsEntryPointFile(sourceFile, config);
```

**CodeGenContext Setup** (lines 422-429):
Each file gets its own context with:
- Reference to the shared symbol table
- Source file path for debugging info
- Project namespace for generated C# namespaces
- **`ProjectRootPath`**: Critical for computing relative namespaces (see `ComputeSourceRootPath()`)

**Source Root Path Computation** (lines 517-556):
This is a **key algorithm** for determining how to map file paths to C# namespaces.

Example:
```
Project structure:
  src/models/user.spy    → namespace MyProject.Models
  src/services/auth.spy  → namespace MyProject.Services
```

The algorithm finds the **common directory prefix** (`src/`) and uses it to compute relative namespaces.

**Outcome**: `generatedCSharp` dictionary maps C# file names to their generated source code.

---

### Phase 7: `CompileAssembly()` - .NET Assembly Compilation

**What it does**: Invokes the `AssemblyCompiler` to compile generated C# code into a .NET assembly using Roslyn.

```csharp
var assemblyCompiler = new AssemblyCompiler(_logger);
var assemblyResult = assemblyCompiler.CompileToAssembly(generatedCSharp, config);
```

**Outcome**: A compiled `.dll` or `.exe` file, or compilation errors from the C# compiler.

---

## Helper Methods

### `ComputeSourceRootPath()` - Namespace Mapping

**Purpose**: Determines the "root" directory for computing relative namespaces.

**Algorithm**:
1. If no source files, return project directory
2. If all files in same directory, return that directory
3. Otherwise, find the **longest common path prefix** of all source file directories

**Example**:
```
Files:
  /proj/src/models/user.spy
  /proj/src/services/auth.spy

Result: /proj/src
```

This is used by `CodeGenContext` to compute namespaces:
- `user.spy` → `MyProject.Models` (relative to `src/`)
- `auth.spy` → `MyProject.Services` (relative to `src/`)

---

### `GetLongestCommonPath()` - Path Prefix Calculation

**Purpose**: Utility to find the common directory prefix between two paths.

**Algorithm**: Split paths by directory separators, compare parts one-by-one until they differ.

```csharp
var parts1 = "/proj/src/models".Split('/');  // ["", "proj", "src", "models"]
var parts2 = "/proj/src/services".Split('/'); // ["", "proj", "src", "services"]

// Common: ["", "proj", "src"] → "/proj/src"
```

---

### `MergeModuleExports()` - Module Symbol Merging

**Purpose**: Combines exports from multiple import statements into a single module symbol.

**Use Case**:
```python
import lib.math
import lib.collections
```

Both imports need to contribute to the same `lib` module:
```
lib.Exports["math"] = MathModule
lib.Exports["collections"] = CollectionsModule
```

**Recursive Merging**: If both modules have nested children, merge recursively. Otherwise, first import wins.

---

## Dependencies

### Internal Dependencies
- **Lexer**: Tokenization (`Lexer.Lexer`)
- **Parser**: AST construction (`Parser.Parser`)
- **Semantic**: Type checking (`NameResolver`, `TypeResolver`, `TypeChecker`)
- **CodeGen**: C# generation (`RoslynEmitter`, `CodeGenContext`)
- **Logging**: Diagnostics (`ICompilerLogger`)
- **Project**: Assembly compilation (`AssemblyCompiler`)

### Key Data Structures
- `SymbolTable`: Global symbol registry (from `Sharpy.Compiler.Semantic`)
- `Module`: AST root node (from `Sharpy.Compiler.Parser.Ast`)
- `ProjectConfig`: Input configuration with file list, output settings
- `ProjectCompilationResult`: Output with success status, errors, warnings, metrics

---

## Patterns and Design Decisions

### 1. **Pipeline Pattern**
The seven-phase design is a classic **pipeline architecture**. Each phase:
- Has a clear input/output contract
- Is isolated from other phases
- Can fail independently with detailed error reporting

### 2. **Shared State for Cross-File Compilation**
Unlike single-file compilers, this uses **shared mutable state** (`_symbolTable`, `_semanticInfo`) to enable cross-file references. This is necessary for features like:
- Importing classes from other files
- Inheriting from classes defined in other files
- Calling functions defined in other modules

### 3. **Early Bailout on Errors**
Each phase returns `bool` or checks `_errors.Any()`. If any phase fails, compilation stops immediately rather than continuing with corrupted state.

### 4. **Metrics Collection**
The compiler tracks detailed timing metrics for each file and each phase:
```csharp
fileMetrics.StartPhase("Lexical Analysis");
// ... do work ...
fileMetrics.EndPhase();
```
This enables performance profiling and optimization.

### 5. **Entry Point Detection**
The compiler automatically detects entry points (`main.spy` by default) to generate appropriate `Main()` methods in C#.

---

## Debugging Tips

### 1. **Enable Debug Logging**
Set logger to `Debug` level to see per-file timing metrics:
```csharp
if (_logger.IsEnabled(CompilerLogLevel.Debug))
{
    _logger.LogDebug($"Parsed {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
}
```

### 2. **Check Phase Transition**
If compilation fails, look at the error messages to identify which phase failed:
- **Phase 1 errors**: Syntax/lexer errors (check `LexerError`, `ParserError` catches)
- **Phase 3 errors**: Type declaration issues (undefined types, inheritance cycles)
- **Phase 4 errors**: Import resolution failures (missing modules)
- **Phase 5 errors**: Type checking failures (type mismatches)
- **Phase 7 errors**: C# compilation errors (code generation bugs)

### 3. **Inspect Intermediate State**
After each phase, you can inspect:
- `_parsedModules`: The parsed AST for each file
- `_symbolTable.Dump()`: All registered symbols
- `generatedCSharp`: The generated C# code

### 4. **Common Issues**

**Import resolution fails**: Check `_importResolver.Errors`. Often caused by:
- Incorrect module paths
- Missing `.spy` files
- Circular imports

**Type not found in cross-file reference**: Likely issue in Phase 3 (type declaration collection). Verify that `NameResolver.ResolveDeclarations()` ran successfully for all files.

**Namespace mismatches in generated C#**: Check `ComputeSourceRootPath()` output. If files aren't organized as expected, the namespace computation may be wrong.

---

## Contribution Guidelines

### When to Modify This File

1. **Adding new compilation phases**: If you need to add analysis/transformation steps (e.g., optimization passes), you'd insert new phases in the pipeline.

2. **Changing import semantics**: Modifications to `ResolveImports()` if Sharpy's import system changes.

3. **Adjusting entry point detection**: Modify `IsEntryPointFile()` if entry point conventions change.

4. **Performance improvements**: Add parallelization (currently sequential) or caching.

### Code Style Conventions

- **Phase methods**: Name as `Phase{Number}: {Description}` in comments
- **Helper methods**: Use descriptive names with XML doc comments
- **Error handling**: Always add file/line/column context to error messages
- **Metrics**: Wrap significant operations in `StartPhase()`/`EndPhase()` calls

### Testing Considerations

When modifying this file, ensure tests cover:
- Single-file projects (simple case)
- Multi-file projects with cross-file references
- Import statements (both `import` and `from ... import`)
- Error scenarios in each phase
- Entry point detection edge cases

---

## Cross-References

### Related Files

This file orchestrates components documented in:
- **Lexer**: `docs/implementation_walkthrough/src/Sharpy.Compiler/Lexer/Lexer.md`
- **Parser**: `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/Parser.*.md`
- **Semantic Analysis**:
  - Name Resolution (not yet documented)
  - Type Resolution (not yet documented)
  - Type Checking (not yet documented)
- **Code Generation**:
  - `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/RoslynEmitter.*.md`
  - `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/CodeGenContext.md`

### Data Structures

- **ProjectConfig**: Configuration object with source files, output settings
- **ProjectCompilationResult**: Result object with success/failure, errors, warnings, metrics
- **CompilationMetrics**: Per-file performance tracking
- **ProjectCompilationMetrics**: Aggregate project-level metrics

---

## Summary

`ProjectCompiler` is the **central orchestrator** of the Sharpy compilation pipeline. It:
- Manages the seven-phase compilation process
- Coordinates shared state across multiple source files
- Handles error aggregation and reporting
- Tracks performance metrics
- Ensures proper dependency resolution and cross-file visibility

Understanding this file is crucial for anyone working on the compiler's architecture, adding new compilation phases, or debugging multi-file compilation issues.
