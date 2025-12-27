# Walkthrough: AssemblyCompiler.cs

**Source File**: `src/Sharpy.Compiler/AssemblyCompiler.cs`

---

## Overview

`AssemblyCompiler.cs` is the **final stage** of the Sharpy compilation pipeline. While the main `Compiler` class handles the journey from Sharpy source code (`.spy`) to C# code, `AssemblyCompiler` takes that generated C# and transforms it into executable .NET assemblies (`.dll` or `.exe` files).

**In the compilation flow:**
```
Sharpy Source (.spy) → Compiler → C# Code → AssemblyCompiler → .NET Assembly (.dll/.exe)
```

This class is responsible for:
- Parsing C# syntax trees using Roslyn
- Resolving .NET framework and Sharpy.Core references
- Compiling C# to IL (Intermediate Language) code
- Emitting debug symbols (`.pdb` files) for Debug builds
- Generating runtime configuration files (`.runtimeconfig.json`, `.deps.json`)
- Tracking compilation metrics (timing, memory usage)

Think of it as the **bridge between Sharpy's custom compiler frontend and the standard .NET compilation backend**.

---

## Class/Type Structure

### Main Class: `AssemblyCompiler`

```csharp
public class AssemblyCompiler
{
    private readonly ICompilerLogger _logger;
    
    public AssemblyCompiler(ICompilerLogger? logger = null)
    public AssemblyCompilationResult CompileToAssembly(...)
    private List<MetadataReference> GetMetadataReferences(...)
    private string FormatDiagnostic(...)
    private void GenerateRuntimeConfig(...)
    private void GenerateDepsFile(...)
}
```

**Key characteristics:**
- **Dependency injection pattern**: Accepts an optional `ICompilerLogger` (uses `NullLogger` if none provided)
- **Single responsibility**: Only handles assembly compilation, not Sharpy→C# translation
- **Private helper methods**: Each step (references, diagnostics, config generation) is isolated

### Result Type: `AssemblyCompilationResult`

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

**C# 9.0+ record-style properties** with `init` accessors (immutable after construction). This ensures results can't be accidentally modified after creation.

---

## Key Methods Deep Dive

### 1. `CompileToAssembly()` - The Main Orchestrator

**Signature:**
```csharp
public AssemblyCompilationResult CompileToAssembly(
    Dictionary<string, string> csharpSources,  // fileName → C# source code
    ProjectConfig projectConfig)               // Build settings
```

**What it does:**
This is the entry point that orchestrates the entire assembly compilation process. It follows a **phase-based approach** where each step is tracked for performance metrics.

**Compilation phases:**

#### Phase 1: C# Parsing (Lines 36-45)
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

- **Uses Roslyn's `CSharpSyntaxTree.ParseText`** to parse each C# file into an AST
- **Why track file paths?** Error messages need to reference the original file location
- **UTF-8 encoding**: Ensures consistent handling of international characters

#### Phase 2: Reference Resolution (Lines 48-50)
```csharp
var references = GetMetadataReferences(projectConfig);
```

Gathers all required assemblies:
- .NET Core libraries (`System.Runtime`, `System.Console`, etc.)
- **Sharpy.Core** (the standard library)
- Project-specific references from `.spyproj`

#### Phase 3: Roslyn Compilation (Lines 58-69)
```csharp
var outputKind = projectConfig.OutputType.ToLowerInvariant() == "exe"
    ? OutputKind.ConsoleApplication
    : OutputKind.DynamicallyLinkedLibrary;

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

**Key decisions:**
- **Output type mapping**: `"exe"` → `ConsoleApplication`, anything else → `DynamicallyLinkedLibrary`
- **Optimization level**: Debug builds include extra info for debugging; Release builds are optimized
- **Platform.AnyCpu**: Compiled assemblies can run on any CPU architecture (.NET handles JIT compilation)

#### Phase 4: IL Emission (Lines 80-96)
```csharp
using var assemblyStream = new FileStream(outputPath, FileMode.Create);

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

**Critical pattern:**
- **`using` statements**: Ensures file streams are properly closed even if exceptions occur
- **PDB files**: Program Database files contain debugging symbols (variable names, line mappings). Only generated in Debug mode.
- **File mode `Create`**: Overwrites existing files

#### Error Handling (Lines 98-117)
```csharp
if (!emitResult.Success)
{
    var errors = emitResult.Diagnostics
        .Where(d => d.Severity == DiagnosticSeverity.Error)
        .Select(d => FormatDiagnostic(d))
        .ToList();
    
    var warnings = emitResult.Diagnostics
        .Where(d => d.Severity == DiagnosticSeverity.Warning)
        .Select(d => FormatDiagnostic(d))
        .ToList();
    
    return new AssemblyCompilationResult
    {
        Success = false,
        Errors = errors,
        Warnings = warnings,
        Metrics = metrics
    };
}
```

**Design pattern: Fail-fast with detailed diagnostics**
- Separates errors from warnings (errors prevent execution, warnings don't)
- Uses LINQ for clean filtering/transformation
- Returns immediately on failure (doesn't try to generate config files)

#### Post-Compilation Tasks (Lines 126-130)
```csharp
GenerateRuntimeConfig(outputPath, projectConfig);
GenerateDepsFile(outputPath, projectConfig);
```

These helper methods create the configuration files .NET needs to run the assembly. See detailed explanations below.

---

### 2. `GetMetadataReferences()` - Dependency Resolution

**Signature:**
```csharp
private List<MetadataReference> GetMetadataReferences(ProjectConfig projectConfig)
```

**What it does:**
Locates and loads all assemblies that the compiled code needs to reference. This is analogous to `-r` flags in command-line compilers.

**Three categories of references:**

#### Core .NET Libraries (Lines 159-170)
```csharp
var coreLibPath = typeof(object).Assembly.Location;
var coreLibDir = Path.GetDirectoryName(coreLibPath);

references.Add(MetadataReference.CreateFromFile(coreLibPath)); // System.Private.CoreLib
references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Runtime.dll")));
references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Console.dll")));
// ...
```

**Why `typeof(object).Assembly.Location`?**
- Dynamically finds where .NET Core libraries are installed (varies by platform/installation)
- `object` is guaranteed to exist in every .NET app

#### Sharpy.Core Reference (Line 173)
```csharp
references.Add(MetadataReference.CreateFromFile(typeof(Sharpy.Core.Exports).Assembly.Location));
```

**Critical dependency:**
- Every Sharpy program needs `Sharpy.Core` for builtins (`print()`, `list`, `dict`, etc.)
- Uses runtime reflection to locate the assembly (works in any environment)

#### Project-Specific References (Lines 175-187)
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

**Error handling strategy:**
- Missing references log warnings but **don't fail compilation** (allows for optional dependencies)
- Files are validated before adding (prevents runtime exceptions)

---

### 3. `FormatDiagnostic()` - Error Message Formatting

**Signature:**
```csharp
private string FormatDiagnostic(Diagnostic diagnostic)
```

**What it does:**
Converts Roslyn's `Diagnostic` objects into human-readable error/warning messages.

**Output format:**
```
Program.cs(42,15): error CS0103: The name 'foo' does not exist in the current context
```

**Code breakdown (Lines 196-208):**
```csharp
var location = diagnostic.Location;
if (location.IsInSource)
{
    var lineSpan = location.GetLineSpan();
    var fileName = Path.GetFileName(lineSpan.Path);
    var line = lineSpan.StartLinePosition.Line + 1;      // Convert 0-based to 1-based
    var column = lineSpan.StartLinePosition.Character + 1;
    return $"{fileName}({line},{column}): {diagnostic.Severity.ToString().ToLower()} {diagnostic.Id}: {diagnostic.GetMessage()}";
}

return $"{diagnostic.Severity.ToString().ToLower()} {diagnostic.Id}: {diagnostic.GetMessage()}";
```

**Key points:**
- **1-based line/column numbers**: Humans count from 1, computers from 0
- **File name only**: Displays just filename (not full path) for readability
- **Fallback for non-source errors**: Some diagnostics aren't tied to source code locations

---

### 4. `GenerateRuntimeConfig()` - .NET Runtime Configuration

**Signature:**
```csharp
private void GenerateRuntimeConfig(string assemblyPath, ProjectConfig projectConfig)
```

**What it does:**
Creates a `.runtimeconfig.json` file that tells the .NET runtime which framework version to use.

**Generated JSON structure (Lines 224-237):**
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

**Why this matters:**
- **Without this file**, .NET won't know which runtime to load
- **Version matching**: Uses the current runtime's version dynamically
- **MetadataUpdater disabled**: Prevents hot-reload issues (Sharpy doesn't support it yet)

**Error handling (Lines 242-245):**
```csharp
catch (Exception ex)
{
    _logger.LogWarning($"Failed to generate runtime config: {ex.Message}", 0, 0);
}
```

**Design decision:** Failures here are warnings, not errors. The assembly can still run if the runtime is configured elsewhere.

---

### 5. `GenerateDepsFile()` - Dependency Manifest

**Signature:**
```csharp
private void GenerateDepsFile(string assemblyPath, ProjectConfig projectConfig)
```

**What it does:**
Creates a `.deps.json` file that lists all dependencies and their versions. This helps .NET locate assemblies at runtime.

**Generated structure (Lines 269-309):**
```json
{
  "runtimeTarget": {
    "name": ".NETCoreApp,Version=v9.0",
    "signature": ""
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
      "type": "project",
      "serviceable": false,
      "sha512": ""
    },
    "Sharpy.Core/1.0.0": {
      "type": "reference",
      "serviceable": false,
      "sha512": ""
    }
  }
}
```

**Key information tracked:**
- **Runtime target**: .NET version
- **Dependencies graph**: What assemblies this app needs
- **Library types**: "project" vs "reference"

**Why empty SHA512?** (Line 300, 305)
- Full implementation would compute file hashes for integrity checking
- Currently simplified (not critical for development builds)

---

## Dependencies

### External NuGet Packages
- **Microsoft.CodeAnalysis.CSharp** (Roslyn): C# compiler as a library
- **Microsoft.CodeAnalysis.Emit**: IL emission functionality

### Internal Sharpy Dependencies
- **`Sharpy.Compiler.Logging.ICompilerLogger`**: Logging abstraction
- **`Sharpy.Compiler.Diagnostics.CompilationMetrics`**: Performance tracking
- **`Sharpy.Compiler.ProjectConfig`**: Build configuration (from `.spyproj` files)
- **`Sharpy.Core.Exports`**: Standard library reference

### .NET BCL Dependencies
- **System.IO**: File operations
- **System.Linq**: Collection queries
- **System.Text.Encoding**: Character encoding

---

## Patterns and Design Decisions

### 1. **Builder Pattern via Roslyn**
```csharp
var compilation = CSharpCompilation.Create(...)
    .WithOptimizationLevel(...)
    .WithPlatform(...);
```

Roslyn uses fluent APIs for configuration. Each `With*` method returns a new compilation object (immutability).

### 2. **Strategy Pattern for Configuration**
```csharp
if (projectConfig.Configuration == "Debug")
{
    // Debug-specific logic
}
else
{
    // Release-specific logic
}
```

Different behaviors based on build configuration without polymorphism overhead.

### 3. **Phase-Based Execution with Metrics**
```csharp
metrics.StartPhase("C# Parsing");
// Do work...
metrics.EndPhase();
```

**Benefits:**
- Easy to identify performance bottlenecks
- Provides user feedback during long compilations
- Enables benchmarking and optimization

### 4. **Separation of Concerns**
- **Parsing**: Handled by Roslyn
- **Reference resolution**: Isolated in `GetMetadataReferences()`
- **Error formatting**: Isolated in `FormatDiagnostic()`
- **Config generation**: Separate methods for each file type

Each method has a single, clear responsibility.

### 5. **Fail-Safe Error Handling**
```csharp
try
{
    // Compilation logic
}
catch (Exception ex)
{
    _logger.LogError($"Assembly compilation failed: {ex.Message}", 0, 0);
    return new AssemblyCompilationResult
    {
        Success = false,
        Errors = new List<string> { $"Assembly compilation failed: {ex.Message}" }
    };
}
```

**Never throws exceptions to caller** - always returns a result object. This makes error handling predictable.

### 6. **Resource Management**
```csharp
using var assemblyStream = new FileStream(outputPath, FileMode.Create);
using var pdbStream = new FileStream(pdbPath, FileMode.Create);
```

C# 8.0+ `using` declarations ensure streams are disposed at end of scope.

---

## Debugging Tips

### 1. **Inspecting Generated C# Code**
If assembly compilation fails, look at the C# sources passed to `CompileToAssembly()`:
```csharp
// Add breakpoint here and inspect csharpSources dictionary
foreach (var (filePath, sourceCode) in csharpSources)
{
    Console.WriteLine($"=== {filePath} ===");
    Console.WriteLine(sourceCode);
}
```

### 2. **Examining Roslyn Diagnostics**
Add this after `compilation.Emit()`:
```csharp
foreach (var diagnostic in emitResult.Diagnostics)
{
    Console.WriteLine($"{diagnostic.Severity}: {diagnostic.GetMessage()}");
    if (diagnostic.Location.IsInSource)
    {
        Console.WriteLine($"  Location: {diagnostic.Location.GetLineSpan()}");
    }
}
```

### 3. **Verifying References**
Check what assemblies are being referenced:
```csharp
var references = GetMetadataReferences(projectConfig);
Console.WriteLine($"Total references: {references.Count}");
foreach (var reference in references)
{
    Console.WriteLine($"  {reference.Display}");
}
```

### 4. **Testing with Minimal C# Code**
Create a minimal test case:
```csharp
var minimalCSharp = new Dictionary<string, string>
{
    ["Test.cs"] = "class Program { static void Main() { System.Console.WriteLine(\"Hello\"); } }"
};
var result = assemblyCompiler.CompileToAssembly(minimalCSharp, projectConfig);
```

### 5. **Checking Output Files**
Verify generated files exist:
```bash
ls -la bin/Debug/net9.0/
# Should see: MyApp.dll, MyApp.pdb, MyApp.runtimeconfig.json, MyApp.deps.json
```

### 6. **Using dotnet CLI to Test**
Manually run the compiled assembly:
```bash
dotnet bin/Debug/net9.0/MyApp.dll
```

If it fails, .NET will give detailed error messages about missing dependencies or runtime issues.

---

## Contribution Guidelines

### Areas for Enhancement

#### 1. **Better Reference Resolution**
Current implementation hardcodes common .NET assemblies. Could be improved:
```csharp
// Instead of manually listing System.Runtime, System.Console, etc.
// Use NuGet package resolution or MSBuild SDK references
```

**Task:** Implement automatic discovery of required references based on code analysis.

#### 2. **Support for Resource Files**
Currently doesn't handle:
- Embedded resources
- App settings files
- Static assets

**Task:** Add `GenerateResourceManifest()` method similar to `GenerateDepsFile()`.

#### 3. **Multi-Targeting**
Only supports single target framework. Could support:
```xml
<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

**Task:** Modify `CompileToAssembly()` to loop over multiple target frameworks.

#### 4. **Incremental Compilation**
Always does full rebuilds. Could cache:
- Unchanged syntax trees
- Metadata references
- Previous compilation results

**Task:** Add fingerprinting (file hashes) and skip unchanged files.

#### 5. **Source Generators Support**
Roslyn supports source generators (compile-time code generation). Sharpy could leverage these:
```csharp
var compilation = CSharpCompilation.Create(...)
    .AddSyntaxTrees(...) // Add generated code
```

**Task:** Research Roslyn source generators integration.

#### 6. **Better Diagnostics**
Current error formatting is basic. Could add:
- Source code snippets showing error location
- Color-coded output
- Suggested fixes

**Task:** Enhance `FormatDiagnostic()` with rich formatting.

### Testing Recommendations

#### Unit Tests to Add
```csharp
[Fact]
public void CompileToAssembly_WithValidCSharp_Succeeds()
{
    var csharp = new Dictionary<string, string>
    {
        ["Program.cs"] = "class Program { static void Main() {} }"
    };
    var result = compiler.CompileToAssembly(csharp, GetTestConfig());
    Assert.True(result.Success);
}

[Fact]
public void CompileToAssembly_WithInvalidCSharp_ReturnsErrors()
{
    var csharp = new Dictionary<string, string>
    {
        ["Program.cs"] = "class Program { invalid syntax }"
    };
    var result = compiler.CompileToAssembly(csharp, GetTestConfig());
    Assert.False(result.Success);
    Assert.NotEmpty(result.Errors);
}

[Fact]
public void CompileToAssembly_DebugMode_GeneratesPdbFile()
{
    var config = GetTestConfig();
    config.Configuration = "Debug";
    var result = compiler.CompileToAssembly(GetValidCSharp(), config);
    Assert.True(File.Exists(Path.ChangeExtension(result.OutputAssemblyPath, ".pdb")));
}
```

#### Integration Tests
- Compile sample Sharpy projects end-to-end
- Verify generated assemblies execute correctly
- Test with various `.spyproj` configurations

### Code Style Notes

Following patterns from this file:
- **Null-coalescing for optional dependencies**: `logger ?? NullLogger.Instance`
- **LINQ for transformations**: `emitResult.Diagnostics.Where(...).Select(...)`
- **Early returns on failure**: Don't nest success paths deeply
- **`using` for resources**: Always dispose file streams
- **Descriptive variable names**: `outputKind`, `assemblyStream`, not `ok`, `fs`

---

## Summary

`AssemblyCompiler.cs` is the **final mile** in Sharpy compilation. It:

1. **Leverages Roslyn** to handle C# compilation (no need to reinvent the wheel)
2. **Manages dependencies** by gathering .NET and Sharpy.Core references
3. **Generates runtime artifacts** (.pdb, .runtimeconfig.json, .deps.json)
4. **Tracks performance** via `CompilationMetrics`
5. **Reports errors clearly** with formatted diagnostics

**Key takeaway for newcomers:** This class is a thin orchestration layer over Roslyn. Understanding Roslyn's API (`CSharpCompilation`, `MetadataReference`, `SyntaxTree`) is crucial for maintaining this code.

**Next steps:**
- Read `Compiler.cs` to see how Sharpy→C# translation works
- Study Roslyn documentation: https://github.com/dotnet/roslyn/wiki
- Experiment with modifying `GetMetadataReferences()` to add new dependencies
