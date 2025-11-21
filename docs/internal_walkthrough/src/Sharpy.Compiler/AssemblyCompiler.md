# Walkthrough: AssemblyCompiler.cs

**Source File**: `src/Sharpy.Compiler/AssemblyCompiler.cs`

---

## 1. Overview

`AssemblyCompiler.cs` is the **final stage** of the Sharpy compilation pipeline. While other components handle transforming Sharpy code into C# code, this class is responsible for taking that generated C# code and compiling it into executable .NET assemblies (`.dll` or `.exe` files).

**Role in the compilation pipeline:**
```
Sharpy Source (.spy)
    ↓
Lexer → Parser → Semantic Analyzer → Code Generator
    ↓
C# Source Code (in-memory strings)
    ↓
[AssemblyCompiler] ← YOU ARE HERE
    ↓
.NET Assembly (.dll/.exe) + Runtime Config Files
```

**Key Responsibilities:**
- Parse generated C# code using Roslyn's C# compiler
- Resolve .NET metadata references (System.Runtime, Sharpy.Core, etc.)
- Compile C# syntax trees to IL (Intermediate Language)
- Emit assembly files with proper configuration (Debug vs Release)
- Generate supporting files (`.pdb` for debugging, `.runtimeconfig.json`, `.deps.json`)
- Report compilation errors and warnings from the C# compiler

**Why this exists:** Sharpy compiles to C# as an intermediate representation, then leverages Roslyn (the C# compiler) to do the heavy lifting of IL generation and optimization. This approach gives us .NET interop "for free" and access to mature compiler infrastructure.

---

## 2. Class/Type Structure

### `AssemblyCompiler` Class

The main workhorse class that orchestrates the C#-to-assembly compilation process.

**Key Fields:**
```csharp
private readonly ICompilerLogger _logger;
```
- Logger for tracking compilation progress and debugging
- Defaults to `NullLogger.Instance` if none provided (no-op logger)

**Constructor:**
```csharp
public AssemblyCompiler(ICompilerLogger? logger = null)
```
- Simple constructor that accepts an optional logger
- Uses null-coalescing to provide a default logger

### `AssemblyCompilationResult` Class

A data transfer object (DTO) that encapsulates the outcome of compilation.

```csharp
public class AssemblyCompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string? OutputAssemblyPath { get; init; }
    public CompilationMetrics? Metrics { get; init; }
}
```

**Properties:**
- `Success`: Boolean flag indicating whether compilation succeeded
- `Errors`: List of error messages from the C# compiler
- `Warnings`: List of warning messages from the C# compiler
- `OutputAssemblyPath`: Path to the generated assembly (only set on success)
- `Metrics`: Performance metrics tracking time spent in each compilation phase

**Design note:** Uses `init`-only properties for immutability after construction.

---

## 3. Key Functions/Methods

### 3.1 `CompileToAssembly()` - The Main Entry Point

```csharp
public AssemblyCompilationResult CompileToAssembly(
    Dictionary<string, string> csharpSources,
    ProjectConfig projectConfig)
```

**Purpose:** Takes a collection of C# source files (as strings) and compiles them into a .NET assembly.

**Parameters:**
- `csharpSources`: Dictionary mapping file paths to C# source code
  - Key: File path (e.g., `"MyApp/Program.cs"`)
  - Value: The actual C# source code as a string
- `projectConfig`: Configuration object containing:
  - Assembly name, output path, target framework
  - Build configuration (Debug/Release)
  - References to other assemblies

**Return Value:** `AssemblyCompilationResult` with success/failure status, errors, warnings, and metrics.

**Implementation Flow:**

#### Phase 1: C# Parsing (lines 36-45)
```csharp
var syntaxTrees = new List<SyntaxTree>();
foreach (var (filePath, sourceCode) in csharpSources)
{
    var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode,
        path: filePath,
        encoding: System.Text.Encoding.UTF8);
    syntaxTrees.Add(syntaxTree);
}
```
- Parses each C# source string into a Roslyn `SyntaxTree`
- Syntax trees are the parsed representation of C# code
- `path` parameter associates the syntax tree with a file (important for error reporting)

#### Phase 2: Reference Resolution (lines 48-50)
```csharp
var references = GetMetadataReferences(projectConfig);
```
- Gathers all .NET assemblies that the compiled code depends on
- Includes core .NET libraries, Sharpy.Core, and project-specific references
- See `GetMetadataReferences()` below for details

#### Phase 3: Determine Output Type (lines 53-55)
```csharp
var outputKind = projectConfig.OutputType.ToLowerInvariant() == "exe"
    ? OutputKind.ConsoleApplication
    : OutputKind.DynamicallyLinkedLibrary;
```
- Decides whether to build an executable (`.exe`) or a library (`.dll`)
- Maps Sharpy project config to Roslyn's `OutputKind` enum

#### Phase 4: Create Roslyn Compilation (lines 58-69)
```csharp
var compilation = CSharpCompilation.Create(
    assemblyName,
    syntaxTrees,
    references,
    new CSharpCompilationOptions(outputKind)
        .WithOptimizationLevel(projectConfig.Configuration == "Release"
            ? OptimizationLevel.Release
            : OptimizationLevel.Debug)
        .WithPlatform(Platform.AnyCpu));
```
- Creates a Roslyn `CSharpCompilation` object
- **Compilation options:**
  - **Optimization level:** Release builds use optimizations, Debug builds don't
  - **Platform:** `AnyCpu` means the assembly can run on any CPU architecture
- This is where all syntax trees and references come together

#### Phase 5: IL Emission (lines 79-96)
```csharp
using var assemblyStream = new FileStream(outputPath, FileMode.Create);

EmitResult emitResult;
if (projectConfig.Configuration == "Debug")
{
    var pdbPath = Path.ChangeExtension(outputPath, ".pdb");
    using var pdbStream = new FileStream(pdbPath, FileMode.Create);
    emitResult = compilation.Emit(assemblyStream, pdbStream);
}
else
{
    emitResult = compilation.Emit(assemblyStream);
}
```
- **Debug builds:** Emit both assembly and PDB (Program Database) file
  - PDB contains debugging symbols (line numbers, variable names, etc.)
  - Essential for debugging with breakpoints
- **Release builds:** Emit only the assembly (no debug symbols)
- Uses `using` statements for automatic disposal of file streams

#### Phase 6: Handle Compilation Results (lines 98-138)
```csharp
if (!emitResult.Success)
{
    var errors = emitResult.Diagnostics
        .Where(d => d.Severity == DiagnosticSeverity.Error)
        .Select(d => FormatDiagnostic(d))
        .ToList();
    // ... return failure result
}
```
- If compilation failed, extract and format all errors
- If successful, generate runtime configuration files
- Return a result object with all relevant information

**Error Handling:**
- Catches exceptions and returns them as compilation errors
- Ensures metrics are always included in the result
- Gracefully handles failures in auxiliary file generation

---

### 3.2 `GetMetadataReferences()` - Resolving Dependencies

```csharp
private List<MetadataReference> GetMetadataReferences(ProjectConfig projectConfig)
```

**Purpose:** Gather all .NET assemblies that the compiled code needs to reference.

**Implementation Strategy:**

#### Core .NET References (lines 160-170)
```csharp
var coreLibPath = typeof(object).Assembly.Location;
var coreLibDir = Path.GetDirectoryName(coreLibPath);

references.Add(MetadataReference.CreateFromFile(coreLibPath)); // System.Private.CoreLib
references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Runtime.dll")));
references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Console.dll")));
// ... more references
```
- **Clever trick:** Uses `typeof(object).Assembly.Location` to find where .NET core libraries are installed
- Adds essential .NET assemblies:
  - `System.Private.CoreLib`: Core types (`object`, `int`, `string`, etc.)
  - `System.Runtime`: Runtime services
  - `System.Console`: Console I/O
  - `System.Collections`: Collection types
  - `System.Linq`: LINQ extension methods

#### Sharpy.Core Reference (line 173)
```csharp
references.Add(MetadataReference.CreateFromFile(typeof(Sharpy.Core.Exports).Assembly.Location));
```
- Adds Sharpy's standard library (Pythonic collections, builtin functions)
- Uses the same trick to find the assembly location at runtime

#### Project-Specific References (lines 176-187)
```csharp
foreach (var referencePath in projectConfig.References)
{
    if (File.Exists(referencePath))
    {
        references.Add(MetadataReference.CreateFromFile(referencePath));
        _logger.LogDebug($"Added reference: {referencePath}");
    }
    else
    {
        _logger.LogWarning($"Reference not found: {referencePath}", 0, 0);
    }
}
```
- Adds user-specified assembly references from the project config
- Validates that each reference file exists
- Logs warnings for missing references (non-fatal, continues compilation)

**Why this matters:** Without proper references, the C# compiler can't resolve types, and compilation fails. This method ensures all dependencies are available.

---

### 3.3 `FormatDiagnostic()` - Error Message Formatting

```csharp
private string FormatDiagnostic(Diagnostic diagnostic)
```

**Purpose:** Convert Roslyn diagnostic objects into human-readable error/warning messages.

**Output Format:**
```
MyFile.cs(42,15): error CS1002: ; expected
```

**Implementation:**
```csharp
if (location.IsInSource)
{
    var lineSpan = location.GetLineSpan();
    var fileName = Path.GetFileName(lineSpan.Path);
    var line = lineSpan.StartLinePosition.Line + 1;
    var column = lineSpan.StartLinePosition.Character + 1;
    return $"{fileName}({line},{column}): {diagnostic.Severity.ToString().ToLower()} {diagnostic.Id}: {diagnostic.GetMessage()}";
}
```

**Key Details:**
- Adjusts line/column numbers (Roslyn uses 0-based, users expect 1-based)
- Includes file name, line, column, severity, diagnostic ID, and message
- Falls back to simpler format if diagnostic isn't tied to source code
- Format matches Visual Studio/MSBuild conventions for IDE integration

---

### 3.4 `GenerateRuntimeConfig()` - Runtime Configuration File

```csharp
private void GenerateRuntimeConfig(string assemblyPath, ProjectConfig projectConfig)
```

**Purpose:** Create a `.runtimeconfig.json` file that tells the .NET runtime how to execute the assembly.

**Generated File Example:**
```json
{
  "runtimeOptions": {
    "tfm": "net9.0",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "9.0.0"
    },
    "configProperties": {
      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false
    }
  }
}
```

**Implementation Highlights:**
```csharp
var runtimeVersion = Environment.Version;
var frameworkVersion = $"{runtimeVersion.Major}.{runtimeVersion.Minor}.{runtimeVersion.Build}";
```
- Dynamically detects the current .NET runtime version
- Uses C# 11 raw string literals (`$$"""..."""`) for clean JSON generation
- File is placed alongside the assembly with `.runtimeconfig.json` extension

**Why this file is needed:** Without it, `dotnet run` wouldn't know which .NET runtime version to use when executing the assembly.

**Error Handling:** If generation fails, logs a warning but doesn't fail the entire compilation.

---

### 3.5 `GenerateDepsFile()` - Dependencies Manifest

```csharp
private void GenerateDepsFile(string assemblyPath, ProjectConfig projectConfig)
```

**Purpose:** Create a `.deps.json` file that lists all dependencies of the assembly.

**Generated File Example (simplified):**
```json
{
  "runtimeTarget": {
    "name": ".NETCoreApp,Version=v9.0"
  },
  "targets": {
    ".NETCoreApp,Version=v9.0": {
      "MyApp/1.0.0": {
        "dependencies": {
          "Sharpy.Core": "1.0.0"
        },
        "runtime": {
          "MyApp.dll": {}
        }
      },
      "Sharpy.Core/1.0.0": {
        "runtime": {
          "Sharpy.Core.dll": {
            "assemblyVersion": "1.0.0",
            "fileVersion": "1.0.0"
          }
        }
      }
    }
  },
  "libraries": {
    "MyApp/1.0.0": {
      "type": "project"
    },
    "Sharpy.Core/1.0.0": {
      "type": "reference"
    }
  }
}
```

**Implementation Details:**
```csharp
var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;
var sharpyCoreLocation = sharpyCoreAssembly.Location;
var sharpyCoreName = sharpyCoreAssembly.GetName();
var sharpyCoreVersion = sharpyCoreName.Version?.ToString() ?? "1.0.0";
```
- Reflects on Sharpy.Core assembly to get version and location info
- Builds a complete dependency graph in JSON format
- Uses raw string literals for clean, readable JSON generation

**Why this file is needed:** The .NET runtime uses it to locate and load dependencies at runtime. Essential for multi-assembly applications.

---

## 4. Dependencies

### External Dependencies (NuGet Packages)
- **Microsoft.CodeAnalysis.CSharp**: Roslyn C# compiler APIs
  - `CSharpSyntaxTree`: Parsing C# code
  - `CSharpCompilation`: Compiling syntax trees to IL
  - `MetadataReference`: Assembly references
  - `EmitResult`: Compilation results

### Internal Dependencies (Sharpy Codebase)
- **`Sharpy.Compiler.Logging.ICompilerLogger`**: Logging abstraction
- **`Sharpy.Compiler.Diagnostics.CompilationMetrics`**: Performance tracking
- **`Sharpy.Compiler.ProjectConfig`**: Project configuration (defined elsewhere in the compiler)
- **`Sharpy.Core.Exports`**: Used to locate Sharpy.Core assembly

### .NET Framework Dependencies
- `System.IO`: File operations
- `System.Linq`: LINQ queries for filtering diagnostics
- `System.Text.Encoding`: UTF-8 encoding for source files

---

## 5. Patterns and Design Decisions

### 5.1 Separation of Concerns
- **AssemblyCompiler** doesn't know about Sharpy syntax or semantics
- It only deals with C# code that's already been generated
- This clean separation allows the C# code generator to be swapped out or modified independently

### 5.2 Roslyn as a Compilation Backend
**Why use Roslyn instead of writing our own IL emitter?**
- **Mature and battle-tested:** Roslyn is the production C# compiler
- **Optimizations:** Get all C# compiler optimizations for free
- **Interop:** Seamless integration with existing .NET code
- **Maintenance:** Microsoft maintains it; we don't have to

### 5.3 Metrics Tracking Pattern
```csharp
metrics.StartPhase("C# Parsing");
// ... do work ...
metrics.EndPhase();
```
- Consistent pattern for measuring performance of each compilation phase
- Helps identify bottlenecks in the compilation pipeline
- Metrics are always included in results, even on failure

### 5.4 Graceful Degradation
- If PDB generation fails → log warning, continue
- If runtime config generation fails → log warning, continue
- If deps file generation fails → log warning, continue
- **Philosophy:** Auxiliary files are helpful but not essential; the assembly itself is what matters

### 5.5 Immutable Results
- `AssemblyCompilationResult` uses `init` properties
- Once created, the result can't be modified
- Prevents accidental mutations and makes the API safer

### 5.6 Dependency Injection
- Logger is injected via constructor
- Makes the class testable (can inject a mock logger)
- Provides a default for simple use cases (`NullLogger.Instance`)

---

## 6. Debugging Tips

### 6.1 Viewing Generated C# Code
If compilation fails with mysterious errors, inspect the generated C# code:
```csharp
foreach (var (path, code) in csharpSources)
{
    Console.WriteLine($"=== {path} ===");
    Console.WriteLine(code);
}
```

### 6.2 Understanding Roslyn Diagnostics
Roslyn diagnostics have rich information:
```csharp
foreach (var diagnostic in emitResult.Diagnostics)
{
    Console.WriteLine($"Severity: {diagnostic.Severity}");
    Console.WriteLine($"ID: {diagnostic.Id}");
    Console.WriteLine($"Message: {diagnostic.GetMessage()}");
    Console.WriteLine($"Location: {diagnostic.Location}");
    if (diagnostic.Location.IsInSource)
    {
        Console.WriteLine($"Source: {diagnostic.Location.SourceTree?.GetText().ToString()}");
    }
}
```

### 6.3 Checking References
If you get "type or namespace not found" errors:
```csharp
foreach (var reference in references)
{
    Console.WriteLine($"Reference: {reference.Display}");
}
```

### 6.4 Metrics for Performance Issues
```csharp
var result = compiler.CompileToAssembly(sources, config);
if (result.Metrics != null)
{
    foreach (var phase in result.Metrics.Phases)
    {
        Console.WriteLine($"{phase.Name}: {phase.Duration}ms");
    }
}
```

### 6.5 Common Errors and Solutions

**Error: "The type or namespace name 'Sharpy' could not be found"**
- **Cause:** Sharpy.Core reference missing
- **Solution:** Ensure Sharpy.Core.dll is built and in the expected location

**Error: "Assembly file not found: System.Runtime.dll"**
- **Cause:** .NET SDK path resolution failed
- **Solution:** Check that .NET is properly installed; `typeof(object).Assembly.Location` should return a valid path

**Error: "PDB generation failed"**
- **Cause:** File permissions or disk space issues
- **Solution:** Check write permissions on output directory; falls back gracefully

---

## 7. Contribution Guidelines

### 7.1 Areas for Enhancement

**Performance Optimizations:**
- Cache parsed syntax trees for incremental compilation
- Parallelize syntax tree parsing for large projects
- Reuse metadata references across multiple compilations

**Better Error Messages:**
- Map C# errors back to original Sharpy source locations
- Provide suggestions for common mistakes
- Highlight the exact Sharpy code that caused the C# error

**Additional Configuration Options:**
- Support for code signing
- Custom assembly attributes
- NuGet package generation
- Multi-targeting (e.g., net9.0 and net10.0 simultaneously)

**Testing Improvements:**
- Add unit tests for each method
- Integration tests for various project configurations
- Test error handling paths

### 7.2 Coding Conventions

**When modifying this file:**
- Maintain the phase-based metrics tracking pattern
- Use `_logger.LogInfo()` for major milestones
- Use `_logger.LogDebug()` for detailed diagnostics
- Use `_logger.LogWarning()` for non-fatal issues
- Always return an `AssemblyCompilationResult`, even on exceptions

**XML Documentation:**
- Add `/// <summary>` comments for all public methods
- Document parameters with `/// <param>`
- Document return values with `/// <returns>`

**Error Handling:**
- Catch specific exceptions when possible
- Include context in error messages (file names, line numbers)
- Log exceptions before returning failure results

### 7.3 Testing Your Changes

**Manual testing:**
```csharp
var sources = new Dictionary<string, string>
{
    ["Program.cs"] = "class Program { static void Main() { } }"
};

var config = new ProjectConfig
{
    RootNamespace = "TestApp",
    OutputType = "exe",
    Configuration = "Debug",
    TargetFramework = "net9.0",
    OutputAssemblyPath = "TestApp.dll"
};

var compiler = new AssemblyCompiler();
var result = compiler.CompileToAssembly(sources, config);

if (result.Success)
{
    Console.WriteLine($"Success! Assembly: {result.OutputAssemblyPath}");
}
else
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Error: {error}");
    }
}
```

**Integration testing:**
- Compile sample Sharpy projects
- Verify generated assemblies execute correctly
- Check that debug builds have PDB files
- Verify runtime config and deps files are generated

### 7.4 Common Mistakes to Avoid

❌ **DON'T** modify diagnostic formatting without updating tests
❌ **DON'T** change reference resolution logic without testing on different .NET versions
❌ **DON'T** throw exceptions directly; wrap them in `AssemblyCompilationResult`
❌ **DON'T** forget to update metrics when adding new compilation phases

✅ **DO** maintain backward compatibility with existing `ProjectConfig` structures
✅ **DO** add logging for new functionality
✅ **DO** test on both Debug and Release configurations
✅ **DO** validate that generated files are well-formed (especially JSON)

---

## Summary

`AssemblyCompiler.cs` is the bridge between Sharpy's code generation and executable .NET assemblies. It:

1. Takes generated C# code (strings)
2. Parses it using Roslyn
3. Resolves all necessary .NET references
4. Compiles to IL and emits assembly files
5. Generates supporting runtime configuration files
6. Reports errors and warnings back to the user

**Key Insight:** This class is purely focused on the C# → assembly transformation. It doesn't need to understand Sharpy syntax, types, or semantics—that's all handled by earlier pipeline stages.

**When to modify this file:**
- Adding new compilation options (e.g., deterministic builds)
- Improving error messages or diagnostics
- Optimizing compilation performance
- Supporting new .NET features or runtime versions
- Adding new output formats or configurations

**When NOT to modify this file:**
- Changing how Sharpy code is parsed (that's in `Parser/`)
- Modifying type checking (that's in `Semantic/`)
- Changing C# code generation (that's in `CodeGen/`)
- Adding new Sharpy language features (that's in earlier stages)

This class should remain focused on its single responsibility: compiling C# to assemblies.
