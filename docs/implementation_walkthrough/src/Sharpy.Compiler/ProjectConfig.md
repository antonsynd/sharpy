# Walkthrough: ProjectConfig.cs

**Source File**: `src/Sharpy.Compiler/ProjectConfig.cs`

---

## Overview

`ProjectConfig.cs` is the **entry point** for understanding how Sharpy projects are configured and loaded. This file defines:

1. **ProjectConfig**: A data model class representing a compiled Sharpy project's configuration
2. **ProjectFileParser**: A parser that reads `.spyproj` XML files and produces `ProjectConfig` instances

**Role in the Compiler Pipeline**: This file sits at the **very beginning** of the compilation process. Before any lexing, parsing, or code generation happens, the compiler needs to know:
- Which `.spy` source files to compile
- What the output should be (executable vs library)
- What dependencies exist
- Where to place build outputs

Think of this as the Sharpy equivalent of `.csproj` files in C# or `package.json` in Node.js.

---

## Class/Type Structure

### 1. ProjectConfig (Data Model)

A configuration object with **init-only properties** (immutable after construction). This is a simple data container with no behavior except two computed properties.

**Key Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `ProjectFilePath` | `string` | Absolute path to the `.spyproj` file |
| `ProjectDirectory` | `string` | Directory containing the project (project root) |
| `RootNamespace` | `string` | Root namespace for generated C# code (required) |
| `OutputType` | `string` | `"library"` or `"exe"` - determines output format |
| `TargetFramework` | `string` | E.g., `"net8.0"` - the .NET target framework |
| `AssemblyName` | `string?` | Output assembly name (defaults to RootNamespace) |
| `EntryPoint` | `string?` | Entry point file for executables (e.g., `"main.spy"`) |
| `SourceFiles` | `List<string>` | Resolved list of `.spy` files to compile |
| `References` | `List<string>` | External .NET assemblies to reference |
| `ModulePaths` | `List<string>` | Search paths for resolving module imports |
| `Configuration` | `string` | Build configuration: `"Debug"` or `"Release"` |
| `UsePrecomputedCodeGenInfo` | `bool` | Whether to compute `CodeGenInfo` during semantic analysis (default: `true`) |

**Computed Properties**:

```csharp
public string OutputPath { get; }
// Returns: bin/{Configuration}/{TargetFramework}
// Example: bin/Debug/net8.0

public virtual string OutputAssemblyPath { get; }
// Returns: bin/{Configuration}/{TargetFramework}/{AssemblyName}.{dll|exe}
// Example: bin/Debug/net8.0/MyProject.dll
```

The `virtual` keyword on `OutputAssemblyPath` suggests this class may be subclassed in tests or other contexts.

**Special Property - UsePrecomputedCodeGenInfo**:
```csharp
public bool UsePrecomputedCodeGenInfo { get; set; } = true;
```

This property controls whether the semantic analyzer pre-computes C# names and other code generation metadata during type checking. **This must be `true`** for code generation to work, as legacy tracking has been removed. This is one of the only mutable properties in `ProjectConfig` because it's a compiler flag that might be toggled for debugging purposes.

---

### 2. ProjectFileParser (Static Parser)

A static utility class that reads and parses `.spyproj` XML files. This is the **workhorse** of project configuration.

**Key Methods**:
- `Load(string projectFilePath, string? configuration)` - Main parsing method
- `FindProjectFile(string directory)` - Locates a `.spyproj` in a directory
- `ResolveGlobPattern(...)` - Resolves glob patterns to actual file paths (private)

---

## Key Functions/Methods

### `ProjectFileParser.Load(string projectFilePath, string? configuration)`

**Purpose**: Parse a `.spyproj` XML file and return a fully populated `ProjectConfig`.

**Flow**:

```
1. Validate file exists
2. Load XML document
3. Validate root <Project> element
4. Parse <PropertyGroup> (required settings)
5. Parse <ItemGroup> elements (source files, references, module paths)
6. Resolve glob patterns for source files
7. Deduplicate source files
8. Validate at least one source file exists
9. Return ProjectConfig instance
```

**Important Implementation Details**:

1. **XML Structure Validation** (lines 113-129):
   ```csharp
   if (root == null || root.Name.LocalName != "Project")
       throw new InvalidDataException("Invalid .spyproj file: missing <Project> root element");
   ```
   The parser is strict about requiring a `<PropertyGroup>` and `<RootNamespace>`. This prevents cryptic errors downstream.

2. **Dual Element Names** (lines 144-167):
   ```csharp
   // Both <SpyFile> and <SourceFile> are supported
   foreach (var spyFile in itemGroup.Elements("SpyFile")) { ... }
   foreach (var sourceFile in itemGroup.Elements("SourceFile")) { ... }
   ```
   This flexibility allows users to use either element name, improving ergonomics.

3. **Glob Pattern Resolution with Exclusions** (lines 146-153):
   ```csharp
   var include = spyFile.Attribute("Include")?.Value;
   var exclude = spyFile.Attribute("Exclude")?.Value;

   if (!string.IsNullOrWhiteSpace(include))
   {
       var resolvedFiles = ResolveGlobPattern(projectDirectory, include, exclude);
       sourceFiles.AddRange(resolvedFiles);
   }
   ```
   Supports MSBuild-style glob patterns like `**/*.spy` with optional `Exclude` patterns like `bin/**;obj/**`.

4. **Path Resolution** (lines 176-179):
   ```csharp
   var referencePath = Path.IsPathRooted(include)
       ? include
       : Path.Combine(projectDirectory, include);
   ```
   Absolute paths are used as-is; relative paths are resolved from the project directory.

5. **Deduplication** (line 198):
   ```csharp
   sourceFiles = sourceFiles.Distinct().ToList();
   ```
   Prevents the same file from being compiled multiple times if matched by multiple patterns.

**Parameters**:
- `projectFilePath` - Path to the `.spyproj` file
- `configuration` - Optional build configuration (`"Debug"` or `"Release"`, defaults to `"Debug"`)

**Returns**: `ProjectConfig` instance

**Throws**:
- `FileNotFoundException` - Project file doesn't exist
- `InvalidDataException` - Malformed XML or missing required elements

---

### `ProjectFileParser.FindProjectFile(string directory)`

**Purpose**: Locate a `.spyproj` file in a directory automatically.

**Behavior**:
- Returns `null` if no `.spyproj` found
- Returns the single `.spyproj` path if exactly one exists
- **Throws** if multiple `.spyproj` files exist (ambiguous)

**Usage Example**:
```csharp
var projectFile = ProjectFileParser.FindProjectFile(Directory.GetCurrentDirectory());
if (projectFile == null)
{
    Console.WriteLine("No project file found. Run 'sharpy init' to create one.");
}
```

This is typically used by CLI commands that operate on "the current project" without requiring explicit file paths.

---

### `ProjectFileParser.ResolveGlobPattern(string baseDirectory, string includePattern, string? excludePattern)`

**Purpose**: Convert glob patterns like `**/*.spy` into concrete file paths.

**Key Features**:

1. **Microsoft.Extensions.FileSystemGlobbing**: Uses the official .NET globbing library, same as MSBuild
   ```csharp
   var matcher = new Matcher();
   matcher.AddInclude(includePattern);
   ```

2. **Multiple Exclude Patterns** (lines 253-260):
   ```csharp
   var excludePatterns = excludePattern.Split(';', StringSplitOptions.RemoveEmptyEntries);
   foreach (var pattern in excludePatterns)
   {
       matcher.AddExclude(pattern.Trim());
   }
   ```
   Supports semicolon-separated exclusions: `Exclude="bin/**;obj/**"`

3. **Full Path Resolution** (lines 266-269):
   ```csharp
   return result.Files
       .Select(f => Path.GetFullPath(Path.Combine(baseDirectory, f.Path)))
       .Where(File.Exists)
       .ToList();
   ```
   Returns absolute paths and filters out non-existent files (defensive programming).

**Returns**: `List<string>` of absolute file paths

---

## Dependencies

### External Dependencies

1. **System.Xml.Linq** (line 1):
   - Used for XML parsing with LINQ
   - Provides `XDocument`, `XElement`, etc.

2. **Microsoft.Extensions.FileSystemGlobbing** (lines 2-3):
   - Official .NET library for glob pattern matching
   - Same library used by MSBuild and .NET SDK project system

### Internal Dependencies

This file has **no dependencies** on other Sharpy compiler components. It's intentionally isolated to:
- Allow project files to be loaded independently
- Enable tooling (e.g., IDE integration) to read projects without loading the full compiler
- Make testing easier

### Downstream Consumers

Files that depend on `ProjectConfig.cs`:
- **Compiler.cs**: Uses `ProjectConfig` to drive the compilation process
- **CLI commands** (Program.cs): Load projects to build, run, or analyze
- **Build orchestration**: Determines what files to compile and where to output results

---

## Patterns and Design Decisions

### 1. **Init-Only Properties Pattern**

```csharp
public string RootNamespace { get; init; } = string.Empty;
```

All properties use `{ get; init; }`, making `ProjectConfig` immutable after construction. This prevents accidental modification during compilation and makes the object thread-safe.

### 2. **Computed Properties for Paths**

Instead of storing `OutputPath` and `OutputAssemblyPath`, they're computed on-the-fly:

```csharp
public string OutputPath
{
    get
    {
        var binPath = Path.Combine(ProjectDirectory, "bin", Configuration, TargetFramework);
        return binPath;
    }
}

public virtual string OutputAssemblyPath
{
    get
    {
        var assemblyName = AssemblyName ?? RootNamespace;
        var extension = OutputType.ToLowerInvariant() == "exe" ? ".exe" : ".dll";
        return Path.Combine(OutputPath, assemblyName + extension);
    }
}
```

**Benefits**: 
- Ensures these paths stay consistent with `Configuration` and `TargetFramework` without manual sync
- The `OutputAssemblyPath` logic automatically determines the correct extension (`.exe` vs `.dll`) based on `OutputType`
- Being `virtual` allows subclasses (like `SingleFileProjectConfig` in the CLI) to override the output location

### 3. **Static Parser Class**

`ProjectFileParser` is a static class (all methods are static). This is a common pattern for:
- Utility/parser code with no instance state
- Discouraging instantiation when there's no object state to maintain

### 4. **MSBuild-Compatible XML Structure**

The `.spyproj` format mirrors MSBuild conventions:

```xml
<Project>
  <PropertyGroup>
    <RootNamespace>MyProject</RootNamespace>
    <OutputType>exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <SpyFile Include="**/*.spy" Exclude="bin/**;obj/**" />
    <Reference Include="path/to/library.dll" />
  </ItemGroup>
</Project>
```

**Rationale**: Familiar to .NET developers and allows reuse of existing tooling/libraries.

### 5. **Fail-Fast Validation**

The parser throws exceptions immediately when encountering invalid data:
- Missing required elements
- No source files
- Multiple project files in a directory

This prevents silent failures and provides clear error messages early.

---

## Debugging Tips

### Common Issues and How to Debug

1. **"No source files found in project"** (line 202):
   - **Cause**: Glob patterns didn't match any files, or all matched files were excluded
   - **Debug**: Print the glob patterns and the directory being searched
   - **Fix**: Check that `Include` patterns are correct and files actually exist

2. **"Multiple .spyproj files found"** (line 236):
   - **Cause**: Directory contains more than one `.spyproj` file
   - **Debug**: Run `ls *.spyproj` in the directory
   - **Fix**: Explicitly specify which project file to use, or remove unused `.spyproj` files

3. **Reference paths not resolving**:
   - **Cause**: Relative paths are resolved from `ProjectDirectory`, not the current working directory
   - **Debug**: Print `ProjectDirectory` and the resolved reference paths
   - **Fix**: Ensure references are relative to the `.spyproj` file location

4. **Source files being compiled twice**:
   - **Cause**: Multiple glob patterns match the same file, and deduplication failed
   - **Debug**: Add breakpoint at line 198 and inspect `sourceFiles` before/after `Distinct()`
   - **Fix**: Adjust glob patterns to be mutually exclusive, or verify `Distinct()` is working

### Debugging Workflow

To inspect what a project file parses to:

```csharp
var config = ProjectFileParser.Load("path/to/project.spyproj");

Console.WriteLine($"Root Namespace: {config.RootNamespace}");
Console.WriteLine($"Output Type: {config.OutputType}");
Console.WriteLine($"Source Files ({config.SourceFiles.Count}):");
foreach (var file in config.SourceFiles)
{
    Console.WriteLine($"  - {file}");
}
Console.WriteLine($"References ({config.References.Count}):");
foreach (var reference in config.References)
{
    Console.WriteLine($"  - {reference}");
}
```

### Logging Recommendations

If adding logging to this file, focus on:
- Which glob patterns are being evaluated
- How many files each pattern matches
- Which files are being excluded
- Final source file count after deduplication

---

## Contribution Guidelines

### When to Modify This File

1. **Adding New Project Properties**:
   - Add the property to `ProjectConfig` with a sensible default
   - Parse it from `<PropertyGroup>` in `Load()`
   - Document its purpose and format

2. **Supporting New Item Types**:
   - Add parsing logic in the `foreach (var itemGroup in root.Elements("ItemGroup"))` loop
   - Follow the pattern of `<Reference>` or `<ModulePath>` elements

3. **Changing Output Path Structure**:
   - Modify the `OutputPath` or `OutputAssemblyPath` computed properties
   - Ensure compatibility with existing projects or provide migration guidance

4. **Improving Error Messages**:
   - Add more specific exceptions with actionable guidance
   - Example: Instead of "Invalid .spyproj file", say "Missing <RootNamespace> in <PropertyGroup>"

### Testing Checklist

When making changes:
1. **Test with missing/malformed XML**: Ensure exceptions are clear
2. **Test glob edge cases**: Empty directories, no matches, only excluded files
3. **Test path resolution**: Relative vs absolute paths, Windows vs Unix paths
4. **Test multiple configurations**: Debug vs Release builds

### Performance Considerations

This file is called **once per compilation**, so performance is not critical. However:
- Glob pattern matching can be slow for large directory trees
- Consider caching if projects are reloaded frequently (e.g., in watch mode)

### Backwards Compatibility

When changing the `.spyproj` format:
- Support old formats with defaults where possible
- Provide clear migration messages for breaking changes
- Consider a version attribute: `<Project Version="2.0">`

---

## Cross-References

### Related Files

- **Compiler.md** (`docs/implementation_walkthrough/src/Sharpy.Compiler/Compiler.md`):
  - See how single-file compilation works
  - `ProjectConfig` is for multi-file projects; single-file uses different path

- **AssemblyCompiler.md** (`docs/implementation_walkthrough/src/Sharpy.Compiler/AssemblyCompiler.md`):
  - Shows how `ProjectConfig` is consumed to compile multiple files into a .NET assembly
  - The `AssemblyCompiler` constructor accepts a `ProjectConfig` parameter

- **Program.cs** (in `src/Sharpy.Cli/`):
  - CLI commands that load and use `ProjectConfig`
  - Contains `SingleFileProjectConfig` subclass that overrides `OutputAssemblyPath`
  - Uses `ProjectFileParser.FindProjectFile()` for auto-discovery

### Example Project Files

Look for sample `.spyproj` files in:
- **`samples/calculator_app/calculator.spyproj`**: A real working example of an executable project
- **`samples/SampleModule/`**: Example of a library module structure
- Integration tests may have test project files in the tests directories

### Design Philosophy

This file embodies the principle of **separation of concerns**:
- **Configuration loading** is isolated from compilation logic
- **Data** (ProjectConfig) is separate from **behavior** (ProjectFileParser)
- **Validation** happens early, at parse time, not during compilation

This makes the codebase more modular and testable.

---

## Example .spyproj File

Here's a complete example to illustrate what this parser handles:

```xml
<Project>
  <PropertyGroup>
    <RootNamespace>MySharpyApp</RootNamespace>
    <OutputType>exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>MyApp</AssemblyName>
    <EntryPoint>main.spy</EntryPoint>
  </PropertyGroup>

  <ItemGroup>
    <!-- Include all .spy files, but exclude build outputs -->
    <SpyFile Include="**/*.spy" Exclude="bin/**;obj/**" />

    <!-- Reference external libraries -->
    <Reference Include="../libs/SomeLibrary.dll" />
    <Reference Include="/absolute/path/to/AnotherLib.dll" />

    <!-- Module search paths for imports -->
    <ModulePath Include="../shared/modules" />
  </ItemGroup>
</Project>
```

**Parsed Result**:
- `RootNamespace`: `"MySharpyApp"`
- `OutputType`: `"exe"`
- `AssemblyName`: `"MyApp"`
- `EntryPoint`: `"main.spy"`
- `SourceFiles`: All `.spy` files in the directory tree except those in `bin/` or `obj/`
- `References`: Two DLL paths (one relative, one absolute)
- `ModulePaths`: One shared module directory
- `OutputAssemblyPath`: `bin/Debug/net8.0/MyApp.exe`

---

## Summary

`ProjectConfig.cs` is the **foundation** of Sharpy's build system. It's:
- **Simple**: Just two classes with clear responsibilities
- **Robust**: Fail-fast validation and clear error messages
- **Familiar**: Uses MSBuild conventions that .NET developers know
- **Isolated**: No dependencies on the rest of the compiler

When working on this file, prioritize clarity and error messages - developers will encounter project configuration issues frequently, and good errors save hours of debugging.
