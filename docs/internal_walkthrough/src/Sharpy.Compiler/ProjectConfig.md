# Walkthrough: ProjectConfig.cs

**Source File**: `src/Sharpy.Compiler/ProjectConfig.cs`

---

## 1. Overview

`ProjectConfig.cs` is the backbone of Sharpy's project system. This file provides two main components:

1. **`ProjectConfig`** - A data class that represents a parsed `.spyproj` project file
2. **`ProjectFileParser`** - A utility class that loads and parses `.spyproj` XML files into `ProjectConfig` objects

**Role in the Compiler Pipeline:**
When you run `sharpyc project myapp.spyproj`, this file is responsible for:
- Reading the XML project file
- Resolving glob patterns (like `src/**/*.spy`) to actual file paths
- Extracting build configuration (Debug/Release, target framework, output type)
- Providing the compiler with all the information it needs to build a multi-file Sharpy project

Think of it as the bridge between the `.spyproj` XML file on disk and the strongly-typed configuration object the compiler needs to orchestrate compilation.

---

## 2. Class/Type Structure

### 2.1 `ProjectConfig` Class

This is a **data transfer object (DTO)** that holds all configuration for a Sharpy project. It uses C# 9+ **init-only properties** to create an immutable-after-construction object.

**Key Properties:**

```csharp
public class ProjectConfig
{
    // File system locations
    public string ProjectFilePath { get; init; }    // Full path to .spyproj
    public string ProjectDirectory { get; init; }   // Project root directory
    
    // Build settings
    public string RootNamespace { get; init; }      // e.g., "MyApp"
    public string OutputType { get; init; }         // "exe" or "library"
    public string TargetFramework { get; init; }    // e.g., "net8.0"
    public string Configuration { get; init; }      // "Debug" or "Release"
    
    // Compilation inputs
    public List<string> SourceFiles { get; init; }  // Resolved .spy files
    public List<string> References { get; init; }   // .NET assemblies
    public List<string> ModulePaths { get; init; }  // Module search paths
    
    // Computed properties
    public string OutputPath { get; }               // bin/Debug/net8.0/
    public virtual string OutputAssemblyPath { get; } // bin/Debug/net8.0/MyApp.dll
}
```

**Design Decision:** Properties use `init` instead of `set`, making instances immutable after construction. This prevents accidental modification and makes the object thread-safe.

### 2.2 `ProjectFileParser` Class

This is a **static utility class** (all methods are static) that handles the low-level work of parsing `.spyproj` files.

**Key Methods:**
- `Load(string projectFilePath, string? configuration)` - Main entry point for loading a project
- `FindProjectFile(string directory)` - Auto-discovers `.spyproj` files in a directory
- `ResolveGlobPattern(string baseDirectory, string pattern)` - Expands glob patterns to file paths (private helper)

---

## 3. Key Functions/Methods

### 3.1 `ProjectConfig.OutputPath` (Property)

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

**What it does:**
- Computes the output directory following .NET conventions: `<project>/bin/<Configuration>/<TargetFramework>/`
- Example: `myapp/bin/Debug/net8.0/`

**Why it's computed:**
- The path depends on `Configuration` (which can be overridden at build time)
- No need to store it separately when it can be derived from other properties

**Usage:**
```csharp
var config = ProjectFileParser.Load("myapp.spyproj", "Release");
Console.WriteLine(config.OutputPath);  // myapp/bin/Release/net8.0/
```

### 3.2 `ProjectConfig.OutputAssemblyPath` (Property)

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

**What it does:**
- Computes the full path to the output assembly (DLL or EXE)
- Falls back to `RootNamespace` if `AssemblyName` is not specified
- Chooses extension based on `OutputType`

**Why it's virtual:**
- Allows derived classes to override the assembly path computation if needed
- Provides extensibility for future project types

**Example outputs:**
- `myapp/bin/Debug/net8.0/MyApp.dll` (library)
- `myapp/bin/Release/net8.0/calculator.exe` (exe)

### 3.3 `ProjectFileParser.Load()` - The Heart of the Parser

```csharp
public static ProjectConfig Load(string projectFilePath, string? configuration = null)
```

**Parameters:**
- `projectFilePath` - Full or relative path to the `.spyproj` file
- `configuration` - Optional override for build configuration (defaults to "Debug")

**Return Value:**
- A fully populated `ProjectConfig` object

**What it does (step-by-step):**

#### Step 1: Load and Validate XML
```csharp
if (!File.Exists(projectFilePath))
    throw new FileNotFoundException($"Project file not found: {projectFilePath}");

var document = XDocument.Load(projectFilePath);
var root = document.Root;

if (root == null || root.Name.LocalName != "Project")
    throw new InvalidDataException("Invalid .spyproj file: missing <Project> root element");
```
- Uses `XDocument` (LINQ to XML) for parsing
- Validates that the root element is `<Project>`

#### Step 2: Parse PropertyGroup
```csharp
var propertyGroup = root.Element("PropertyGroup");
if (propertyGroup == null)
    throw new InvalidDataException("Invalid .spyproj file: missing <PropertyGroup> element");

var rootNamespace = propertyGroup.Element("RootNamespace")?.Value;
if (string.IsNullOrWhiteSpace(rootNamespace))
    throw new InvalidDataException("Invalid .spyproj file: <RootNamespace> is required");
```
- Extracts required and optional properties
- **Required:** `RootNamespace`
- **Optional:** `OutputType`, `TargetFramework`, `AssemblyName` (have defaults)

#### Step 3: Parse ItemGroup Elements
```csharp
foreach (var itemGroup in root.Elements("ItemGroup"))
{
    // Parse SpyFile includes
    foreach (var spyFile in itemGroup.Elements("SpyFile"))
    {
        var include = spyFile.Attribute("Include")?.Value;
        if (!string.IsNullOrWhiteSpace(include))
        {
            var resolvedFiles = ResolveGlobPattern(projectDirectory, include);
            sourceFiles.AddRange(resolvedFiles);
        }
    }
    // ... similar for Reference and ModulePath
}
```
- Iterates all `<ItemGroup>` elements
- Handles three types of items:
  - `<SpyFile Include="src/**/*.spy" />` - Source files (supports globs)
  - `<Reference Include="path/to/assembly.dll" />` - .NET assembly references
  - `<ModulePath Include="libs/" />` - Module search paths

**Important:** Glob patterns are resolved immediately, so `SourceFiles` contains actual file paths, not patterns.

#### Step 4: Validation and Construction
```csharp
if (sourceFiles.Count == 0)
    throw new InvalidDataException("No source files found in project. Add <SpyFile Include=\"...\" /> elements.");

return new ProjectConfig { /* ... */ };
```
- Ensures at least one source file was found
- Constructs and returns the `ProjectConfig` object

**Error Handling Philosophy:**
- **Fail fast** with descriptive error messages
- Better to throw during parsing than fail later during compilation

### 3.4 `ProjectFileParser.FindProjectFile()` - Project Discovery

```csharp
public static string? FindProjectFile(string directory)
```

**What it does:**
- Searches for `.spyproj` files in the specified directory
- Returns `null` if none found
- Throws if multiple `.spyproj` files exist (ambiguity error)

**Usage in CLI:**
```csharp
// User runs: sharpyc project
// CLI looks for .spyproj in current directory
var projectFile = ProjectFileParser.FindProjectFile(Directory.GetCurrentDirectory());
if (projectFile == null)
    Console.Error.WriteLine("No .spyproj file found in current directory");
else
    var config = ProjectFileParser.Load(projectFile);
```

**Design Decision:**
- Returns `null` instead of throwing when no project is found
- This allows the caller to decide how to handle the case (CLI vs library usage)
- Throws only when there's ambiguity (multiple projects)

### 3.5 `ResolveGlobPattern()` - Glob Expansion

```csharp
private static List<string> ResolveGlobPattern(string baseDirectory, string pattern)
{
    var matcher = new Matcher();
    matcher.AddInclude(pattern);

    var directoryInfo = new DirectoryInfo(baseDirectory);
    var result = matcher.Execute(new DirectoryInfoWrapper(directoryInfo));

    return result.Files
        .Select(f => Path.GetFullPath(Path.Combine(baseDirectory, f.Path)))
        .Where(File.Exists)
        .ToList();
}
```

**What it does:**
- Expands glob patterns like `src/**/*.spy` into actual file paths
- Uses Microsoft's `FileSystemGlobbing` library (same as .NET SDK uses)

**Supported Patterns:**
- `*` - Match any characters in a single directory
- `**` - Match any characters across directories (recursive)
- `src/**/*.spy` - All `.spy` files in `src/` and subdirectories
- `tests/*.spy` - All `.spy` files directly in `tests/`

**Why it filters with `File.Exists`:**
- The glob matcher might return paths that were deleted or are symbolic links
- Ensures only real, existing files are included

**Example:**
```
Project structure:
myapp/
  src/
    main.spy
    utils/
      helper.spy
  tests/
    test_main.spy
```

Pattern `src/**/*.spy` resolves to:
- `C:\myapp\src\main.spy`
- `C:\myapp\src\utils\helper.spy`

---

## 4. Dependencies

### External Dependencies (NuGet Packages)

1. **`System.Xml.Linq`** (.NET BCL)
   - Used for XML parsing via `XDocument`
   - Why: Provides a LINQ-friendly API for XML (easier than `XmlDocument`)

2. **`Microsoft.Extensions.FileSystemGlobbing`**
   - Used for glob pattern matching
   - Why: Battle-tested implementation used by .NET SDK itself
   - Same library that powers `<ItemGroup>` in `.csproj` files

### Internal Dependencies

- **None!** This file is remarkably self-contained
- Does not reference any other Sharpy.Compiler types
- Acts as a standalone utility for project file parsing

**Why this matters:**
- Can be tested in isolation
- Could potentially be extracted to a separate library
- Low coupling with the rest of the compiler

### Reverse Dependencies (Who Uses This?)

```
Sharpy.Cli (Program.cs)
    └─> ProjectFileParser.Load()
            └─> ProjectConfig
                    └─> AssemblyCompiler (multi-file compilation)
```

The CLI uses this to load projects, then passes the `ProjectConfig` to `AssemblyCompiler` for compilation.

---

## 5. Patterns and Design Decisions

### 5.1 Immutability Pattern

All `ProjectConfig` properties use `{ get; init; }`:

```csharp
public string RootNamespace { get; init; } = string.Empty;
```

**Benefits:**
- Thread-safe (can be safely shared across threads)
- Prevents accidental modification after parsing
- Makes reasoning about state easier (config doesn't change after creation)

**Alternative considered:** Mutable properties with `{ get; set; }` - rejected because projects shouldn't change after loading.

### 5.2 Static Parser Pattern

`ProjectFileParser` uses only static methods:

```csharp
public class ProjectFileParser
{
    public static ProjectConfig Load(string projectFilePath, string? configuration = null)
    // ...
}
```

**Why:**
- No instance state needed
- Parser is a pure transformation: XML file → `ProjectConfig` object
- Simplifies usage: `ProjectFileParser.Load(path)` vs `new ProjectFileParser().Load(path)`

### 5.3 Fail-Fast Validation

The parser validates extensively during parsing:

```csharp
if (string.IsNullOrWhiteSpace(rootNamespace))
    throw new InvalidDataException("Invalid .spyproj file: <RootNamespace> is required");

if (sourceFiles.Count == 0)
    throw new InvalidDataException("No source files found in project...");
```

**Philosophy:**
- Better to fail immediately with a clear error message
- Prevents mysterious failures later in the compilation pipeline
- User gets actionable feedback right away

### 5.4 Convention Over Configuration

Default values match .NET conventions:

```csharp
public string OutputType { get; init; } = "library";
public string TargetFramework { get; init; } = "net8.0";
public string Configuration { get; init; } = "Debug";
```

- Users familiar with .NET will find Sharpy projects intuitive
- Most projects can omit these properties and use defaults

### 5.5 Separation of Concerns

- `ProjectConfig` - **Data** (what is the project configuration?)
- `ProjectFileParser` - **Behavior** (how do we load configuration?)

This follows the **Single Responsibility Principle**. If we need to support a different format (e.g., JSON), we can write `JsonProjectFileParser` that produces the same `ProjectConfig`.

---

## 6. Debugging Tips

### 6.1 Common Issues and How to Debug Them

#### Issue: "No source files found in project"

**Cause:** Glob pattern didn't match any files

**Debug steps:**
1. Check the glob pattern in `.spyproj`:
   ```xml
   <SpyFile Include="src/**/*.spy" />
   ```

2. Add a breakpoint in `ResolveGlobPattern()` and inspect:
   - `baseDirectory` - Is it the correct project directory?
   - `pattern` - Is the pattern what you expected?
   - `result.Files` - What files did the matcher find?

3. Common mistakes:
   - Pattern uses `\` instead of `/` (use forward slashes)
   - Pattern is absolute instead of relative
   - Files are in a different location than expected

#### Issue: "Multiple .spyproj files found"

**Cause:** Directory has more than one `.spyproj` file

**Debug steps:**
1. Check what files exist:
   ```bash
   ls -la *.spyproj
   ```

2. Either:
   - Remove the extra `.spyproj` file
   - Specify the project explicitly: `sharpyc project myapp.spyproj`

#### Issue: Path resolution problems (Windows vs Linux)

**Cause:** Hardcoded path separators or incorrect use of `Path.Combine()`

**Debug steps:**
1. Check if paths use hardcoded separators:
   ```csharp
   // ❌ Bad - hardcoded separator
   var path = projectDir + "/bin/output.dll";
   
   // ✅ Good - use Path.Combine
   var path = Path.Combine(projectDir, "bin", "output.dll");
   ```

2. Use `Path.GetFullPath()` to normalize paths:
   ```csharp
   var normalized = Path.GetFullPath(somePath);
   ```

### 6.2 Logging and Diagnostics

To understand what's happening during parsing, add debug output:

```csharp
public static ProjectConfig Load(string projectFilePath, string? configuration = null)
{
    Console.WriteLine($"Loading project: {projectFilePath}");
    Console.WriteLine($"Project directory: {projectDirectory}");
    
    // ... parse PropertyGroup ...
    Console.WriteLine($"RootNamespace: {rootNamespace}");
    Console.WriteLine($"OutputType: {outputType}");
    
    // ... parse source files ...
    Console.WriteLine($"Found {sourceFiles.Count} source files:");
    foreach (var file in sourceFiles)
        Console.WriteLine($"  - {file}");
    
    return new ProjectConfig { /* ... */ };
}
```

### 6.3 Testing Project Files

Create a minimal test `.spyproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
        <OutputType>exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include="*.spy" />
    </ItemGroup>
</Project>
```

Test it:
```csharp
var config = ProjectFileParser.Load("test.spyproj");
Assert.NotNull(config);
Assert.Equal("TestApp", config.RootNamespace);
Assert.True(config.SourceFiles.Count > 0);
```

---

## 7. Contribution Guidelines

### 7.1 When to Modify This File

Add features to this file when you need to:

1. **Support new project properties**
   - Example: Add support for `<Version>` or `<Authors>`
   - Add property to `ProjectConfig`
   - Parse it in `ProjectFileParser.Load()`
   - Add tests

2. **Support new item types**
   - Example: Add `<Dependency>` for Sharpy package references
   - Parse in the `ItemGroup` loop
   - Store in a new `List<string>` property

3. **Support alternative project formats**
   - Example: Add JSON project support
   - Create `JsonProjectFileParser`
   - Return the same `ProjectConfig` type

### 7.2 What NOT to Add Here

- **Compilation logic** - Belongs in `Compiler.cs` or `AssemblyCompiler.cs`
- **File I/O for source files** - Belongs in the compiler pipeline
- **Build orchestration** - Belongs in the CLI or build tools

**This file should only:**
- Parse project files
- Resolve file paths
- Validate configuration
- Return configuration objects

### 7.3 Testing Checklist

When modifying this file, test:

- [ ] Valid project files parse correctly
- [ ] Missing required properties throw clear errors
- [ ] Glob patterns resolve to correct files
- [ ] Relative paths resolve correctly
- [ ] Multiple `.spyproj` files are detected
- [ ] Invalid XML throws `InvalidDataException`
- [ ] Empty project directories are handled gracefully
- [ ] Windows and Unix path separators work

### 7.4 Common Contributions

#### Adding a New Property

1. Add to `ProjectConfig`:
   ```csharp
   public string NewProperty { get; init; } = "default";
   ```

2. Parse in `Load()`:
   ```csharp
   var newProperty = propertyGroup.Element("NewProperty")?.Value ?? "default";
   ```

3. Assign in constructor:
   ```csharp
   return new ProjectConfig
   {
       // ... existing properties ...
       NewProperty = newProperty
   };
   ```

4. Add test:
   ```csharp
   [Fact]
   public void TestNewProperty()
   {
       var xml = @"
       <Project>
           <PropertyGroup>
               <RootNamespace>Test</RootNamespace>
               <NewProperty>value</NewProperty>
           </PropertyGroup>
       </Project>";
       
       var config = /* parse xml */;
       Assert.Equal("value", config.NewProperty);
   }
   ```

#### Adding a New Item Type

Follow the same pattern as `<SpyFile>`, `<Reference>`, or `<ModulePath>`:

```csharp
// In Load() method
var newItems = new List<string>();

foreach (var itemGroup in root.Elements("ItemGroup"))
{
    foreach (var item in itemGroup.Elements("NewItem"))
    {
        var include = item.Attribute("Include")?.Value;
        if (!string.IsNullOrWhiteSpace(include))
        {
            // Process the item (resolve paths, validate, etc.)
            newItems.Add(ProcessItem(include));
        }
    }
}
```

### 7.5 Best Practices

1. **Keep it simple** - This is a data parsing file, not business logic
2. **Validate early** - Throw exceptions with clear messages during parsing
3. **Use Path.Combine()** - Never hardcode path separators
4. **Add XML comments** - Document what each property means
5. **Test on multiple platforms** - Windows paths differ from Unix
6. **Follow .NET conventions** - Users expect Sharpy projects to work like C# projects

### 7.6 Related Files to Review

When working on project configuration, also look at:

- **`AssemblyCompiler.cs`** - How `ProjectConfig` is used during compilation
- **`src/Sharpy.Cli/Program.cs`** - How projects are loaded from the CLI
- **`samples/calculator_app/calculator.spyproj`** - Example of a real project file
- **Test projects** - Look for `.spyproj` files in test directories

---

## Quick Reference Card

```csharp
// Load a project file
var config = ProjectFileParser.Load("myapp.spyproj", "Release");

// Auto-discover project in directory
var projectPath = ProjectFileParser.FindProjectFile(".");
if (projectPath != null)
    var config = ProjectFileParser.Load(projectPath);

// Access configuration
Console.WriteLine(config.RootNamespace);      // "MyApp"
Console.WriteLine(config.OutputType);         // "exe"
Console.WriteLine(config.OutputAssemblyPath); // "bin/Release/net8.0/MyApp.exe"

// Iterate source files
foreach (var file in config.SourceFiles)
    Console.WriteLine(file);  // Full paths to .spy files

// Check references
if (config.References.Count > 0)
    Console.WriteLine("Project has external dependencies");
```

---

## Summary

`ProjectConfig.cs` is a focused, well-designed file that does one thing well: parsing `.spyproj` files. It's remarkably self-contained, has no dependencies on the rest of the compiler, and follows .NET conventions closely. When working with this file:

- **Understand** that it's purely about parsing and data - no compilation logic
- **Validate** early and fail fast with clear error messages
- **Test** on multiple platforms and with various project structures
- **Keep it simple** - resist the urge to add compilation concerns here

This file is a great example of separation of concerns and single responsibility principle in action. 🚀
