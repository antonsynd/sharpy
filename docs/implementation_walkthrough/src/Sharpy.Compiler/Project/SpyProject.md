# Walkthrough: SpyProject.cs

**Source File**: `src/Sharpy.Compiler/Project/SpyProject.cs`

---

## Overview

`SpyProject.cs` is the project file management system for the Sharpy compiler. It defines how `.spyproj` files (Sharpy's equivalent to `.csproj` files) are loaded, parsed, and used during compilation. This file sits at the **beginning** of the compiler pipeline, providing the configuration and file inputs needed before any lexing or parsing occurs.

**Role in Pipeline**: Pre-compilation → **SpyProject** → Lexer → Parser → Semantic Analysis → RoslynEmitter → C#

The file contains:
- **`SpyProject`**: A data class representing a parsed project with all its configuration
- **`SpyProjectLoader`**: A static utility class for loading and parsing `.spyproj` XML files

Think of this as the "build configuration layer" that determines what gets compiled and how.

---

## Class/Type Structure

### 1. SpyProject Class

```csharp
public class SpyProject
```

An immutable data transfer object (DTO) that holds all configuration for a Sharpy project. Uses C# 9's `init`-only properties to ensure immutability after construction.

**Key Properties**:

| Property | Type | Purpose |
|----------|------|---------|
| `ProjectFilePath` | `string` | Absolute path to the `.spyproj` file |
| `ProjectDirectory` | `string` | Directory containing the project (used as base for relative paths) |
| `RootNamespace` | `string` | Root namespace for all types in the project |
| `OutputType` | `string` | "Exe" for executables, "Library" for DLLs |
| `TargetFramework` | `string` | .NET framework version (e.g., "net8.0") |
| `AssemblyName` | `string?` | Output assembly name (defaults to `RootNamespace` if null) |
| `EntryPoint` | `string?` | Entry file for executables (defaults to "main.spy") |
| `SourceFiles` | `List<string>` | Resolved list of `.spy` files to compile |
| `References` | `List<string>` | .NET assemblies to reference during compilation |
| `ModulePaths` | `List<string>` | Search paths for resolving module imports |
| `Configuration` | `string` | Build configuration ("Debug" or "Release") |

**Computed Properties**:

- **`OutputPath`**: Constructs the output directory following .NET conventions: `bin/{Configuration}/{TargetFramework}/`
- **`OutputAssemblyPath`**: Full path to the output `.exe` or `.dll` file
- **`IsExecutable`**: Boolean indicating if this is an executable project

### 2. SpyProjectLoader Class

```csharp
public static class SpyProjectLoader
```

A static utility class that handles all XML parsing and file resolution logic. This is the only way to create a `SpyProject` instance from disk.

---

## Key Functions/Methods

### SpyProject.GetEntryPointPath()

```csharp
public string? GetEntryPointPath()
```

**Purpose**: Resolves the entry point file for executable projects.

**Algorithm**:
1. Returns `null` immediately if not an executable project
2. Uses `EntryPoint` property or defaults to `"main.spy"`
3. If the entry point is an absolute path, checks if it exists
4. Otherwise, searches `SourceFiles` for a file with matching name (case-insensitive)
5. Returns the first match or `null` if not found

**Usage**: Called by the compiler to determine which file contains the `main()` function.

**Debugging Tip**: If you're getting "entry point not found" errors, check that the file specified in `<EntryPoint>` matches exactly with one of the resolved `SourceFiles`.

---

### SpyProject.ToProjectConfig()

```csharp
public ProjectConfig ToProjectConfig()
```

**Purpose**: Converts the `SpyProject` into a `ProjectConfig` object used by the compiler.

**Why It Exists**: Separation of concerns—`SpyProject` is the parsed representation of the XML file, while `ProjectConfig` is the compiler's internal representation. This allows the compiler to accept configurations from sources other than `.spyproj` files (e.g., command-line arguments, in-memory builds).

**Returns**: A new `ProjectConfig` instance with all properties copied over.

---

### SpyProjectLoader.Load()

```csharp
public static SpyProject Load(string projectFilePath, string? configuration = null)
```

**Purpose**: The main entry point for loading a `.spyproj` file. This method does all the heavy lifting of XML parsing and file resolution.

**Parameters**:
- `projectFilePath`: Absolute or relative path to the `.spyproj` file
- `configuration`: Optional build configuration ("Debug" or "Release"), defaults to "Debug"

**Algorithm**:

1. **File Validation** (lines 146-149)
   - Throws `FileNotFoundException` if the project file doesn't exist

2. **XML Parsing** (lines 151-158)
   - Loads XML using `XDocument.Load()`
   - Validates that root element is `<Project>`
   - Throws `InvalidDataException` if structure is invalid

3. **PropertyGroup Parsing** (lines 160-176)
   - Extracts `<RootNamespace>` (required)
   - Extracts `<OutputType>` (defaults to "Library")
   - Extracts `<TargetFramework>` (defaults to "net8.0")
   - Extracts `<AssemblyName>` (optional)
   - Extracts `<EntryPoint>` (optional)

4. **ItemGroup Parsing** (lines 178-237)
   - **Source Files**: Processes `<SpyFile>` and `<SourceFile>` elements
     - Supports `Include` attribute for glob patterns
     - Supports `Exclude` attribute for exclusion patterns
     - Calls `ResolveGlobPattern()` to expand patterns to actual files
   - **References**: Processes `<Reference>` elements
     - Resolves relative paths to absolute paths
   - **Module Paths**: Processes `<ModulePath>` elements
     - Resolves relative paths to absolute paths

5. **Post-Processing** (lines 239-246)
   - Removes duplicate source files using `Distinct()`
   - Validates that at least one source file was found
   - Throws `InvalidDataException` if no source files

6. **Object Construction** (lines 248-261)
   - Returns a fully initialized `SpyProject` instance

**Example .spyproj File**:

```xml
<Project>
  <PropertyGroup>
    <RootNamespace>MyApp</RootNamespace>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <EntryPoint>main.spy</EntryPoint>
  </PropertyGroup>

  <ItemGroup>
    <SpyFile Include="src/**/*.spy" Exclude="src/tests/**" />
    <Reference Include="System.Text.Json.dll" />
    <ModulePath Include="lib/" />
  </ItemGroup>
</Project>
```

---

### SpyProjectLoader.FindProjectFile()

```csharp
public static string? FindProjectFile(string directory)
```

**Purpose**: Auto-discovers a `.spyproj` file in a directory when the user doesn't specify one explicitly.

**Algorithm**:
1. Searches for all `*.spyproj` files in the specified directory (non-recursive)
2. Returns `null` if no project files found
3. Throws `InvalidOperationException` if multiple project files found (ambiguity error)
4. Returns the single project file found

**Usage**: Typically called by the CLI when the user runs `sharpy build` in a directory without specifying a project file.

**Error Handling**: Provides helpful error messages listing all found project files when there's ambiguity.

---

### SpyProjectLoader.ResolveGlobPattern()

```csharp
private static List<string> ResolveGlobPattern(string baseDirectory, string includePattern, string? excludePattern = null)
```

**Purpose**: Resolves glob patterns (like `**/*.spy`) to actual file paths, with support for exclusions.

**Parameters**:
- `baseDirectory`: Base directory for relative path resolution
- `includePattern`: Glob pattern to include (e.g., `"src/**/*.spy"`)
- `excludePattern`: Optional semicolon-separated exclusion patterns (e.g., `"tests/**;temp/**"`)

**Algorithm**:
1. Creates a `Matcher` from `Microsoft.Extensions.FileSystemGlobbing`
2. Adds the include pattern
3. If exclude pattern provided, splits by semicolon and adds each exclusion
4. Executes the matcher against the directory structure
5. Converts relative paths to absolute paths
6. Filters to only existing files
7. Returns the list of resolved file paths

**Glob Pattern Support**:
- `**` - Recursive directory search
- `*` - Wildcard for file/directory names
- `?` - Single character wildcard

**Example**:
```xml
<SpyFile Include="**/*.spy" Exclude="tests/**;bin/**;obj/**" />
```
This includes all `.spy` files recursively but excludes anything in `tests/`, `bin/`, or `obj/` directories.

---

## Dependencies

### External Dependencies

1. **System.Xml.Linq** (`XDocument`, `XElement`)
   - Used for parsing XML project files
   - Provides LINQ-to-XML for easy element querying

2. **Microsoft.Extensions.FileSystemGlobbing**
   - Provides the `Matcher` class for glob pattern resolution
   - Standard .NET library for file pattern matching

### Internal Dependencies

1. **`ProjectConfig`** (referenced but defined elsewhere)
   - The compiler's internal representation of project configuration
   - `SpyProject.ToProjectConfig()` converts to this type

2. **Compiler Pipeline** (downstream consumers)
   - The compiler uses `SpyProject` to determine what files to compile
   - The `SourceFiles` list is fed into the Lexer
   - The `References` list is used during code generation (RoslynEmitter)

---

## Patterns and Design Decisions

### 1. Immutability via Init-Only Properties

All properties in `SpyProject` use `init` accessors, making the object immutable after construction. This prevents accidental mutations during compilation and makes the code safer for concurrent access.

```csharp
public string RootNamespace { get; init; } = string.Empty;
```

### 2. Separation of Parsing and Representation

- **`SpyProject`**: Pure data representation
- **`SpyProjectLoader`**: All parsing logic

This follows the Single Responsibility Principle and makes testing easier (you can construct `SpyProject` instances directly for testing without needing XML files).

### 3. MSBuild-Inspired XML Schema

The `.spyproj` format mimics MSBuild's `.csproj` structure:
- `<PropertyGroup>` for scalar properties
- `<ItemGroup>` for collections
- `Include` and `Exclude` attributes for patterns

**Rationale**: Familiar to .NET developers, leverages existing tooling knowledge.

### 4. Glob Pattern Resolution

Using `Microsoft.Extensions.FileSystemGlobbing` provides:
- Cross-platform file matching
- Standard glob syntax
- Efficient directory traversal

This is the same library used by .NET's project system.

### 5. Defensive Validation

The loader validates at multiple points:
- File existence check before parsing
- Required element validation (`<Project>`, `<PropertyGroup>`, `<RootNamespace>`)
- At least one source file requirement

**Rationale**: Fail fast with clear error messages rather than mysterious compilation errors later.

### 6. Dual Element Names

Both `<SpyFile>` and `<SourceFile>` are supported for specifying source files:

```csharp
foreach (var spyFile in itemGroup.Elements("SpyFile")) { ... }
foreach (var sourceFile in itemGroup.Elements("SourceFile")) { ... }
```

**Rationale**: Flexibility for users, backward compatibility if naming conventions change.

---

## Debugging Tips

### Problem: "No source files found in project"

**Possible Causes**:
1. Glob patterns don't match any files
2. Exclude patterns are too broad
3. Files don't exist on disk yet

**Debug Approach**:
- Add a breakpoint in `ResolveGlobPattern()` at line 306 to see what the matcher returns
- Check `result.Files` to see if any files matched before filtering
- Verify the `baseDirectory` is correct
- Test your glob pattern with a simple tool first

### Problem: "Entry point not found" in executable projects

**Check**:
1. `GetEntryPointPath()` returns non-null value (line 111)
2. The `EntryPoint` file name matches a file in `SourceFiles` (case-insensitive)
3. If `EntryPoint` is not specified, ensure `main.spy` exists in source files

**Debug Approach**:
- Print `SourceFiles` list to see what was resolved
- Print `GetEntryPointPath()` result to see what file is selected
- Check casing—Windows is case-insensitive but Linux/Mac are not

### Problem: "Multiple .spyproj files found"

This happens in `FindProjectFile()` when a directory has multiple project files.

**Solution**: Explicitly specify which project file to use via command-line argument.

### Problem: References not resolving

**Check**:
1. Reference paths are absolute after resolution (line 218-221)
2. Referenced assemblies actually exist on disk
3. Path separators are correct for the platform

**Debug Approach**:
- Add logging in the `Reference` parsing loop to see resolved paths
- Verify `Path.Combine()` produces valid paths on your platform

### Problem: Duplicate source files appearing

This should be prevented by `Distinct()` at line 240, but if you're seeing duplicates downstream:
- Check if multiple `<ItemGroup>` elements have overlapping patterns
- Verify the duplicate removal is working correctly
- Look for symbolic links that might appear as different files

---

## Contribution Guidelines

### Types of Changes to This File

1. **Adding New Project Properties**
   - Add property to `SpyProject` class
   - Add parsing logic in `SpyProjectLoader.Load()`
   - Update `ToProjectConfig()` if the property needs to be passed to the compiler
   - Update XML schema documentation

2. **Supporting New Element Types**
   - Add parsing loop in the `ItemGroup` section (lines 183-237)
   - Follow the pattern of existing elements (`Reference`, `ModulePath`, etc.)
   - Resolve relative paths to absolute paths
   - Add validation if required

3. **Enhancing Glob Pattern Support**
   - Modify `ResolveGlobPattern()` method
   - Consider backward compatibility with existing patterns
   - Test on Windows, Linux, and macOS (path separator differences)

4. **Improving Error Messages**
   - Find `throw` statements and enhance messages
   - Include context (file path, element name, etc.)
   - Suggest corrective actions when possible

### Testing Considerations

When modifying this file:
- Test with various glob patterns (recursive, wildcards, exclusions)
- Test with both absolute and relative paths
- Test error cases (missing files, invalid XML, missing required elements)
- Test on different operating systems if changing path logic
- Verify backward compatibility with existing `.spyproj` files

### Code Style Conventions

- Use `init`-only properties for immutability
- Validate inputs early and throw descriptive exceptions
- Use `Path.Combine()` for all path operations (cross-platform)
- Keep parsing logic in the loader, keep data in the project class
- Follow existing XML element naming conventions (PascalCase, singular for single values)

---

## Cross-References

### Related Files

This file works closely with:

1. **`ProjectConfig`** (exact location TBD)
   - Receives converted configuration from `SpyProject.ToProjectConfig()`
   - Used by the compiler for actual compilation

2. **CLI/Entry Point** (likely in `src/Sharpy.Cli/`)
   - Calls `SpyProjectLoader.Load()` to read project files
   - Uses `FindProjectFile()` for auto-discovery

3. **Compiler** (likely in `src/Sharpy.Compiler/Compiler.cs`)
   - Consumes `ProjectConfig` to drive compilation
   - Uses `SourceFiles` to determine what to compile
   - Uses `References` during code generation

### Documentation Files

For a complete understanding of the compilation pipeline:
- **Lexer**: See `docs/implementation_walkthrough/src/Sharpy.Compiler/Lexer/Lexer.md`
- **Parser**: See `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/`
- **Code Generation**: See `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/RoslynEmitter.md`
- **Type Mapping**: See `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/TypeMapper.md`

---

## Summary

`SpyProject.cs` is the foundation of the Sharpy build system. It provides:
- **Input**: `.spyproj` XML files
- **Output**: Validated project configuration with resolved file paths
- **Key Responsibility**: Translating user-friendly project files into compiler-ready configuration

When debugging build issues, **start here**. Most compilation problems (missing files, wrong entry point, reference errors) originate from project configuration, making this file the first place to investigate.
