# Walkthrough: Compiler.cs

**Source File**: `src/Sharpy.Compiler/Compiler.cs`

---

## Overview

The `Compiler.cs` file is the **main orchestrator** of the Sharpy compilation pipeline. It acts as the entry point that coordinates all compilation phases from source code (.spy files) to generated C# code. This file implements the classic compiler frontend pattern, executing a series of well-defined phases: lexical analysis, syntax analysis, semantic analysis, and code generation.

**Role in Pipeline**: This is the conductor that brings together all the specialized compiler components (Lexer, Parser, NameResolver, TypeChecker, RoslynEmitter) and ensures they execute in the correct order with proper error handling and logging.

**Key Responsibilities**:
- Orchestrating the multi-phase compilation pipeline
- Managing compilation options and module registries
- Collecting and reporting errors from each phase
- Tracking compilation metrics for performance analysis
- Supporting both single-file and multi-file project compilation

---

## Class/Type Structure

### Main Classes

#### 1. `Compiler` Class

The primary compiler driver with two distinct modes:

**Fields**:
- `_logger`: ICompilerLogger - Handles diagnostic output throughout compilation
- `_moduleRegistry`: ModuleRegistry? - Optional registry for resolving cross-module imports and .NET assembly references

**Constructors**:

```csharp
// Simple constructor for single-file compilation without module dependencies
public Compiler(ICompilerLogger? logger = null)

// Advanced constructor for multi-file projects with module paths and assembly references
public Compiler(CompilerOptions options, ICompilerLogger? logger = null)
```

The constructor choice determines compilation mode:
- **Null registry**: Single-file, standalone compilation
- **With registry**: Multi-file project compilation with module resolution

#### 2. `CompilationResult` Class

Immutable record of single-file compilation outcome:

```csharp
public class CompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; }
    public Module? Module { get; init; }                    // Parsed AST
    public SymbolTable? SymbolTable { get; init; }          // Symbol resolution data
    public SemanticInfo? SemanticInfo { get; init; }        // Type information
    public ModuleRegistry? ModuleRegistry { get; init; }    // Import resolution
    public string? GeneratedCSharpCode { get; init; }       // Final C# output
    public CompilationMetrics? Metrics { get; init; }       // Performance data
}
```

**Design Decision**: Uses init-only properties for immutability, making compilation results thread-safe and preventing accidental modification.

#### 3. `ProjectCompilationResult` Class

Specialized result for multi-file project compilation:

```csharp
public class ProjectCompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; }
    public List<string> Warnings { get; init; }
    public string? OutputAssemblyPath { get; init; }
    public Dictionary<string, string> GeneratedCSharpFiles { get; init; }  // File path -> C# code
    public ProjectCompilationMetrics? Metrics { get; init; }
}
```

**Key Difference**: Includes `Warnings` and maps multiple source files to their generated C# counterparts.

#### 4. `CompilerOptions` Class

Configuration container for advanced compilation scenarios:

```csharp
public class CompilerOptions
{
    public string[]? ModulePaths { get; set; }   // Where to find .spy modules
    public string[]? References { get; set; }    // .NET assemblies to reference
}
```

---

## Key Methods

### Constructor with Options (Lines 27-58)

```csharp
public Compiler(CompilerOptions options, ICompilerLogger? logger = null)
```

**Purpose**: Initialize the compiler for multi-file project compilation with external dependencies.

**Implementation Details**:
1. Creates a `ModuleRegistry` to manage module imports
2. Registers all module search paths from options
3. Pre-loads referenced .NET assemblies
4. Logs success/failure for each reference

**Why This Matters**: Module resolution must happen **before** compilation starts. If a Sharpy file imports another module, the compiler needs to know where to find it. This constructor front-loads that discovery.

**Error Handling**: Reference loading failures are logged as warnings but don't fail the constructor. The actual compilation will fail later during semantic analysis if a required module is missing.

---

### CompileProject Method (Lines 63-67)

```csharp
public ProjectCompilationResult CompileProject(ProjectConfig projectConfig)
```

**Purpose**: Entry point for compiling an entire Sharpy project (.spyproj file).

**Architecture Decision**: Delegates to a specialized `ProjectCompiler` rather than implementing multi-file logic directly. This keeps `Compiler.cs` focused on single-file orchestration while `ProjectCompiler` handles file discovery, dependency ordering, and assembly generation.

**Usage Pattern**:
```csharp
var options = new CompilerOptions { ModulePaths = new[] { "./lib" } };
var compiler = new Compiler(options, logger);
var result = compiler.CompileProject(projectConfig);
```

---

### Compile Method - The Main Pipeline (Lines 69-204)

```csharp
public CompilationResult Compile(string sourceCode, string filePath)
```

This is the **heart of the compiler**. Let's break down each phase:

#### Phase 1: Lexical Analysis (Lines 76-81)

```csharp
var lexer = new Lexer.Lexer(sourceCode, _logger);
var tokens = lexer.TokenizeAll();
```

**What Happens**: Source code string is converted into a sequence of tokens (keywords, identifiers, operators, literals).

**Output**: `List<Token>` where each token knows its type, value, and source position.

**Debugging Tip**: If you see "unexpected token" errors, add logging here to inspect the token stream. The lexer might be misinterpreting certain character sequences.

---

#### Phase 2: Syntax Analysis (Lines 84-88)

```csharp
var parser = new Parser.Parser(tokens, _logger);
var module = parser.ParseModule();
```

**What Happens**: Token stream is transformed into an Abstract Syntax Tree (AST) representing the program structure.

**Output**: `Module` AST node containing all top-level declarations (functions, classes, imports).

**Connection Point**: The `Module` AST is passed to all subsequent phases. If parsing fails, compilation halts here.

---

#### Phase 3: Semantic Analysis (Lines 91-142)

This phase has **three sub-passes** - a critical design decision for handling forward references and circular dependencies:

##### Module Registry Check (Lines 97-105)

```csharp
if (_moduleRegistry != null && _moduleRegistry.Errors.Any())
{
    return new CompilationResult
    {
        Success = false,
        Errors = _moduleRegistry.Errors.Select(e => e.Message).ToList(),
        Metrics = metrics
    };
}
```

**Purpose**: Before proceeding with semantic analysis, verify that all external module references were loaded successfully.

**Early Exit**: If module loading failed during construction, we catch it here before attempting name resolution.

---

##### Sub-Pass 1: Name Resolution (Lines 107-122)

```csharp
var nameResolver = new NameResolver(symbolTable, _logger);
nameResolver.ResolveDeclarations(module);
nameResolver.ResolveInheritance();  // Second pass for inheritance
```

**Purpose**: Build the symbol table by discovering all declarations.

**Two-Phase Design**:
1. **ResolveDeclarations**: Registers all top-level names (classes, functions, variables)
2. **ResolveInheritance**: Resolves base classes now that all types are known

**Why Two Phases?** Handles forward references:
```python
class Child(Parent):  # Parent not declared yet
    pass

class Parent:
    pass
```

**Early Exit**: If name resolution errors occur (duplicate names, undefined base classes), compilation stops. Type checking cannot proceed without a valid symbol table.

---

##### Sub-Pass 2: Type Resolution and Type Checking (Lines 125-142)

```csharp
var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
typeChecker.CheckModule(module);
```

**Type Resolution**: Maps type names to their definitions (e.g., "List[int]" → generic instantiation).

**Type Checking**: Validates that expressions have compatible types (e.g., can't add a string to an integer).

**SemanticInfo**: Accumulates type annotations for every expression in the AST. The code generator uses this to emit correctly-typed C# code.

**Critical Dependency**: TypeChecker depends on TypeResolver. If you modify type resolution logic, verify type checking still works correctly.

---

##### Sub-Pass 3: Semantic Validation (Line 144)

```csharp
// TODO: Pass 3: Semantic validation (will implement in Phase 3)
```

**Future Work**: Reserved for additional checks like:
- Unreachable code detection
- Unused variable warnings
- Control flow analysis (e.g., return statement coverage)

---

#### Phase 4: Code Generation (Lines 147-181)

```csharp
var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
{
    SourceFilePath = filePath,
    ProjectNamespace = $"Sharpy.{ToPascalCase(defaultNamespace)}",
    IsEntryPoint = true,
    Logger = _logger
};
var emitter = new RoslynEmitter(codeGenContext);
var compilationUnit = emitter.GenerateCompilationUnit(module);
var csharpCode = compilationUnit.ToFullString();
```

**Namespace Generation**: For single-file compilation, derives namespace from filename:
- `hello_world.spy` → `Sharpy.HelloWorld` namespace

**Entry Point Flag**: Single-file compilation always sets `IsEntryPoint = true`, which tells RoslynEmitter to generate a `Main` method.

**RoslynEmitter**: Uses Microsoft.CodeAnalysis.CSharp to construct a C# syntax tree, then converts it to string. This ensures generated code is syntactically valid C#.

**Error Collection**: CodeGenContext accumulates errors during emission. Unlike earlier phases, code generation errors are collected in the context object rather than thrown as exceptions.

---

#### Error Handling Strategy (Lines 97-105, 114-122, 134-142, 173-181)

**Pattern**: After each major phase, check for errors and return early:

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

**Why This Design?**
- **Fail-fast**: No point running code generation if type checking failed
- **Clear error attribution**: Each phase's errors are reported separately
- **Performance**: Skips expensive phases when earlier ones fail

---

#### Exception Handling (Lines 194-203)

```csharp
catch (Exception ex)
{
    _logger.LogError($"Compilation failed with exception: {ex.Message}", 0, 0);
    return new CompilationResult
    {
        Success = false,
        Errors = new List<string> { $"Compilation failed: {ex.Message}" },
        Metrics = metrics
    };
}
```

**Defensive Programming**: Catches unexpected exceptions (e.g., file I/O errors, null reference bugs) and converts them to compilation errors rather than crashing.

**Logging**: Exceptions are logged before being converted to errors, helping with post-mortem debugging.

---

### ToPascalCase Utility Method (Lines 210-241)

```csharp
private static string ToPascalCase(string name)
```

**Purpose**: Convert file names to valid C# namespace identifiers.

**Transformations**:
- `hello_world` → `HelloWorld` (snake_case)
- `my-module` → `MyModule` (kebab-case)
- `123abc` → `_123abc` (prefixes digits)
- `file@name` → `FileName` (sanitizes special chars)

**Algorithm**:
1. Replace non-alphanumeric characters (except `_`) with underscores
2. Split by underscore
3. Capitalize first letter of each part
4. Join parts together
5. Prefix with `_` if result starts with a digit

**Why This Matters**: C# namespaces must be valid identifiers. User-provided filenames might contain spaces, hyphens, or other invalid characters.

**Example Edge Cases**:
```csharp
ToPascalCase("test-file.spy") → "TestFile"    // Extension removed before calling
ToPascalCase("2fast2furious") → "_2fast2furious"
ToPascalCase("___") → "_"                      // Degenerate case
```

---

## Dependencies

### Internal Sharpy Dependencies

This file depends on **all major compiler subsystems**:

1. **Sharpy.Compiler.Lexer** (`Lexer` class)
   - Converts source code to tokens
   - See: `src/Sharpy.Compiler/Lexer/Lexer.cs`

2. **Sharpy.Compiler.Parser** (`Parser` class, `Ast` namespace)
   - Converts tokens to AST
   - See: `src/Sharpy.Compiler/Parser/Parser.cs`
   - AST node types: `src/Sharpy.Compiler/Parser/Ast/*.cs`

3. **Sharpy.Compiler.Semantic**
   - `NameResolver`: Symbol table construction
   - `TypeResolver`: Type name resolution
   - `TypeChecker`: Type validation
   - `SymbolTable`, `SemanticInfo`: Data structures
   - `BuiltinRegistry`: Built-in type definitions
   - See: `src/Sharpy.Compiler/Semantic/*.cs`

4. **Sharpy.Compiler.CodeGen** (`RoslynEmitter`, `CodeGenContext`)
   - C# code generation from AST
   - See: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.*.cs` (partial classes)

5. **Sharpy.Compiler.Project** (`ProjectCompiler`, `ProjectConfig`)
   - Multi-file project compilation
   - See: `src/Sharpy.Compiler/Project/*.cs`

6. **Sharpy.Compiler.Logging** (`ICompilerLogger`, `NullLogger`)
   - Diagnostic output abstraction
   - See: `src/Sharpy.Compiler/Logging/ICompilerLogger.cs`

7. **Sharpy.Compiler.Diagnostics** (`CompilationMetrics`)
   - Performance tracking
   - See: `src/Sharpy.Compiler/Diagnostics/*.cs`

### External Dependencies

- **Microsoft.CodeAnalysis.CSharp**: Used by RoslynEmitter for generating C# syntax trees (Roslyn compiler API)

---

## Patterns and Design Decisions

### 1. **Pipeline Pattern**

The compiler implements a classic **staged pipeline**:
```
Source → Tokens → AST → Semantics → C# Code
```

Each stage is independent and can be tested in isolation. This is a textbook compiler architecture (see "Dragon Book").

### 2. **Fail-Fast Error Handling**

Each phase checks for errors before proceeding. This prevents cascading errors where one phase's failures cause confusing errors in subsequent phases.

**Alternative Considered**: Collect all errors from all phases. **Rejected** because semantic errors without a valid AST are often nonsensical.

### 3. **Logging Abstraction**

Uses `ICompilerLogger` interface with a `NullLogger` default. This enables:
- Unit testing without console spam
- IDE integration with custom loggers
- Performance analysis by swapping logger implementations

### 4. **Immutable Results**

`CompilationResult` uses `init` properties. Once created, it cannot be modified. This prevents bugs where compilation results are accidentally altered after being returned.

### 5. **Metrics Tracking**

Every phase is timed using `CompilationMetrics`. This helps identify performance bottlenecks:
```csharp
metrics.StartPhase("Lexical Analysis");
// ... work ...
metrics.EndPhase();
```

**Usage**: CLI tools can display timing information to users. Build systems can track compilation speed regressions.

### 6. **Optional Module Registry**

The module registry is nullable. This design supports two modes:
- **Standalone**: Compile a single file without imports
- **Project**: Compile multiple files with cross-module dependencies

**Alternative Considered**: Always create a registry. **Rejected** because it adds complexity for simple single-file scenarios.

---

## Debugging Tips

### Debugging Compilation Failures

1. **Enable Verbose Logging**:
   ```csharp
   var logger = new ConsoleLogger(LogLevel.Debug);
   var compiler = new Compiler(logger);
   ```
   This shows which phase failed and internal details.

2. **Inspect Intermediate Results**:
   ```csharp
   var result = compiler.Compile(source, filePath);
   if (!result.Success)
   {
       // Inspect result.Module (AST)
       // Inspect result.SymbolTable (name bindings)
       // Inspect result.SemanticInfo (type annotations)
   }
   ```

3. **Check Error Phase**:
   - Lexer errors → Usually syntax issues (unclosed strings, invalid characters)
   - Parser errors → Malformed code structure (missing colons, unbalanced parens)
   - NameResolver errors → Undefined names, duplicate declarations
   - TypeChecker errors → Type mismatches, invalid operations
   - CodeGen errors → Usually compiler bugs, not user code issues

4. **Module Registry Issues**:
   ```csharp
   if (result.ModuleRegistry != null && result.ModuleRegistry.Errors.Any())
   {
       // Import resolution failed - check module paths
   }
   ```

### Common Pitfalls

1. **Null Logger**: If logger is null, you won't see any diagnostic output. Always provide a logger during debugging.

2. **Module Path Confusion**: If imports fail, verify:
   - ModulePaths in CompilerOptions are correct
   - Referenced assemblies exist at specified paths
   - File extensions are correct (.dll for assemblies, .spy for Sharpy modules)

3. **Metrics Not Started**: If you add a new phase, remember to call `StartPhase`/`EndPhase`. Forgetting this will cause timing data to be incorrect or throw exceptions.

---

## Contribution Guidelines

### When to Modify This File

**Add Code Here**:
- Adding a new compilation phase (e.g., optimization pass, additional semantic checks)
- Changing the order of compilation phases
- Adding new configuration options to `CompilerOptions`
- Extending `CompilationResult` with new artifacts
- Modifying namespace generation logic for single-file compilation

**Don't Add Code Here**:
- Lexer logic → Modify `Lexer.cs`
- Parser logic → Modify `Parser.cs`
- Type checking logic → Modify `TypeChecker.cs`
- Code generation logic → Modify `RoslynEmitter.*.cs`
- Project-specific logic → Modify `ProjectCompiler.cs`

### Adding a New Compilation Phase

**Example**: Adding a "constant folding" optimization phase:

```csharp
// Phase 3.5: Constant Folding (after type checking, before code gen)
_logger.LogInfo("Phase 3.5: Constant Folding");
metrics.StartPhase("Constant Folding");
var optimizer = new ConstantFolder(semanticInfo, _logger);
optimizer.OptimizeModule(module);
metrics.EndPhase();

if (optimizer.Errors.Any())
{
    return new CompilationResult
    {
        Success = false,
        Errors = optimizer.Errors.Select(e => e.Message).ToList(),
        Metrics = metrics
    };
}
```

**Checklist**:
- [ ] Add phase logging
- [ ] Add metrics tracking
- [ ] Add error handling with early return
- [ ] Update `CompilationResult` if new data is produced
- [ ] Update tests to cover new phase

### Modifying CompilationResult

**When Adding Fields**:
1. Use `init` for immutability
2. Make fields nullable if they might not always be available
3. Update all return statements in `Compile()` that create `CompilationResult`
4. Add corresponding tests

**Example**:
```csharp
public class CompilationResult
{
    // ... existing fields ...
    public OptimizationReport? OptimizationReport { get; init; }  // New field
}
```

### Testing Changes

When modifying this file, ensure:
1. **Unit tests** pass for each individual phase (Lexer, Parser, etc.)
2. **Integration tests** pass for end-to-end compilation
3. **Error path tests** verify correct failure handling
4. **Performance tests** check that metrics are tracked correctly

**Test File Locations**:
- Unit tests: `tests/Sharpy.Compiler.Tests/CompilerTests.cs`
- Integration tests: `tests/Sharpy.Integration.Tests/`

---

## Cross-References

### Related Documentation

- **RoslynEmitter Documentation**: See `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/RoslynEmitter.*.md` for code generation details
  - `RoslynEmitter.CompilationUnit.md` - Overall structure generation
  - `RoslynEmitter.ClassMembers.md` - Member generation details
  - `RoslynEmitter.Expressions.md` - Expression translation
  - `RoslynEmitter.Statements.md` - Statement translation

- **CodeGenContext Documentation**: See `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/CodeGenContext.md` for configuration options

- **Program.cs CLI Integration**: See `docs/implementation_walkthrough/src/Sharpy.Cli/Program.md` for how the CLI invokes the compiler

### Component Dependencies Graph

```
Compiler.cs (THIS FILE)
    ├─> Lexer.Lexer
    ├─> Parser.Parser
    │       └─> Parser.Ast.Module
    ├─> Semantic.NameResolver
    │       └─> Semantic.SymbolTable
    ├─> Semantic.TypeResolver
    ├─> Semantic.TypeChecker
    │       └─> Semantic.SemanticInfo
    ├─> CodeGen.RoslynEmitter
    │       └─> CodeGen.CodeGenContext
    ├─> Project.ProjectCompiler
    │       └─> Project.ProjectConfig
    └─> Diagnostics.CompilationMetrics
```

### Files You'll Likely Edit Together

When making changes to compilation orchestration, you'll often modify:
1. `Compiler.cs` (this file) - Pipeline orchestration
2. `ProjectCompiler.cs` - Multi-file compilation logic
3. `CompilationMetrics.cs` - Performance tracking
4. `ICompilerLogger.cs` - Logging interface
5. Test files in `tests/Sharpy.Compiler.Tests/`

---

## Summary

The `Compiler.cs` file is the **orchestration layer** that ties together all compiler subsystems into a cohesive pipeline. It's designed to be:

- **Simple**: Each phase is clearly delineated with explicit error handling
- **Extensible**: New phases can be inserted between existing ones
- **Debuggable**: Extensive logging and metrics tracking
- **Maintainable**: Delegates complex logic to specialized components

**Key Takeaway**: This file doesn't implement compiler logic itself - it **coordinates** other components. When debugging, determine which phase failed, then dive into that phase's implementation (Lexer, Parser, TypeChecker, etc.) for the actual bug.
