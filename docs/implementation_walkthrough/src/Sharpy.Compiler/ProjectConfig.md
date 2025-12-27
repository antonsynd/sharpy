# Walkthrough: ProjectConfig.cs

**Source File**: `src/Sharpy.Compiler/ProjectConfig.cs`

---

## 1. Overview

The `ProjectConfig.cs` file is the heart of Sharpy's project system. It provides two essential components:

1. **`ProjectConfig` class**: A data model that represents all the configuration for a Sharpy project (`.spyproj` files)
2. **`ProjectFileParser` class**: A parser that reads XML-based `.spyproj` files and converts them into `ProjectConfig` objects

Think of this file as the bridge between the MSBuild-style project file format and the compiler's internal representation. When you run `sharpyc project myapp.spyproj`, this code is responsible for:
- Reading the XML project file
- Resolving glob patterns like `src/**/*.spy` into actual file paths
- Computing output paths based on configuration (Debug/Release)
- Validating that the project has required elements

**Role in the overall architecture**: This file sits at the entry point of multi-file compilation. The CLI (`Sharpy.Cli`) uses `ProjectFileParser.Load()` to get a `ProjectConfig`, which is then passed to `AssemblyCompiler` to orchestrate the full compilation pipeline.

---

## 2. Class/Type Structure

### 2.1 `ProjectConfig` Class

A plain configuration data class (no complex logic) using C# 9.0 init-only properties. It's effectively a **Data Transfer Object (DTO)** pattern.

```csharp
public class ProjectConfig
{
    // Properties with init-only setters
    public string ProjectFilePath { get; init; } = string.Empty;
    public string RootNamespace { get; init; } = string.Empty;
    // ... more properties
    
    // Computed properties with getters only
    public string OutputPath { get; }
    public virtual string OutputAssemblyPath { get; }
}
```

**Key design decision**: All properties use `init` accessors, making instances **immutable after construction**. This prevents accidental modification during compilation and makes the code easier to reason about.

### 2.2 `ProjectFileParser` Class

A static utility class following the **Factory pattern**. It has no instance state—all methods are static.

```csharp
public class ProjectFileParser
{
    public static ProjectConfig Load(string projectFilePath, string? configuration = null)
    public static string? FindProjectFile(string directory)
    private static List<string> ResolveGlobPattern(string baseDirectory, string pattern)
}
```

**Why static?** The parser has no state to maintain between operations. Each `Load()` call is independent, making it thread-safe and simple to use.

---

## 3. Key Functions/Methods

### 3.1 `ProjectConfig.OutputPath` (Computed Property)

```csharp
public string OutputPath
{
    get
    {
        var binPath = Path.Combine(ProjectDirectory, "bin", Configuration, TargetFramework);
        return binPath;
    }
}
```

**What it does**: Computes the output directory path following .NET conventions.

**Example output**: 
- Debug build: `<project_dir>/bin/Debug/net8.0/`
- Release build: `<project_dir>/bin/Release/net8.0/`

**Key insight**: This mimics MSBuild's directory structure, making Sharpy projects feel familiar to .NET developers. The path is computed on-demand rather than stored, ensuring it's always consistent with `Configuration` and `TargetFramework`.

### 3.2 `ProjectConfig.OutputAssemblyPath` (Computed Property)

```csharp
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

**What it does**: Determines the full path to the final compiled assembly.

**Parameters/Logic**:
- Falls back to `RootNamespace` if `AssemblyName` isn't specified
- Chooses `.exe` for console apps, `.dll` for libraries
- Builds on top of `OutputPath`

**Why virtual?** Allows test projects or specialized compilers to override the output path logic without modifying the base class.

**Example**: For project with `RootNamespace="MyApp"` and `OutputType="exe"`:
```
/path/to/project/bin/Debug/net8.0/MyApp.exe
```

### 3.3 `ProjectFileParser.Load()` (Main Entry Point)

This is the **workhorse method** that deserializes `.spyproj` files.

```csharp
public static ProjectConfig Load(string projectFilePath, string? configuration = null)
```

**What it does**: 
1. Validates the file exists
2. Parses XML using `XDocument`
3. Extracts required elements (`RootNamespace`)
4. Extracts optional elements (with defaults)
5. Resolves glob patterns to actual files
6. Returns an immutable `ProjectConfig`

**Implementation flow**:

```
Load(projectFilePath)
    ↓
Validate file exists
    ↓
Parse XML with XDocument
    ↓
Extract <PropertyGroup> elements
    ↓
Loop through <ItemGroup> elements
    ↓
    For each <SpyFile Include="...">:
        Call ResolveGlobPattern()
    ↓
    For each <Reference Include="...">:
        Resolve relative paths
    ↓
    For each <ModulePath Include="...">:
        Resolve relative paths
    ↓
Validate at least one source file found
    ↓
Return new ProjectConfig { ... }
```

**Error handling**:
- `FileNotFoundException`: Project file doesn't exist
- `InvalidDataException`: Missing required XML elements or no source files

**Key parameters**:
- `projectFilePath`: Path to `.spyproj` file (can be relative or absolute)
- `configuration`: Optional override for Debug/Release (defaults to "Debug")

**Return value**: A fully-initialized `ProjectConfig` with all paths resolved to absolute paths.

### 3.4 `ProjectFileParser.FindProjectFile()` (Convenience Method)

```csharp
public static string? FindProjectFile(string directory)
```

**What it does**: Searches for a `.spyproj` file in a directory, with smart error handling.

**Behavior**:
- **0 files found**: Returns `null` (not an error—allows callers to handle this)
- **1 file found**: Returns the file path ✅
- **2+ files found**: Throws `InvalidOperationException` with helpful message listing all files

**Example error message**:
```
Multiple .spyproj files found in '/path/to/dir'. Please specify which project to build:
  - app.spyproj
  - lib.spyproj
```

**Use case**: Enables `sharpyc project .` to auto-discover the project file in the current directory.

### 3.5 `ProjectFileParser.ResolveGlobPattern()` (Private Helper)

```csharp
private static List<string> ResolveGlobPattern(string baseDirectory, string pattern)
```

**What it does**: Converts glob patterns into actual file paths using Microsoft's globbing library.

**How it works**:
```csharp
var matcher = new Matcher();
matcher.AddInclude(pattern);  // e.g., "src/**/*.spy"
var result = matcher.Execute(new DirectoryInfoWrapper(directoryInfo));
```

**Glob pattern examples**:
- `**/*.spy`: All `.spy` files recursively
- `src/**/*.spy`: All `.spy` files under `src/`
- `*.spy`: Only `.spy` files in project root

**Key implementation details**:
1. Uses `Microsoft.Extensions.FileSystemGlobbing` (industry-standard library)
2. Wraps `DirectoryInfo` with `DirectoryInfoWrapper` (required by the API)
3. Converts relative paths to **absolute paths** via `Path.GetFullPath()`
4. Filters with `File.Exists()` to exclude non-existent paths (defensive)

**Return value**: List of absolute file paths, ready for compilation.

---

## 4. Dependencies

### External NuGet Packages
- **`System.Xml.Linq`**: For parsing XML project files
- **`Microsoft.Extensions.FileSystemGlobbing`**: For glob pattern matching

### Internal Dependencies
- **`AssemblyCompiler.cs`**: Consumes `ProjectConfig` to compile multi-file projects
- **`Sharpy.Cli/Program.cs`**: Calls `ProjectFileParser.Load()` and `FindProjectFile()`
- **`Compiler.cs`**: Single-file compilation doesn't use `ProjectConfig`, but could be extended

### Coupling Analysis
- **Low coupling**: Only depends on standard .NET libraries
- **High cohesion**: Everything in this file is about project configuration
- **Interface design**: `ProjectConfig` is just data—no behavior beyond computed properties

---

## 5. Patterns and Design Decisions

### 5.1 Immutability via Init-Only Properties

```csharp
public string RootNamespace { get; init; } = string.Empty;
```

**Why?** Once a `ProjectConfig` is created, its state should never change. This:
- Prevents bugs from accidental mutation
- Makes the object thread-safe
- Simplifies reasoning about data flow

**Trade-off**: Requires creating new instances for modifications (not an issue here, as configs are created once).

### 5.2 Factory Pattern

`ProjectFileParser` is a static factory that creates `ProjectConfig` instances. This separates:
- **What**: The data model (`ProjectConfig`)
- **How**: The creation logic (`ProjectFileParser`)

**Alternative considered**: Could have made `Load()` a static method on `ProjectConfig` itself, but keeping parsing separate allows for:
- Different parsers in the future (e.g., JSON-based configs)
- Cleaner separation of concerns

### 5.3 Fail-Fast Validation

The parser validates **immediately** during `Load()`:

```csharp
if (string.IsNullOrWhiteSpace(rootNamespace))
{
    throw new InvalidDataException("Invalid .spyproj file: <RootNamespace> is required");
}
```

**Philosophy**: Better to fail early with a clear error than to pass around incomplete data structures.

### 5.4 Computed Properties vs. Stored Values

`OutputPath` and `OutputAssemblyPath` are computed, not stored. This is a **declarative** approach:

**Pros**:
- Always consistent—can't have stale values
- No need to update multiple fields when `Configuration` changes

**Cons**:
- Minor performance overhead (but these are called rarely)

### 5.5 Sensible Defaults

```csharp
public string OutputType { get; init; } = "library";
public string TargetFramework { get; init; } = "net8.0";
```

Default to building libraries for .NET 8.0—the most common case. This follows the **convention over configuration** principle.

### 5.6 Path Resolution Strategy

All paths are normalized to **absolute paths** during loading:

```csharp
ProjectFilePath = Path.GetFullPath(projectFilePath)
```

**Why?** Prevents ambiguity when the working directory changes during compilation. Absolute paths are unambiguous.

---

## 6. Debugging Tips

### 6.1 Common Issues

**Problem**: "Invalid .spyproj file: missing <RootNamespace>"

**Debug approach**:
1. Check the XML structure—is `<PropertyGroup>` present?
2. Is `<RootNamespace>` inside `<PropertyGroup>`?
3. Is there whitespace-only content? The check uses `IsNullOrWhiteSpace()`

**Problem**: "No source files found in project"

**Debug approach**:
1. Check the glob pattern in `<SpyFile Include="..." />`
2. Manually test the glob with a tool: `ls src/**/*.spy`
3. Add logging to `ResolveGlobPattern()` to see what's being matched:
   ```csharp
   var files = ResolveGlobPattern(projectDirectory, include);
   Console.WriteLine($"Pattern '{include}' matched {files.Count} files");
   ```

**Problem**: Wrong output path generated

**Debug approach**:
1. Inspect `OutputPath` property—does it reflect the expected configuration?
2. Check `Configuration` property value—is it "Debug" or "Release"?
3. Verify `TargetFramework`—should match your expected TFM

### 6.2 Breakpoint Locations

**Key places to set breakpoints**:
1. **Line 96** (`Load()` entry): See what project file is being loaded
2. **Line 142** (inside `ResolveGlobPattern` call): See glob results
3. **Line 180** (return statement): Inspect final `ProjectConfig` state

### 6.3 Quick Test

To test project loading without running the full compiler:

```csharp
var config = ProjectFileParser.Load("path/to/test.spyproj");
Console.WriteLine($"Root namespace: {config.RootNamespace}");
Console.WriteLine($"Source files: {string.Join(", ", config.SourceFiles)}");
Console.WriteLine($"Output: {config.OutputAssemblyPath}");
```

### 6.4 XML Parsing Gotchas

- **XML namespaces**: The parser uses `.Name.LocalName` to ignore XML namespaces. If someone adds `xmlns="..."` to their project file, it still works.
- **Case sensitivity**: XML element names are case-sensitive. `<propertygroup>` won't match `<PropertyGroup>`.

---

## 7. Contribution Guidelines

### 7.1 When to Modify This File

**Add new properties to `ProjectConfig` when**:
- Sharpy needs new project-level settings (e.g., optimization flags, package metadata)
- You're adding a compiler feature that requires configuration

**Example**: Adding nullable reference type warnings:
```csharp
public bool NullableWarnings { get; init; } = true;
```

Then update `Load()` to parse it:
```csharp
var nullableWarnings = bool.TryParse(
    propertyGroup.Element("NullableWarnings")?.Value, 
    out var value) ? value : true;
```

### 7.2 What Types of Changes Are Welcome

✅ **Encouraged**:
- Adding new optional project properties
- Improving error messages (make them more actionable)
- Supporting additional `<ItemGroup>` element types
- Adding validation for conflicting settings

❌ **Discouraged**:
- Breaking changes to existing property names (would break user projects)
- Adding complex business logic to `ProjectConfig` (keep it a data class)
- Making the XML schema overly complex

### 7.3 Testing Checklist

When you modify this file, test:

1. **Valid project files**: Does a minimal `.spyproj` still work?
   ```xml
   <Project>
     <PropertyGroup>
       <RootNamespace>Test</RootNamespace>
       <OutputType>exe</OutputType>
     </PropertyGroup>
     <ItemGroup>
       <SpyFile Include="*.spy" />
     </ItemGroup>
   </Project>
   ```

2. **Invalid project files**: Do you get good error messages?
   - Missing `<RootNamespace>`
   - No source files
   - Malformed XML

3. **Glob patterns**: Test various patterns
   - `**/*.spy` (recursive)
   - `src/**/*.spy` (subdirectory)
   - `*.spy` (flat)

4. **Path resolution**: Test with relative and absolute paths

### 7.4 Code Style Guidelines

**Follow existing patterns**:
- Use `init` for all `ProjectConfig` properties
- Use `string.Empty` instead of `""` for default values
- Use `Path.Combine()` for building paths (cross-platform)
- Add XML doc comments (`/// <summary>`) for all public members

**Example of a good addition**:
```csharp
/// <summary>
/// Whether to generate XML documentation file
/// </summary>
public bool GenerateDocumentationFile { get; init; } = false;
```

### 7.5 Example: Adding Support for Package References

Let's say you want to add NuGet package references. Here's how:

**Step 1**: Add property to `ProjectConfig`:
```csharp
/// <summary>
/// List of NuGet package references
/// </summary>
public List<PackageReference> PackageReferences { get; init; } = new();

public class PackageReference
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
}
```

**Step 2**: Parse in `ProjectFileParser.Load()`:
```csharp
// Inside the ItemGroup loop
foreach (var packageRef in itemGroup.Elements("PackageReference"))
{
    var name = packageRef.Attribute("Include")?.Value;
    var version = packageRef.Attribute("Version")?.Value ?? "1.0.0";
    
    if (!string.IsNullOrWhiteSpace(name))
    {
        packageReferences.Add(new PackageReference { Name = name, Version = version });
    }
}
```

**Step 3**: Add to constructor:
```csharp
return new ProjectConfig
{
    // ... existing properties
    PackageReferences = packageReferences
};
```

**Step 4**: Update `AssemblyCompiler` to use the new property.

### 7.6 Documentation Updates

When you add new properties:
1. Add XML doc comments to the property
2. Update `samples/` with example `.spyproj` files showing the new property
3. Update `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md` if it affects the project format

---

## Related Files to Explore Next

After understanding `ProjectConfig.cs`, check out:

1. **`AssemblyCompiler.cs`**: See how `ProjectConfig` is used to drive compilation
2. **`Sharpy.Cli/Program.cs`**: See how the CLI calls `ProjectFileParser.Load()`
3. **`samples/*.spyproj`**: Look at real-world project files
4. **`Compiler.cs`**: Single-file compilation (doesn't use `ProjectConfig`)

---

## Summary

`ProjectConfig.cs` is a small but critical file that:
- Defines the **data model** for Sharpy projects
- Parses **XML project files** with glob pattern support
- Follows **immutable design** with computed properties
- Provides **excellent error messages** for invalid configs
- Mimics **.NET/MSBuild conventions** for familiarity

It's a great example of a well-designed configuration system: simple, focused, and easy to extend. When in doubt, keep changes **minimal** and **backward-compatible**—many users will have existing `.spyproj` files that must continue to work.
