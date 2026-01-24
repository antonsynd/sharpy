# Walkthrough: Program.cs

**Source File**: `src/Sharpy.Cli/Program.cs`

---

## Overview

`Program.cs` is the entry point for the **Sharpy Compiler CLI** (`sharpyc`). This file implements a command-line interface that exposes all compiler functionality to users, from basic compilation to advanced debugging and introspection tools.

**Role in the Compiler Pipeline:**
- Serves as the **user-facing interface** to the entire Sharpy compiler
- Orchestrates the compilation pipeline: Source → Lexer → Parser → Semantic Analysis → Code Generation → .NET Assembly
- Provides diagnostic tools for inspecting intermediate representations (tokens, AST, generated C#)
- Manages project-based compilation via `.spyproj` files
- Handles the "compile and run" workflow for quick iteration

This is the first file a developer will interact with when using Sharpy, and it's structured around the [System.CommandLine](https://github.com/dotnet/command-line-api) library for robust CLI argument parsing.

---

## Architecture: Command Structure

The CLI is organized into **5 main commands**, each with specific responsibilities:

| Command | Purpose | Example Usage |
|---------|---------|---------------|
| `build` | Compile `.spy` files to `.dll` or `.exe` | `sharpyc build main.spy --type exe -o app.exe` |
| `run` | Compile and immediately execute a `.spy` file | `sharpyc run script.spy --args arg1 arg2` |
| `project` | Build multi-file projects from `.spyproj` | `sharpyc project MyApp.spyproj --configuration Release` |
| `emit` | Output intermediate representations | `sharpyc emit csharp main.spy` (see tokens, AST, or C#) |
| `cache` | Manage the overload discovery cache | `sharpyc cache clear` |

### Global Options

Four global options apply across all commands:

```csharp
--log-level <None|Error|Warning|Info|Debug>  // Control compiler diagnostic output
--log-file <path>                            // Redirect logs to file
--metrics-format <text|json>                 // Output compilation performance metrics
--metrics-output <path>                      // Write metrics to file
```

These are configured in lines 20-28 and parsed by each command handler.

---

## Class Structure

### Main Class: `Program`

A single static class with:
- **`Main(string[] args)`** - Entry point that builds the command tree
- **Command Handlers** - Methods like `HandleBuildCommand`, `HandleRunCommand`, etc.
- **Compiler Pipeline Helpers** - `EmitTokens`, `EmitAst`, `EmitCSharp`, `CompileToBinary`
- **Utilities** - `CreateLogger`, `OutputMetrics`, `ValidateInputFile`, etc.

### Inner Class: `SingleFileProjectConfig`

A wrapper class (lines 997-1030) that extends `ProjectConfig` to handle single-file compilation scenarios. This allows the `build` and `run` commands to reuse the same compilation infrastructure as full projects.

**Why this exists:**
- The `Sharpy.Compiler.Compiler` class expects a `ProjectConfig` object
- Single-file builds don't have a `.spyproj` file, so we synthesize a minimal config
- Overrides `OutputAssemblyPath` to respect the user's `--output` flag

---

## Key Functions and Workflows

### 1. `Main(string[] args)` - Command Tree Construction

**Lines 15-215**

This method constructs the entire CLI structure using the builder pattern from `System.CommandLine`:

```csharp
var rootCommand = new RootCommand("sharpyc - Sharpy Compiler");

// Add global options
rootCommand.Options.Add(logLevelOption);
rootCommand.Options.Add(logFileOption);
// ... etc

// Add subcommands
rootCommand.Subcommands.Add(buildCommand);
rootCommand.Subcommands.Add(runCommand);
// ... etc

return rootCommand.Parse(args).Invoke();
```

**Key Design Pattern:**
Each command follows a consistent structure:
1. Create the command object with description
2. Define arguments (required positional parameters)
3. Define options (optional flags like `--output`)
4. Call `SetAction()` with a lambda that extracts values and delegates to a handler

**Example - Build Command (lines 30-67):**

```csharp
var buildCommand = new Command("build", "Compile a Sharpy source file to a binary or library");
var buildInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file to compile" };
var buildTypeOpt = new Option<string?>("--type") { Description = "Output type: 'exe' or 'library'" };

buildCommand.Arguments.Add(buildInputArg);
buildCommand.Options.Add(buildTypeOpt);
// ... more options

buildCommand.SetAction((parseResult) => {
    var input = parseResult.GetValue(buildInputArg)!;
    var type = parseResult.GetValue(buildTypeOpt) ?? "library";
    // ... extract all parameters
    HandleBuildCommand(input, type, output, reference, projectReference, modulePath, logger, metricsFormat, metricsOutput);
});
```

---

### 2. `HandleBuildCommand` - Single-File Compilation

**Lines 302-315**

The simplest workflow: compile a single `.spy` file to a `.dll` or `.exe`.

```csharp
static void HandleBuildCommand(FileInfo inputFile, string outputType, FileInfo? output, ...)
{
    ValidateInputFile(inputFile);
    CompileToBinary(inputFile, outputType, output, references, projectReferences, modulePaths, logger, ...);
}
```

**Flow:**
1. Validate the input file exists and has `.spy` extension (warning only)
2. Delegate to `CompileToBinary` which runs the full pipeline

---

### 3. `CompileToBinary` - The Core Compilation Pipeline

**Lines 863-992**

This is where the **magic happens**. It orchestrates the entire compilation from source to executable assembly:

#### Step 1: Read Source File
```csharp
var source = File.ReadAllText(inputFile.FullName);
```

#### Step 2: Create Compiler with Options
```csharp
var compilerOptions = new CompilerOptions
{
    References = references,      // .NET assembly references (-r)
    ModulePaths = modulePaths      // Sharpy module search paths (-m)
};
var compiler = new Sharpy.Compiler.Compiler(compilerOptions, logger);
```

#### Step 3: Compile to C#
```csharp
var result = compiler.Compile(source, inputFile.FullName);
if (!result.Success)
{
    // Display errors and exit with code 1
    Console.Error.WriteLine("Compilation failed:");
    foreach (var error in result.Errors)
        Console.Error.WriteLine($"  {error}");
    Environment.Exit(1);
}
```

The `Compiler.Compile()` method internally runs:
- Lexical analysis (tokenization)
- Parsing (AST construction)
- Semantic analysis (name resolution, type checking)
- Code generation (Roslyn emission to C#)

#### Step 4: Configure Output Path
```csharp
var inputFileName = Path.GetFileNameWithoutExtension(inputFile.Name);
var assemblyName = output != null
    ? Path.GetFileNameWithoutExtension(output.Name)
    : inputFileName;

var extension = outputType.ToLowerInvariant() == "exe" ? ".exe" : ".dll";
var finalOutputPath = output != null
    ? output.FullName
    : Path.Combine(outputDir, assemblyName + extension);
```

**Important:** If no `--output` is specified, defaults to `{input_name}.dll` in the current directory.

#### Step 5: Create Synthetic Project Config
```csharp
var projectConfig = new SingleFileProjectConfig(
    projectFilePath: inputFile.FullName,
    projectDirectory: Path.GetDirectoryName(inputFile.FullName) ?? Directory.GetCurrentDirectory(),
    rootNamespace: inputFileName,
    assemblyName: assemblyName,
    outputType: outputType,
    targetFramework: "net8.0",
    configuration: "Debug",
    sourceFiles: new List<string> { inputFile.FullName },
    references: references.ToList(),
    modulePaths: modulePaths.ToList(),
    outputAssemblyPath: finalOutputPath
);
```

This allows single-file compilation to reuse the same `AssemblyCompiler` infrastructure as multi-file projects.

#### Step 6: Compile C# to .NET Assembly
```csharp
var csharpSources = new Dictionary<string, string>
{
    { Path.ChangeExtension(inputFile.FullName, ".cs"), result.GeneratedCSharpCode! }
};

var assemblyCompiler = new AssemblyCompiler(logger);
var assemblyResult = assemblyCompiler.CompileToAssembly(csharpSources, projectConfig);
```

The `AssemblyCompiler` uses the Roslyn `CSharpCompilation` API to:
- Parse the generated C# code
- Add references to required assemblies (including `Sharpy.Core.dll`)
- Emit the final `.dll` or `.exe` binary

#### Step 7: Display Results
```csharp
if (assemblyResult.Warnings.Any())
{
    Console.WriteLine("Warnings:");
    foreach (var warning in assemblyResult.Warnings)
        Console.WriteLine($"  {warning}");
}

Console.WriteLine($"Successfully compiled to: {assemblyResult.OutputAssemblyPath}");
OutputMetrics(assemblyResult.Metrics, metricsFormat, metricsOutput);
```

---

### 4. `HandleRunCommand` - Compile and Execute

**Lines 317-431**

This command provides a **rapid iteration workflow**: compile to a temporary executable and run it immediately.

#### Key Differences from `build`:

1. **Temporary Output Handling:**
   ```csharp
   if (outputPath == null)
   {
       var tempDir = Path.GetTempPath();
       var inputFileName = Path.GetFileNameWithoutExtension(inputFile.Name);
       tempBaseName = $"{inputFileName}_{Guid.NewGuid():N}";
       outputPath = Path.Combine(tempDir, tempBaseName + ".exe");
       isTempOutput = true;
   }
   ```

2. **Always Compiles to Executable:**
   ```csharp
   CompileToBinary(inputFile, "exe", new FileInfo(outputPath), ...);
   ```

3. **Copies `Sharpy.Core.dll` to Output Directory:**
   ```csharp
   var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;
   var sharpyCorePath = sharpyCoreAssembly.Location;
   var outputDir = Path.GetDirectoryName(outputPath)!;
   var sharpyCoreDestPath = Path.Combine(outputDir, "Sharpy.Core.dll");
   File.Copy(sharpyCorePath, sharpyCoreDestPath, overwrite: true);
   ```

   **Why?** The compiled executable depends on `Sharpy.Core.dll` at runtime. For temporary builds in `/tmp`, we can't rely on the assembly being in the same directory as the CLI tool.

   **TODO (line 356-358):** Replace this manual copy with a self-contained publish mode that bundles the .NET runtime and all dependencies.

4. **Executes via `dotnet` Process:**
   ```csharp
   var startInfo = new System.Diagnostics.ProcessStartInfo
   {
       FileName = "dotnet",
       ArgumentList = { outputPath },  // Pass the .exe as first argument
       UseShellExecute = false
   };

   // Add user-provided arguments
   foreach (var arg in args)
       startInfo.ArgumentList.Add(arg);

   var process = System.Diagnostics.Process.Start(startInfo);
   process.WaitForExit();
   Environment.Exit(process.ExitCode);  // Propagate exit code
   ```

5. **Cleanup Temporary Files:**
   ```csharp
   if (isTempOutput)
   {
       try
       {
           File.Delete(outputPath);
           File.Delete(Path.Combine(basePath, tempBaseName + ".runtimeconfig.json"));
           File.Delete(Path.Combine(basePath, tempBaseName + ".deps.json"));
           File.Delete(Path.Combine(basePath, tempBaseName + ".pdb"));
           File.Delete(sharpyCoreDestPath);
       }
       catch { /* Ignore cleanup errors */ }
   }
   ```

   This happens both on success (lines 384-399) and on error (lines 406-428).

---

### 5. `HandleProjectCommand` - Multi-File Compilation

**Lines 433-464**

Handles `.spyproj` files that define multi-file Sharpy projects with dependencies, module paths, and configuration.

#### Project File Auto-Discovery:
```csharp
if (resolvedProjectFile == null)
{
    // Auto-discover .spyproj file in current directory
    var currentDir = Directory.GetCurrentDirectory();
    var discoveredPath = ProjectFileParser.FindProjectFile(currentDir);

    if (discoveredPath == null)
    {
        Console.Error.WriteLine("Error: No .spyproj file found in current directory.");
        Environment.Exit(1);
    }

    resolvedProjectFile = new FileInfo(discoveredPath);
    Console.WriteLine($"Building project: {Path.GetFileName(discoveredPath)}");
}
```

This allows the common workflow: `cd MyProject && sharpyc project`

#### Delegates to `CompileProject`:
```csharp
CompileProject(resolvedProjectFile, configuration, clean, emitCsTo, logger, logLevel, metricsFormat, metricsOutput);
```

---

### 6. `CompileProject` - Full Project Build

**Lines 667-759**

The most complex build workflow, supporting multiple source files, configurations, and incremental builds.

#### Step 1: Load Project Configuration
```csharp
var projectConfig = ProjectFileParser.Load(projectFile.FullName, configuration);
```

The `ProjectFileParser` reads the XML `.spyproj` file and extracts:
- `RootNamespace`, `AssemblyName`, `OutputType`
- List of source files (e.g., `<Compile Include="src/**/*.spy" />`)
- References to .NET assemblies
- Module search paths
- Configuration-specific settings (Debug vs Release)

#### Step 2: Handle Clean Flag
```csharp
if (clean)
{
    CleanProject(projectConfig);  // Deletes bin/ and obj/ directories
}
```

#### Step 3: Create Compiler and Compile
```csharp
var compilerOptions = new CompilerOptions
{
    References = projectConfig.References.ToArray(),
    ModulePaths = projectConfig.ModulePaths.ToArray()
};

var compiler = new Sharpy.Compiler.Compiler(compilerOptions, logger);
var result = compiler.CompileProject(projectConfig);
```

The `Compiler.CompileProject()` method:
1. Compiles each `.spy` file to C# in dependency order
2. Resolves `import` statements across modules
3. Generates a single .NET assembly containing all modules

#### Step 4: Save Generated C# (Optional)
```csharp
if (emitCsTo != null && result.GeneratedCSharpFiles.Any())
{
    SaveGeneratedCSharp(emitCsTo, result.GeneratedCSharpFiles);
}
```

This is useful for debugging code generation issues.

#### Step 5: Display Results
```csharp
if (!result.Success)
{
    Console.Error.WriteLine("Build FAILED.");
    foreach (var error in result.Errors)
        Console.Error.WriteLine($"  {error}");
    Environment.Exit(1);
}

Console.WriteLine("Build succeeded.");
Console.WriteLine($"Output: {result.OutputAssemblyPath}");
OutputProjectMetrics(result.Metrics, metricsFormat, metricsOutput);
```

---

### 7. `EmitTokens` - Lexer Debugging

**Lines 480-512**

Outputs the raw token stream from the lexer, useful for debugging parsing issues.

#### Example Output:
```
Tokens for test.spy:
================================================================================
   0: DEF                @ L1:C1
   1: IDENTIFIER         @ L1:C5 = 'greet'
   2: LEFT_PAREN         @ L1:C10
   3: IDENTIFIER         @ L1:C11 = 'name'
   4: COLON              @ L1:C15
   5: IDENTIFIER         @ L1:C17 = 'str'
   ...
================================================================================
Total tokens: 15
```

#### Implementation:
```csharp
var source = File.ReadAllText(inputFile.FullName);
var lexer = new Lexer(source, logger);
var tokens = lexer.TokenizeAll();

for (int i = 0; i < tokens.Count; i++)
{
    var token = tokens[i];
    var value = string.IsNullOrEmpty(token.Value) ? "" : $" = '{token.Value}'";
    Console.WriteLine($"{i,4}: {token.Type,-20} @ L{token.Line}:C{token.Column}{value}");
}
```

**Error Handling:** Catches `LexerError` exceptions and displays the line/column information:
```csharp
catch (LexerError ex)
{
    Console.Error.WriteLine($"Lexer error at line {ex.Line}, column {ex.Column}:");
    Console.Error.WriteLine($"  {ex.Message}");
    Environment.Exit(1);
}
```

---

### 8. `EmitAst` - Parser Debugging

**Lines 514-550**

Outputs the abstract syntax tree (AST) after parsing, showing the hierarchical structure of the program.

#### Example Output:
```
AST for test.spy:
================================================================================
Module
  FunctionDef(name='greet', params=[Parameter(name='name', type='str')], return_type='None')
    ExpressionStatement
      Call(function='print', args=[BinaryOp(left='Hello, ', op='+', right=name)])
================================================================================
```

#### Implementation:
```csharp
var source = File.ReadAllText(inputFile.FullName);
var lexer = new Lexer(source, logger);
var tokens = lexer.TokenizeAll();
var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
var module = parser.ParseModule();

var dumper = new AstDumper();
var ast = dumper.Dump(module);
Console.Write(ast);
```

The `AstDumper` class (in `Sharpy.Compiler.Parser`) recursively walks the AST and formats it as indented text.

---

### 9. `EmitCSharp` - Code Generation Debugging

**Lines 552-646**

The most complex of the `emit` subcommands. This runs the **full compilation pipeline** (lexer, parser, semantic analysis, code generation) but stops before creating a binary.

#### Why This Is Critical for Development:

When debugging compiler issues, you often need to see:
1. What C# code is being generated?
2. Is the problem in semantic analysis or code generation?
3. How are Sharpy constructs being translated to C#?

#### Full Pipeline Execution:

**Lexing and Parsing:**
```csharp
var source = File.ReadAllText(inputFile.FullName);
var lexer = new Lexer(source, logger);
var tokens = lexer.TokenizeAll();
var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
var module = parser.ParseModule();
```

**Semantic Analysis - Name Resolution:**
```csharp
var builtins = new BuiltinRegistry();
var symbolTable = new SymbolTable(builtins);
var semanticInfo = new SemanticInfo();

var nameResolver = new NameResolver(symbolTable, logger);
nameResolver.ResolveDeclarations(module);
nameResolver.ResolveInheritance();

if (nameResolver.Errors.Any())
{
    Console.Error.WriteLine("Name resolution errors:");
    foreach (var error in nameResolver.Errors)
        Console.Error.WriteLine($"  {error.Message}");
    Environment.Exit(1);
}
```

The `NameResolver` walks the AST and:
- Registers type aliases, classes, functions, variables in the symbol table
- Resolves class inheritance hierarchies
- Detects duplicate declarations

**Semantic Analysis - Type Checking:**
```csharp
var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger);
typeChecker.CheckModule(module, computeCodeGenInfo: true);

if (typeChecker.Errors.Any())
{
    Console.Error.WriteLine("Type checking errors:");
    foreach (var error in typeChecker.Errors)
        Console.Error.WriteLine($"  {error.Message}");
    Environment.Exit(1);
}
```

The `TypeChecker`:
- Resolves all type references
- Checks assignment compatibility
- Validates function calls and overload resolution
- Computes code generation metadata (stored in `SemanticInfo`)

**Code Generation:**
```csharp
var context = new CodeGenContext(symbolTable, builtins)
{
    SourceFilePath = inputFile.FullName,
    IsEntryPoint = true,  // Single-file emit is treated as entry point
    Logger = logger
};

var emitter = new RoslynEmitter(context);
var compilationUnit = emitter.GenerateCompilationUnit(module);
var csharpCode = compilationUnit.ToFullString();
```

**Important Detail (line 603):** `IsEntryPoint = true` ensures the generated C# includes a `Main` method. This matches the behavior of `build` and `run` commands.

**Output:**
```csharp
FileInfo outputFile;
if (output != null)
{
    outputFile = output;
}
else
{
    // Default: replace .spy extension with .cs
    var outputPath = Path.ChangeExtension(inputFile.FullName, ".cs");
    outputFile = new FileInfo(outputPath);
}

File.WriteAllText(outputFile.FullName, csharpCode);
Console.WriteLine($"Generated C# code written to: {outputFile.FullName}");
```

---

### 10. `CreateLogger` - Logging Configuration

**Lines 217-232**

A factory method that creates the appropriate logger based on CLI flags:

```csharp
static ICompilerLogger CreateLogger(CompilerLogLevel logLevel, FileInfo? logFile)
{
    if (logLevel == CompilerLogLevel.None)
    {
        return NullLogger.Instance;  // No-op logger (default)
    }
    else if (logFile != null)
    {
        var stream = new StreamWriter(logFile.FullName, append: false);
        return new ConsoleCompilerLogger(logLevel, stream, stream);
    }
    else
    {
        return new ConsoleCompilerLogger(logLevel);  // Logs to stderr
    }
}
```

**Design Pattern:** Strategy pattern - the compiler doesn't know or care which logger implementation it's using. This allows easy testing with mock loggers.

---

### 11. `OutputMetrics` - Performance Tracking

**Lines 234-266**

Outputs compilation performance metrics in either text or JSON format:

```csharp
static void OutputMetrics(CompilationMetrics? metrics, string? metricsFormat, FileInfo? metricsOutput)
{
    if (metrics == null || metricsFormat == null)
        return;

    var format = metricsFormat.ToLowerInvariant();
    if (format != "text" && format != "json")
    {
        Console.Error.WriteLine($"Invalid metrics format: {metricsFormat}. Use 'text' or 'json'.");
        return;
    }

    var output = format == "json" ? metrics.FormatAsJson() : metrics.FormatAsText();

    if (metricsOutput != null)
    {
        File.WriteAllText(metricsOutput.FullName, output);
        Console.WriteLine($"Metrics written to: {metricsOutput.FullName}");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine(output);
    }
}
```

**Use Case:** Tracking compiler performance over time, identifying bottlenecks in large projects.

**Example Text Output:**
```
=== Compilation Metrics ===
Lexing:          42ms
Parsing:         156ms
Semantic:        234ms
Code Gen:        89ms
Assembly:        1,234ms
Total:           1,755ms
```

**JSON Output:** Structured data suitable for automated analysis or CI integration.

---

### 12. Cache Management Commands

#### `ClearCache` (lines 648-665)
```csharp
static void ClearCache(string? cacheDir)
{
    var cache = new OverloadIndexCache(cacheDir);
    cache.ClearAll();
    Console.WriteLine("Overload discovery cache cleared successfully.");
}
```

#### `ShowCacheInfo` (lines 761-780)
```csharp
static void ShowCacheInfo(string? cacheDir)
{
    var cache = new OverloadIndexCache(cacheDir);
    var info = cache.GetInfo();

    Console.WriteLine("Overload Discovery Cache Information:");
    Console.WriteLine($"Cache Directory: {info.CacheDirectory}");
    Console.WriteLine($"Cached Assemblies: {info.CachedAssemblies}");
    Console.WriteLine($"Total Size: {FormatBytes(info.TotalSizeBytes)}");
}
```

**What Is the Overload Index Cache?**

The Sharpy compiler needs to resolve .NET method overloads when calling standard library functions. Reflecting over all methods in an assembly (like `System.dll`) is expensive. The cache stores pre-computed overload metadata to speed up subsequent compilations.

**Example Usage:**
```bash
sharpyc cache info                    # Show cache stats
sharpyc cache clear                   # Delete all cached data
sharpyc cache clear --cache-dir /tmp  # Use custom cache location
```

---

## Dependencies

### Internal Dependencies (Sharpy.Compiler Namespace)

| Namespace | Components Used | Purpose |
|-----------|----------------|---------|
| `Sharpy.Compiler` | `Compiler`, `CompilerOptions`, `ProjectConfig`, `ProjectFileParser` | Core compilation logic |
| `Sharpy.Compiler.Lexer` | `Lexer`, `LexerError` | Tokenization |
| `Sharpy.Compiler.Parser` | `Parser`, `ParserError`, `AstDumper` | Parsing and AST |
| `Sharpy.Compiler.Semantic` | `NameResolver`, `TypeChecker`, `TypeResolver`, `BuiltinRegistry`, `SymbolTable`, `SemanticInfo` | Semantic analysis |
| `Sharpy.Compiler.CodeGen` | `RoslynEmitter`, `CodeGenContext`, `AssemblyCompiler` | C# generation and compilation |
| `Sharpy.Compiler.Logging` | `ICompilerLogger`, `ConsoleCompilerLogger`, `NullLogger`, `CompilerLogLevel` | Logging infrastructure |
| `Sharpy.Compiler.Discovery.Caching` | `OverloadIndexCache` | Performance optimization |
| `Sharpy.Compiler.Diagnostics` | `CompilationMetrics`, `ProjectCompilationMetrics` | Performance tracking |

### External Dependencies

| Package | Purpose |
|---------|---------|
| `System.CommandLine` | Modern CLI argument parsing with subcommands, options, and help generation |
| `System.Diagnostics` | Process launching for `run` command |
| `System.IO` | File I/O operations |

### Runtime Dependency

| Assembly | Purpose |
|----------|---------|
| `Sharpy.Core.dll` | Standard library implementation (lists, strings, math, etc.) - must be deployed with compiled executables |

---

## Patterns and Design Decisions

### 1. Command Pattern via `System.CommandLine`

Each CLI command is configured declaratively with its arguments, options, and action handler. This provides:
- Automatic help text generation (`sharpyc --help`, `sharpyc build --help`)
- Type-safe argument parsing
- Consistent error handling for invalid inputs

### 2. Error Exit Codes

All error paths call `Environment.Exit(1)`, ensuring shell scripts can detect failures:

```bash
if ! sharpyc build main.spy; then
    echo "Compilation failed!"
    exit 1
fi
```

### 3. Consistent Error Reporting Format

All compiler errors follow the same pattern:

```csharp
Console.Error.WriteLine($"<Phase> error at line {ex.Line}, column {ex.Column}:");
Console.Error.WriteLine($"  {ex.Message}");
Environment.Exit(1);
```

This makes errors machine-parseable for IDE integration.

### 4. Separation of Concerns

- **Program.cs** handles CLI parsing and user interaction
- **Sharpy.Compiler** handles the actual compilation logic
- This allows the compiler to be used as a library (e.g., for an IDE plugin) without CLI dependencies

### 5. Dependency Injection for Loggers

Loggers are created once in each command handler and passed down through the compiler. This avoids global state and makes testing easier.

### 6. Temporary File Cleanup

The `run` command carefully manages temporary files with try/catch blocks in two places:
1. After successful execution (lines 384-399)
2. In the exception handler (lines 406-428)

This ensures no temp file leaks even if the compiled program crashes.

---

## Debugging Tips

### 1. Use `emit` Commands to Inspect Compilation Stages

When a bug occurs, narrow down which phase is failing:

```bash
sharpyc emit tokens file.spy   # Does lexing work?
sharpyc emit ast file.spy      # Does parsing work?
sharpyc emit csharp file.spy   # Does semantic analysis work?
sharpyc build file.spy         # Does C# compilation work?
```

### 2. Enable Debug Logging

```bash
sharpyc build file.spy --log-level Debug --log-file compiler.log
```

This outputs verbose information about:
- Symbol table construction
- Type resolution decisions
- Code generation choices

### 3. Inspect Generated C# Code

When you suspect a code generation bug:

```bash
sharpyc emit csharp file.spy -o generated.cs
cat generated.cs  # Review the actual C# code
```

Compare this with what you expect based on the Sharpy source.

### 4. Use `--metrics-format` for Performance Issues

```bash
sharpyc project MyApp.spyproj --metrics-format text
```

This will show which compilation phase is slow (lexing, parsing, semantic analysis, code generation, or .NET compilation).

### 5. Check the Overload Cache

If you're seeing unexpected overload resolution errors:

```bash
sharpyc cache info    # See what's cached
sharpyc cache clear   # Force re-indexing
```

### 6. Validate Project File Structure

For multi-file compilation issues:

```bash
# Try building each file individually first
sharpyc build src/module1.spy
sharpyc build src/module2.spy

# Then try the full project
sharpyc project MyApp.spyproj --log-level Debug
```

### 7. Debugging `run` Command Issues

The `run` command has multiple failure points:

1. **Compilation fails:** Check with `build` command first
2. **Runtime assembly not found:** Verify `Sharpy.Core.dll` is copied (lines 350-354)
3. **Runtime crash:** Check that the `dotnet` command is in PATH
4. **Wrong exit code:** The process exit code is propagated (line 401)

Add `--log-level Debug` to see what's happening:

```bash
sharpyc run script.spy --log-level Debug
```

---

## Contribution Guidelines

### When to Modify This File

**Add a new CLI command when:**
- You need to expose new compiler functionality (e.g., a `lint` command)
- You want to add a new diagnostic tool (e.g., `emit cfg` for control flow graphs)

**Add a new global option when:**
- The option applies to all commands (like `--log-level`)
- Avoid command-specific global options

**Modify error handling when:**
- You need more specific error messages
- You want to add structured error output (e.g., JSON for IDE integration)

### Coding Conventions

1. **Keep command handlers simple:** Delegate complex logic to helper methods
2. **Use consistent parameter ordering:** Input file first, output second, then options
3. **Validate inputs early:** Call `ValidateInputFile` before doing expensive work
4. **Propagate exit codes:** Always call `Environment.Exit(1)` on errors
5. **Clean up resources:** Use try/catch for temporary file cleanup

### Testing This File

While `Program.cs` doesn't have unit tests (it's an entry point), test it manually:

```bash
# Test each command
sharpyc build test.spy
sharpyc run test.spy
sharpyc project test.spyproj
sharpyc emit tokens test.spy
sharpyc emit ast test.spy
sharpyc emit csharp test.spy
sharpyc cache info
sharpyc cache clear

# Test error cases
sharpyc build nonexistent.spy          # File not found
sharpyc build invalid.spy              # Compilation errors
sharpyc run crashing.spy               # Runtime errors

# Test options
sharpyc build test.spy --type exe -o app.exe
sharpyc run test.spy --args arg1 arg2
sharpyc build test.spy --log-level Debug --log-file log.txt
sharpyc project test.spyproj --configuration Release --clean
```

### Adding a New `emit` Subcommand

**Example: Add `emit bytecode` to show IL disassembly**

1. Create the command object:
   ```csharp
   var emitBytecodeCommand = new Command("bytecode", "Emit IL bytecode disassembly");
   var emitBytecodeInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
   emitBytecodeCommand.Arguments.Add(emitBytecodeInputArg);
   ```

2. Add the action handler:
   ```csharp
   emitBytecodeCommand.SetAction((parseResult) =>
   {
       var input = parseResult.GetValue(emitBytecodeInputArg)!;
       var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
       var logFile = parseResult.GetValue(logFileOption);
       var logger = CreateLogger(logLevel, logFile);
       EmitBytecode(input, logger);  // Implement this method
   });
   ```

3. Add to the `emit` command:
   ```csharp
   emitCommand.Subcommands.Add(emitBytecodeCommand);
   ```

4. Implement the handler method:
   ```csharp
   static void EmitBytecode(FileInfo inputFile, ICompilerLogger logger)
   {
       // 1. Compile to binary in memory
       // 2. Load the assembly with reflection
       // 3. Disassemble the IL
       // 4. Output to console
   }
   ```

### Adding a New Top-Level Command

Follow the pattern of existing commands (build, run, project):

1. Define arguments and options
2. Create the command with `new Command(...)`
3. Set the action handler with `SetAction(...)`
4. Add to root command: `rootCommand.Subcommands.Add(myCommand)`
5. Implement the handler method (e.g., `HandleMyCommand`)

---

## Cross-References

### Related Files in `Sharpy.Cli`

This is the only source file in the `Sharpy.Cli` project. The CLI is intentionally kept minimal.

### Upstream Dependencies (Compiler Components)

For deep dives into how compilation actually works, see:

- **Lexical Analysis:** `docs/implementation_walkthrough/src/Sharpy.Compiler/Lexer/Lexer.md`
- **Parsing:** `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/Parser.md`
- **Semantic Analysis:**
  - `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/NameResolver.md`
  - `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/TypeChecker.md`
- **Code Generation:** `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/RoslynEmitter.md`
- **Project System:** `docs/implementation_walkthrough/src/Sharpy.Compiler/ProjectFileParser.md`

### Downstream Consumers

- **End Users:** Developers writing `.spy` code interact with this CLI
- **Build Tools:** CI/CD scripts and build systems invoke `sharpyc` commands
- **IDEs:** Future IDE plugins might invoke `sharpyc emit csharp` for diagnostics

---

## Summary: The Big Picture

`Program.cs` is the **public face** of the Sharpy compiler. It:

1. **Provides a clean interface** to all compiler functionality via subcommands
2. **Orchestrates the compilation pipeline** from source to binary
3. **Supports multiple workflows**: single-file builds, multi-file projects, compile-and-run, diagnostics
4. **Handles all user-facing concerns**: error reporting, logging, metrics, help text
5. **Delegates the hard work** to specialized components in `Sharpy.Compiler`

When in doubt, the workflow is:
- **User runs** `sharpyc <command> <args>`
- **Program.cs parses** arguments and creates appropriate compiler objects
- **Compiler components** do the actual work (lex, parse, analyze, generate)
- **Program.cs reports** results back to the user

This separation of concerns makes the compiler maintainable, testable, and embeddable in other tools (like IDEs) that don't need the CLI layer.
