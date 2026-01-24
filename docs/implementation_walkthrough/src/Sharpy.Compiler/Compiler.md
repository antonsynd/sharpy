# Walkthrough: Compiler.cs

**Source File**: `src/Sharpy.Compiler/Compiler.cs`

---

## Overview

The `Compiler.cs` file is the **main orchestrator** of the Sharpy compilation pipeline. It acts as the entry point that coordinates all compilation phases from source code (.spy files) to generated C# code. This file implements the classic compiler frontend pattern, executing a series of well-defined phases: lexical analysis, syntax analysis, semantic analysis, validation, and code generation.

**Role in Pipeline**: This is the conductor that brings together all the specialized compiler components (Lexer, Parser, NameResolver, TypeChecker, ValidationPipeline, RoslynEmitter) and ensures they execute in the correct order with proper error handling and logging.

**Key Responsibilities**:
- Orchestrating the multi-phase compilation pipeline
- Managing compilation options and module registries
- Collecting and reporting errors from each phase
- Tracking compilation metrics for performance analysis
- Supporting both single-file and multi-file project compilation
- Generating appropriate namespaces and entry points for compiled code

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
    public DependencyGraph? DependencyGraph { get; init; }                 // Build order analysis
    public ProjectModel? ProjectModel { get; init; }                       // All CompilationUnits
}
```

**Key Differences**: 
- Includes `Warnings` and maps multiple source files to their generated C# counterparts
- Provides `DependencyGraph` for tooling (incremental compilation, build visualization)
- Exposes `ProjectModel` with all CompilationUnits for analysis tools

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
var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger, pipeline);
typeChecker.CheckModule(module, computeCodeGenInfo: true);
```

**Type Resolution**: Maps type names to their definitions (e.g., "List[int]" → generic instantiation).

**Validation Pipeline**: A pluggable system of validators that check various semantic properties:
- Operator validation (ensuring operators are implemented correctly)
- Protocol validation (checking interface implementations)
- Access control validation (public/private/protected)

**Type Checking**: Validates that expressions have compatible types (e.g., can't add a string to an integer).

**SemanticInfo**: Accumulates type annotations for every expression in the AST. The code generator uses this to emit correctly-typed C# code.

**CodeGenInfo Parameter**: Setting `computeCodeGenInfo: true` tells the type checker to compute additional metadata needed for code generation (e.g., which methods need special handling).

**Critical Dependency**: TypeChecker depends on TypeResolver and ValidationPipeline. If you modify type resolution logic or add validators, verify type checking still works correctly.

---

##### Sub-Pass 3: Semantic Validation

**Current Status**: Validation is now integrated into the TypeChecker via the ValidationPipeline (see Sub-Pass 2 above).

The ValidationPipeline pattern allows for:
- **Pluggable validators**: New semantic checks can be added without modifying TypeChecker
- **Ordered execution**: Validators run in a specific order (operator → protocol → access)
- **Error accumulation**: All validation errors are collected before reporting

**Available Validators**:
- `OperatorValidatorV2`: Ensures operators follow protocol rules
- `ProtocolValidator`: Verifies protocol implementations
- `AccessValidator`: Checks visibility rules

**Future Enhancements** could include:
- Unreachable code detection
- Unused variable warnings
- Exhaustiveness checking for match statements
- Control flow analysis (e.g., return statement coverage)

---

#### Phase 4: Code Generation (Lines 147-181)

```csharp
// Derive namespace from filename for single-file compilation
var defaultNamespace = !string.IsNullOrEmpty(filePath)
    ? Path.GetFileNameWithoutExtension(filePath)
    : null;

var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
{
    SourceFilePath = filePath,
    // Single-file: use file-based namespace
    ProjectNamespace = !string.IsNullOrEmpty(defaultNamespace)
        ? $"Sharpy.{ToPascalCase(defaultNamespace)}"
        : null,
    IsEntryPoint = true,  // Generate Main method
    Logger = _logger
};

var emitter = new RoslynEmitter(codeGenContext);
var compilationUnit = emitter.GenerateCompilationUnit(module);
var csharpCode = compilationUnit.ToFullString();
```

**Namespace Generation Strategy**: 
- Single-file: `filename.spy` → `Sharpy.{PascalCaseName}` namespace
- Multi-file: Uses project root path and calculates relative namespacing
- The distinction is determined by whether `ProjectRootPath` is set in context

**Entry Point Flag**: Single-file compilation always sets `IsEntryPoint = true`, which tells RoslynEmitter to:
- Generate a `Main` method that calls module-level code
- Mark the class as `static` if appropriate
- Handle command-line arguments if needed

**RoslynEmitter Architecture**: 
- Uses Microsoft.CodeAnalysis.CSharp (Roslyn) to construct C# syntax trees
- **Exclusively uses `SyntaxFactory` methods** - no string templating
- Converts syntax tree to string using `ToFullString()`
- This ensures generated code is syntactically valid C# by construction

**Error Collection**: 
- `CodeGenContext` accumulates errors during emission
- Unlike earlier phases, errors are collected in the context rather than thrown
- Check `codeGenContext.HasErrors` after emission completes

**Performance Note**: `ToFullString()` includes all trivia (whitespace, comments), making generated code more readable but slightly slower than `ToString()`.

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

### CreateServices Method (Lines 210-225)

```csharp
private CompilerServices CreateServices(
    SymbolTable symbolTable,
    SemanticInfo semanticInfo,
    TypeResolver typeResolver,
    ClrMemberCache? clrCache = null)
{
    return new CompilerServicesBuilder()
        .WithLogger(_logger)
        .WithSymbolTable(symbolTable)
        .WithSemanticInfo(semanticInfo)
        .WithTypeResolver(typeResolver)
        .WithClrCache(clrCache ?? new ClrMemberCache())
        .Build();
}
```

**Purpose**: Factory method for creating a unified service container that bundles compiler services.

**Builder Pattern**: Uses `CompilerServicesBuilder` for fluent configuration of services.

**Components**:
- **Logger**: Diagnostic output
- **SymbolTable**: Name bindings and declarations
- **SemanticInfo**: Type annotations
- **TypeResolver**: Type name resolution
- **ClrMemberCache**: Caches .NET CLR member lookups for performance

**Usage Context**: This method isn't currently called in the main compilation flow but is available for tooling that needs to access compiler services (e.g., IDE integrations, language servers).

**Design Note**: The services pattern provides a clean interface for external tools to interact with compiler internals without directly coupling to implementation details.

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

**Real-World Examples**:
```csharp
ToPascalCase("hello_world")      → "HelloWorld"
ToPascalCase("my-module")        → "MyModule"
ToPascalCase("test_file_2")      → "TestFile2"
ToPascalCase("@special#chars$")  → "SpecialChars"
```

**Why This Matters**: 
- C# namespaces must be valid identifiers
- User-provided filenames might contain spaces, hyphens, or other invalid characters
- Consistent naming helps developers predict generated namespace names

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

4. **Sharpy.Compiler.Semantic.Validation**
   - `ValidationPipeline`: Pluggable validator orchestration
   - `ValidationPipelineFactory`: Creates default validator chains
   - `OperatorValidatorV2`: Validates operator implementations
   - `ProtocolValidator`: Checks protocol/interface conformance
   - `AccessValidator`: Enforces visibility rules
   - See: `src/Sharpy.Compiler/Semantic/Validation/*.cs`

5. **Sharpy.Compiler.CodeGen** (`RoslynEmitter`, `CodeGenContext`)
   - C# code generation from AST
   - See: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.*.cs` (partial classes)

6. **Sharpy.Compiler.Project** (`ProjectCompiler`, `ProjectConfig`)
   - Multi-file project compilation
   - `DependencyGraph`: Build order analysis
   - `ProjectModel`: Compilation unit management
   - See: `src/Sharpy.Compiler/Project/*.cs`

7. **Sharpy.Compiler.Discovery** (`ModuleRegistry`)
   - Module import resolution
   - .NET assembly reference loading
   - Module path management
   - See: `src/Sharpy.Compiler/Discovery/*.cs`

8. **Sharpy.Compiler.Services**
   - `CompilerServices`: Service container
   - `CompilerServicesBuilder`: Fluent builder for services
   - `ClrMemberCache`: Performance optimization for .NET member lookups
   - See: `src/Sharpy.Compiler/Services/*.cs`

9. **Sharpy.Compiler.Logging** (`ICompilerLogger`, `NullLogger`)
   - Diagnostic output abstraction
   - See: `src/Sharpy.Compiler/Logging/ICompilerLogger.cs`

10. **Sharpy.Compiler.Diagnostics** (`CompilationMetrics`)
   - Performance tracking
   - See: `src/Sharpy.Compiler/Diagnostics/*.cs`

### External Dependencies

- **Microsoft.CodeAnalysis.CSharp**: Used by RoslynEmitter for generating C# syntax trees (Roslyn compiler API)

---

## Patterns and Design Decisions

### 1. **Pipeline Pattern**

The compiler implements a classic **staged pipeline**:
```
Source → Tokens → AST → Semantics → Validation → C# Code
```

Each stage is independent and can be tested in isolation. This is a textbook compiler architecture (see "Dragon Book").

**Key Improvement**: The addition of the ValidationPipeline as a separate stage allows for pluggable semantic checks without coupling them to the type checker.

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

### 7. **Pluggable Validation**

The `ValidationPipeline` pattern decouples semantic validation from type checking:
- **Extensibility**: New validators can be added without modifying TypeChecker
- **Composition**: Different validator combinations for different contexts
- **Testing**: Each validator can be tested independently

This follows the Open/Closed Principle: open for extension, closed for modification.

### 8. **Namespace Derivation Strategy**

Single-file compilation derives namespace from filename, while multi-file uses project structure:
- Simplifies single-file usage (no project config needed)
- Provides flexibility for complex projects
- Uses `ToPascalCase` to ensure valid C# identifiers

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
   - Validation errors → Operator/protocol violations, access control issues
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
- Adding new validators to the ValidationPipeline

**Don't Add Code Here**:
- Lexer logic → Modify `Lexer.cs`
- Parser logic → Modify `Parser.cs`
- Type checking logic → Modify `TypeChecker.cs`
- Validation logic → Create new validator in `Semantic/Validation/`
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

### Adding a New Validator

**Example**: Adding a validator for checking unused variables:

1. **Create the validator class**:
```csharp
// src/Sharpy.Compiler/Semantic/Validation/UnusedVariableValidator.cs
public class UnusedVariableValidator : IValidator
{
    public void Validate(Module module, ValidationContext context)
    {
        // Implementation
    }
}
```

2. **Register in ValidationPipelineFactory**:
```csharp
public static ValidationPipeline CreateDefault(ICompilerLogger logger)
{
    return new ValidationPipeline(logger)
        .AddValidator(new OperatorValidatorV2())
        .AddValidator(new ProtocolValidator())
        .AddValidator(new AccessValidator())
        .AddValidator(new UnusedVariableValidator());  // New validator
}
```

3. **No changes needed to Compiler.cs** - the pipeline automatically runs all validators!

---

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
- Unit tests: `src/Sharpy.Compiler.Tests/CompilerTests.cs`
- Integration tests: `src/Sharpy.Compiler.Tests/Integration/`
- File-based integration tests: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`

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
    │       ├─> Semantic.SemanticInfo
    │       └─> Semantic.Validation.ValidationPipeline
    │               ├─> OperatorValidatorV2
    │               ├─> ProtocolValidator
    │               └─> AccessValidator
    ├─> CodeGen.RoslynEmitter
    │       └─> CodeGen.CodeGenContext
    ├─> Project.ProjectCompiler
    │       ├─> Project.ProjectConfig
    │       ├─> Project.DependencyGraph
    │       └─> Model.ProjectModel
    ├─> Discovery.ModuleRegistry
    ├─> Services.CompilerServices
    │       └─> Services.CompilerServicesBuilder
    └─> Diagnostics.CompilationMetrics
```

### Files You'll Likely Edit Together

When making changes to compilation orchestration, you'll often modify:
1. `Compiler.cs` (this file) - Pipeline orchestration
2. `ProjectCompiler.cs` - Multi-file compilation logic
3. `ValidationPipelineFactory.cs` - Validator registration
4. `CompilationMetrics.cs` - Performance tracking
5. `ICompilerLogger.cs` - Logging interface
6. Test files in `src/Sharpy.Compiler.Tests/`

### Partial Class Architecture

While `Compiler.cs` itself is not a partial class, it heavily interacts with components that are:
- **RoslynEmitter**: Split across `RoslynEmitter.CompilationUnit.cs`, `RoslynEmitter.ClassMembers.cs`, `RoslynEmitter.Expressions.cs`, `RoslynEmitter.Statements.cs`, etc.
- **Sharpy.Core**: Standard library split across `Partial.List/`, `Partial.Str/`, `Partial.Dict/`, etc.

When debugging code generation issues, you may need to explore multiple RoslynEmitter partial class files.

---

## Summary

The `Compiler.cs` file is the **orchestration layer** that ties together all compiler subsystems into a cohesive pipeline. It's designed to be:

- **Simple**: Each phase is clearly delineated with explicit error handling
- **Extensible**: New phases can be inserted between existing ones
- **Debuggable**: Extensive logging and metrics tracking
- **Maintainable**: Delegates complex logic to specialized components

**Key Takeaway**: This file doesn't implement compiler logic itself - it **coordinates** other components. When debugging, determine which phase failed, then dive into that phase's implementation (Lexer, Parser, TypeChecker, etc.) for the actual bug.

---

## Recent Enhancements

### ValidationPipeline Integration (Current)

The most significant recent addition is the integration of the `ValidationPipeline` into the type checking phase:

**Benefits**:
- **Separation of Concerns**: Validation logic is decoupled from type checking
- **Extensibility**: New validators can be added via factory pattern
- **Maintainability**: Each validator is an independent, testable unit

**Current Validators**:
1. `OperatorValidatorV2`: Ensures operators follow protocol-based design
2. `ProtocolValidator`: Verifies protocol/interface implementations
3. `AccessValidator`: Enforces visibility and access control rules

### CodeGenInfo Computation

The `TypeChecker.CheckModule` now accepts a `computeCodeGenInfo` parameter that, when true, pre-computes metadata needed for code generation:
- Method resolution information
- Operator implementation details  
- Protocol conformance data

This optimization reduces work during the code generation phase.

### Enhanced Project Compilation Results

`ProjectCompilationResult` now includes:
- `DependencyGraph`: Enables incremental compilation and build order analysis
- `ProjectModel`: Provides full compilation unit structure for tooling
- `Warnings`: Separate from errors for better diagnostics

These additions support advanced IDE features and build system integration.

---

## Quick Reference

**Compilation Success Check**:
```csharp
var result = compiler.Compile(sourceCode, filePath);
if (!result.Success)
{
    foreach (var error in result.Errors)
        Console.Error.WriteLine(error);
}
```

**Accessing Generated Code**:
```csharp
if (result.Success)
{
    string csharpCode = result.GeneratedCSharpCode;
    File.WriteAllText("output.cs", csharpCode);
}
```

**Performance Analysis**:
```csharp
if (result.Metrics != null)
{
    Console.WriteLine($"Lexing: {result.Metrics.GetPhaseTime("Lexical Analysis")}ms");
    Console.WriteLine($"Parsing: {result.Metrics.GetPhaseTime("Syntax Analysis")}ms");
    Console.WriteLine($"Total: {result.Metrics.TotalTime}ms");
}
```

**Module Compilation with References**:
```csharp
var options = new CompilerOptions
{
    ModulePaths = new[] { "./libs", "./modules" },
    References = new[] { "MyAssembly.dll", "System.Collections.dll" }
};
var compiler = new Compiler(options, logger);
var result = compiler.Compile(sourceCode, filePath);
```
