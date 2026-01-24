# Walkthrough: Compiler.cs

**Source File**: `src/Sharpy.Compiler/Compiler.cs`

---

## Overview

`Compiler.cs` is the **main orchestrator** of the Sharpy compilation pipeline. It acts as the entry point that coordinates all compilation phases, from tokenization through code generation. Think of it as the conductor of an orchestra—it doesn't implement the individual instruments (lexer, parser, semantic analyzer), but it ensures they all play together in the right order.

**Role in the Pipeline**: This is the top-level API that consumers (like the CLI or IDE tooling) use to compile Sharpy source code into C#. It manages the flow: **Source (.spy) → Lexer → Parser → Semantic Analysis → Code Generation → C#**.

---

## Class/Type Structure

### 1. `Compiler` Class

The main driver class with two key responsibilities:
- **Single-file compilation**: `Compile(string sourceCode, string filePath)`
- **Project compilation**: `CompileProject(ProjectConfig projectConfig)`

**Fields**:
- `_logger`: An `ICompilerLogger` for diagnostic output (defaults to `NullLogger` if not provided)
- `_moduleRegistry`: Optional `ModuleRegistry` for managing multi-module projects and external assembly references

**Constructors**:
```csharp
public Compiler(ICompilerLogger? logger = null)
public Compiler(CompilerOptions options, ICompilerLogger? logger = null)
```

The parameterless constructor is used for simple single-file compilation. The second constructor initializes module support, loading module search paths and assembly references from `CompilerOptions`.

### 2. `CompilationResult` Class

A data transfer object (DTO) that captures the outcome of compiling a single file. It's immutable (`init`-only properties).

**Key Properties**:
- `Success`: Did the compilation succeed?
- `Errors`: List of error messages (empty if successful)
- `Module`: The parsed AST (Abstract Syntax Tree)
- `SymbolTable`: All declared symbols (variables, functions, classes)
- `SemanticInfo`: Type annotations and semantic metadata attached to AST nodes
- `GeneratedCSharpCode`: The final C# code generated
- `Metrics`: Performance timing for each compilation phase

### 3. `ProjectCompilationResult` Class

Similar to `CompilationResult`, but for multi-file projects.

**Additional Properties**:
- `Warnings`: Non-fatal diagnostics
- `OutputAssemblyPath`: Path to the compiled .NET assembly
- `GeneratedCSharpFiles`: Map of `.spy` file paths to generated C# code
- `DependencyGraph`: Topological ordering of module dependencies
- `ProjectModel`: Complete project structure with all compilation units

### 4. `CompilerOptions` Class

Configuration for compiler initialization.

**Properties**:
- `ModulePaths`: Directories to search for Sharpy module assemblies
- `References`: Paths to .NET DLLs to reference during compilation

---

## Key Methods

### `Compile(string sourceCode, string filePath)`

**Purpose**: Orchestrates the four-phase compilation pipeline for a single source file.

**Parameters**:
- `sourceCode`: The raw `.spy` file content as a string
- `filePath`: Path to the source file (used for diagnostics and namespace generation)

**Return Value**: `CompilationResult` with success status and generated artifacts.

**Implementation Details**:

The method executes four sequential phases, wrapping each with metrics tracking:

#### **Phase 1: Lexical Analysis** (lines 78-83)
```csharp
var lexer = new Lexer.Lexer(sourceCode, _logger);
var tokens = lexer.TokenizeAll();
```
Converts raw source text into a stream of tokens (e.g., `IDENTIFIER`, `NUMBER`, `INDENT`).

#### **Phase 2: Syntax Analysis** (lines 85-90)
```csharp
var parser = new Parser.Parser(tokens, _logger);
var module = parser.ParseModule();
```
Builds an Abstract Syntax Tree (AST) from tokens. The `Module` object represents the top-level container for all declarations and statements in the file.

#### **Phase 3: Semantic Analysis** (lines 92-146)

This is the most complex phase with multiple sub-passes:

1. **Setup** (lines 94-107):
   - Initialize `BuiltinRegistry` (contains Python built-in functions like `print`, `len`)
   - Create `SymbolTable` (tracks all user-defined symbols)
   - Create `SemanticInfo` (stores type information for AST nodes)
   - Check for module registry errors

2. **Name Resolution** (lines 109-124):
   ```csharp
   var nameResolver = new NameResolver(symbolTable, _logger);
   nameResolver.ResolveDeclarations(module);
   nameResolver.ResolveInheritance();
   ```
   - First pass: Register all declarations (functions, classes, variables) in the symbol table
   - Second pass: Resolve class inheritance chains
   - Early exit if errors occur (e.g., duplicate declarations, circular inheritance)

3. **Type Resolution and Checking** (lines 126-146):
   ```csharp
   var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
   var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
   var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger, pipeline);
   typeChecker.CheckModule(module, computeCodeGenInfo: true, isEntryPoint: true);
   ```
   - Type resolution: Map Sharpy types to .NET types
   - Type checking: Verify expressions have compatible types, function calls match signatures
   - `computeCodeGenInfo: true` tells the checker to prepare metadata for code generation
   - `isEntryPoint: true` indicates this file can define a `main()` function as the program entry point

#### **Phase 4: Code Generation** (lines 150-185)

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

**Key Design Decisions**:
- **Namespace Generation** (lines 154-166): For single-file compilation, the namespace is derived from the filename. For example, `hello_world.spy` becomes `Sharpy.HelloWorld`.
- **Entry Point Handling** (line 168): Single-file scripts always generate a `Main` method, allowing them to be executed directly.
- **RoslynEmitter**: Uses Microsoft's Roslyn `SyntaxFactory` API to generate syntactically correct C# code (no string templating).

**Error Handling**:
- After each phase, the method checks for errors and returns early with `Success = false` if any are found.
- A top-level try-catch (lines 198-207) handles unexpected exceptions.

**Performance Tracking**:
- `CompilationMetrics` tracks the duration of each phase, useful for profiling compiler performance.

---

### `CompileProject(ProjectConfig projectConfig)`

**Purpose**: Delegates to `ProjectCompiler` for multi-file project compilation.

```csharp
public ProjectCompilationResult CompileProject(ProjectConfig projectConfig)
{
    var projectCompiler = new ProjectCompiler(_logger, _moduleRegistry);
    return projectCompiler.Compile(projectConfig);
}
```

**Why delegate?** Project compilation is significantly more complex (dependency resolution, topological sorting, incremental compilation hints). Separating it into `ProjectCompiler` keeps this class focused on single-file orchestration.

---

### `ToPascalCase(string name)` (Private Helper)

**Purpose**: Converts file names like `hello_world` or `my-module` into valid C# namespace components like `HelloWorld` or `MyModule`.

**Algorithm**:
1. Sanitize: Replace non-alphanumeric characters (except `_`) with underscores
2. Split by `_` and capitalize each part
3. Prefix with `_` if the result starts with a digit (C# identifiers can't start with digits)

**Example**:
```csharp
ToPascalCase("hello_world")     // → "HelloWorld"
ToPascalCase("my-module-2")     // → "MyModule2"
ToPascalCase("42_answer")       // → "_42Answer"
```

---

### `CreateServices(...)` (Private Helper)

**Purpose**: Factory method for creating a `CompilerServices` instance using the builder pattern.

**Note**: This method is currently defined but **not used** in the codebase. It appears to be infrastructure for future features (possibly IDE integration or incremental compilation).

---

## Dependencies

### Internal Sharpy Dependencies

| Namespace | Purpose |
|-----------|---------|
| `Sharpy.Compiler.Lexer` | Tokenization (`Lexer` class) |
| `Sharpy.Compiler.Parser` | AST generation (`Parser` class, `Module` type) |
| `Sharpy.Compiler.Semantic` | Symbol table, type resolution (`SymbolTable`, `NameResolver`, `TypeResolver`, `TypeChecker`) |
| `Sharpy.Compiler.Semantic.Validation` | Validation pipeline (e.g., null safety checks) |
| `Sharpy.Compiler.CodeGen` | C# code generation (`RoslynEmitter`, `CodeGenContext`) |
| `Sharpy.Compiler.Logging` | Diagnostic output (`ICompilerLogger`, `NullLogger`) |
| `Sharpy.Compiler.Diagnostics` | Compilation metrics (`CompilationMetrics`) |
| `Sharpy.Compiler.Project` | Multi-file project support (`ProjectCompiler`, `ProjectConfig`, `ModuleRegistry`) |
| `Sharpy.Compiler.Services` | Compiler services infrastructure (`CompilerServices`, `ClrMemberCache`) |

### External Dependencies

- **Microsoft.CodeAnalysis.CSharp**: Roslyn API for C# syntax tree generation (used in `RoslynEmitter`)

---

## Patterns and Design Decisions

### 1. **Pipeline Architecture**

The compiler follows a classic **sequential pipeline** pattern where each phase consumes the output of the previous phase:

```
Source Text → Tokens → AST → Annotated AST → C# Code
```

This design:
- **Separates concerns**: Each phase has a single responsibility
- **Enables testing**: You can test the lexer independently of the parser
- **Supports tooling**: IDEs can run just the parser to get AST for syntax highlighting

### 2. **Fail-Fast Error Handling**

After each phase, the method checks for errors and returns immediately:

```csharp
if (nameResolver.Errors.Any())
{
    return new CompilationResult { Success = false, Errors = ... };
}
```

**Why?** No point in running type checking if name resolution failed—the symbol table would be incomplete.

### 3. **Immutable Result Objects**

`CompilationResult` and `ProjectCompilationResult` use `init`-only properties:

```csharp
public bool Success { get; init; }
```

This makes results **immutable after construction**, preventing accidental modification and making them safe to share across threads.

### 4. **Dependency Injection for Logging**

The constructor accepts an optional `ICompilerLogger`:

```csharp
public Compiler(ICompilerLogger? logger = null)
{
    _logger = logger ?? NullLogger.Instance;
}
```

**Benefits**:
- Testing: Inject a mock logger to verify diagnostic messages
- Flexibility: Console apps use `ConsoleLogger`, IDEs use custom loggers

### 5. **Null-Object Pattern**

Instead of null-checking `_logger` everywhere, the code uses `NullLogger.Instance` (a no-op logger) as a default. This eliminates conditional logic:

```csharp
_logger.LogInfo("Phase 1: Lexical Analysis"); // Safe even if no logger provided
```

### 6. **Metrics Collection**

`CompilationMetrics` wraps each phase with `StartPhase` / `EndPhase` calls:

```csharp
metrics.StartPhase("Lexical Analysis");
var tokens = lexer.TokenizeAll();
metrics.EndPhase();
```

This provides **observability** into which phases are slow, crucial for performance optimization.

---

## Debugging Tips

### 1. **Inspecting Compilation Phases**

When debugging compilation failures, check which phase produced the error:

```bash
# Use the CLI to emit intermediate representations
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy   # Lexer output
dotnet run --project src/Sharpy.Cli -- emit ast file.spy      # Parser output
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy   # Code generator output
```

### 2. **Logging Configuration**

Set breakpoints in `Compiler.Compile` and inspect `_logger` calls to see what phase failed:

```csharp
_logger.LogInfo($"Starting compilation of {filePath}");  // Line 73
_logger.LogInfo("Phase 1: Lexical Analysis");            // Line 79
_logger.LogInfo("Phase 2: Syntax Analysis");             // Line 86
_logger.LogInfo("Phase 3: Semantic Analysis");           // Line 93
_logger.LogInfo("Phase 4: Code Generation");             // Line 151
```

### 3. **Common Error Patterns**

| Error Location | Likely Cause |
|----------------|--------------|
| Lines 99-107 (module registry) | Missing assembly reference or invalid module path |
| Lines 116-124 (name resolver) | Duplicate declaration, circular inheritance, undefined type |
| Lines 138-146 (type checker) | Type mismatch, incompatible assignment, invalid function call |
| Lines 177-185 (code gen context) | Internal compiler bug (code generator couldn't map AST to C#) |

### 4. **Inspecting Generated C#**

The `GeneratedCSharpCode` property contains the final output. You can write it to a file and inspect with a C# IDE:

```csharp
var result = compiler.Compile(sourceCode, "test.spy");
if (result.Success)
{
    File.WriteAllText("test.generated.cs", result.GeneratedCSharpCode);
}
```

### 5. **Metrics Analysis**

Check `result.Metrics` to identify performance bottlenecks:

```csharp
foreach (var phase in result.Metrics.Phases)
{
    Console.WriteLine($"{phase.Name}: {phase.Duration.TotalMilliseconds}ms");
}
```

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made?

#### 1. **Adding New Compilation Phases**

If Sharpy adds a new analysis phase (e.g., optimization, dead code elimination), you would:
1. Add a new phase after type checking (around line 148)
2. Create the analyzer class (e.g., `Optimizer`)
3. Check for errors and return early if needed
4. Update `CompilationResult` to include optimization metadata

#### 2. **Enhancing Error Recovery**

Currently, the compiler stops at the first phase with errors. You might extend it to:
- Continue compilation and collect errors from multiple phases
- Provide better error messages with source location context

#### 3. **Supporting Incremental Compilation**

For IDE integration, you might add:
- Caching of intermediate results (AST, symbol table)
- Detection of which phases need to re-run based on file changes

#### 4. **Improving Namespace Generation**

The `ToPascalCase` method is simple. You might enhance it to:
- Respect a custom namespace from a configuration file
- Handle Unicode characters in file names
- Detect and warn about namespace collisions

#### 5. **Extending Compiler Options**

Add new properties to `CompilerOptions`:
```csharp
public bool EnableOptimizations { get; set; }
public string? TargetFramework { get; set; }  // e.g., "net9.0"
```

Then consume them in the `Compile` method or pass them to `CodeGenContext`.

### Guidelines When Modifying This File

1. **Preserve the pipeline order**: Lexer → Parser → Semantic → CodeGen. Don't skip phases.
2. **Maintain fail-fast semantics**: Always return early if a phase produces errors.
3. **Update metrics tracking**: Wrap new phases with `StartPhase` / `EndPhase`.
4. **Don't modify `CompilationResult` lightly**: It's a public API that tests and tools depend on. Adding properties is safe; removing them is a breaking change.
5. **Test with both single-file and project compilation**: Changes to `Compiler` may affect `ProjectCompiler` indirectly.

---

## Cross-References

### Related Partial Classes
This file is **not** a partial class, but it heavily coordinates with components that may be split across files:

- **RoslynEmitter**: See `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` (and its partial class files for specific AST node types)
- **NameResolver**: See `src/Sharpy.Compiler/Semantic/NameResolver.cs`
- **TypeChecker**: See `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- **ProjectCompiler**: See `src/Sharpy.Compiler/Project/ProjectCompiler.cs` for the multi-file compilation logic

### Related Documentation
- For code generation details, refer to `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/RoslynEmitter.md`
- For symbol table and semantic analysis, look for documentation in `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/`
- For the overall architecture, see `.github/copilot-instructions.md`

### Testing
- Unit tests: `src/Sharpy.Compiler.Tests/`
- Integration tests: `src/Sharpy.Compiler.Tests/Integration/CompilerTests.cs`
- File-based tests: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`

---

## Summary

`Compiler.cs` is the **control center** of the Sharpy compiler. It doesn't implement the heavy lifting (that's delegated to specialized classes like `Lexer`, `Parser`, `TypeChecker`, and `RoslynEmitter`), but it ensures they work together correctly. Understanding this file gives you a high-level map of how Sharpy source code becomes executable .NET code.

**Key Takeaways**:
- The compilation pipeline is **sequential and fail-fast**
- Each phase produces artifacts consumed by the next (tokens → AST → annotated AST → C#)
- Single-file compilation is **always treated as an entry point** (`Main` method generated)
- Namespaces are **derived from file names** for single-file scripts
- Errors from any phase **halt compilation immediately** (no recovery)

When debugging compilation issues, start here to identify which phase failed, then dive into the specific component (lexer, parser, etc.) for detailed investigation.
