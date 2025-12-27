# Walkthrough: Program.cs

**Source File**: `src/Sharpy.Cli/Program.cs`

---

## Overview

`Program.cs` is the entry point for the Sharpy compiler command-line interface (CLI). It implements a modern CLI application using the **System.CommandLine** library, providing developers with a git-like command structure for compiling Sharpy source files to .NET assemblies.

**Core Responsibilities:**
- Define CLI commands and options (build, run, project, emit, cache)
- Parse command-line arguments
- Orchestrate the compilation pipeline (Lexer → Parser → Semantic Analysis → Code Generation → Assembly Compilation)
- Handle errors gracefully with user-friendly messages
- Support debugging features (token/AST emission, C# code generation)
- Manage compilation metrics and logging

**Execution Flow:**
```
User Command → System.CommandLine Parser → Command Handler → Compiler API → Assembly Output
```

---

## Class/Type Structure

### Main Class: `Program`

A single static class containing the CLI implementation. No instance state is maintained—all operations are stateless and driven by command-line arguments.

### Nested Class: `SingleFileProjectConfig`

**Location:** Lines 961-994

```csharp
private class SingleFileProjectConfig : ProjectConfig
```

**Purpose:** Adapts the project-oriented `ProjectConfig` class for single-file compilation scenarios. This is necessary because the compiler's `AssemblyCompiler` expects a `ProjectConfig`, but single-file builds (via `sharpyc build file.spy`) don't have a project file.

**Key Override:**
- `OutputAssemblyPath` property is overridden to provide a custom output path instead of calculating it from project structure.

---

## Key Functions/Methods

### 1. `Main(string[] args)` - Entry Point

**Lines:** 15-215

**Purpose:** Constructs the command tree using System.CommandLine and parses user input.

**Command Structure:**
```
sharpyc
├── build       - Compile a single .spy file
├── run         - Compile and execute a .spy file
├── project     - Build a .spyproj project
├── emit        - Emit intermediate representations
│   ├── tokens  - Show tokenized output
│   ├── ast     - Show abstract syntax tree
│   └── csharp  - Show generated C# code
└── cache       - Manage overload discovery cache
    ├── clear   - Clear the cache
    └── info    - Display cache information
```

**Global Options:**
- `--log-level` - Control compiler verbosity (None, Error, Warning, Info, Debug)
- `--log-file` - Redirect logs to a file
- `--metrics-format` - Output compilation metrics (text or json)
- `--metrics-output` - Write metrics to a file

**Design Pattern:** Command pattern via lambda-based action handlers. Each command uses `SetAction()` to define its behavior inline.

**Return Value:** Exit code from the invoked command (0 for success, 1 for errors).

---

### 2. `CreateLogger(CompilerLogLevel, FileInfo?)` - Logger Factory

**Lines:** 217-232

**Purpose:** Creates the appropriate logger implementation based on user preferences.

**Logic:**
- **No logging:** Returns `NullLogger.Instance` (singleton pattern)
- **File logging:** Creates `ConsoleCompilerLogger` with `StreamWriter` redirected to the file
- **Console logging:** Creates standard `ConsoleCompilerLogger`

**Note:** Despite the name, `ConsoleCompilerLogger` can write to any `TextWriter`, making it reusable for both console and file output.

---

### 3. `HandleBuildCommand()` - Single File Compilation

**Lines:** 302-315

**Purpose:** Compiles a single `.spy` file to a .NET assembly (DLL or EXE).

**Parameters:**
- `inputFile` - Source file path
- `outputType` - "exe" or "library" (defaults to "library")
- `output` - Optional output path (auto-generated if not provided)
- `references` - External .NET assemblies to reference
- `projectReferences` - .NET project references
- `modulePaths` - Paths to search for Sharpy modules
- `logger` - Compiler logger instance
- `metricsFormat/metricsOutput` - Metrics configuration

**Delegates to:** `CompileToBinary()` after validating the input file exists.

**Example Usage:**
```bash
sharpyc build hello.spy --type exe --output ./bin/hello.exe
```

---

### 4. `HandleRunCommand()` - Compile and Execute

**Lines:** 317-431

**Purpose:** Compiles a Sharpy source file to an executable and immediately runs it.

**Key Behavior:**
1. **Temporary Output:** If no output path is specified, creates a temp file with a GUID-based name
2. **Compilation:** Delegates to `CompileToBinary()` with `outputType="exe"`
3. **Runtime Dependency:** Copies `Sharpy.Core.dll` to the output directory so the executable can find it
4. **Execution:** Launches the compiled EXE via `dotnet <path>`
5. **Cleanup:** Deletes temporary files after execution (`.exe`, `.runtimeconfig.json`, `.deps.json`, `.pdb`, `Sharpy.Core.dll`)

**TODO Comment (Line 356):** The current approach manually copies dependencies. A future improvement would be to use .NET's self-contained publish mode to bundle the runtime and all dependencies into a standalone executable.

**Error Handling:** Cleanup happens even if execution fails (try/catch in lines 404-430).

**Example Usage:**
```bash
sharpyc run script.spy --args "arg1" "arg2"
```

---

### 5. `HandleProjectCommand()` - Multi-File Project Compilation

**Lines:** 433-464

**Purpose:** Builds a `.spyproj` project containing multiple source files.

**Auto-Discovery:** If no project file is specified, searches the current directory for a `.spyproj` file using `ProjectFileParser.FindProjectFile()`.

**Delegates to:** `CompileProject()` with the resolved project file path.

**Example Usage:**
```bash
sharpyc project                           # Auto-discover
sharpyc project samples/calculator.spyproj  # Explicit path
```

---

### 6. `EmitTokens()` - Lexer Debugging

**Lines:** 480-512

**Purpose:** Displays the token stream produced by the lexer for debugging lexical analysis issues.

**Output Format:**
```
Tokens for hello.spy:
================================================================================
   0: Identifier        @ L1:C1 = 'print'
   1: LeftParen         @ L1:C6
   2: StringLiteral     @ L1:C7 = 'Hello, World!'
   3: RightParen        @ L1:C22
   4: Newline           @ L1:C23
   5: EndOfFile         @ L2:C1
================================================================================
Total tokens: 6
```

**Use Case:** When a file fails to parse, run this to verify tokens are being recognized correctly.

**Example Usage:**
```bash
sharpyc emit tokens hello.spy
```

---

### 7. `EmitAst()` - Parser Debugging

**Lines:** 514-550

**Purpose:** Displays the abstract syntax tree (AST) generated by the parser.

**Key Component:** Uses `AstDumper` to pretty-print the AST hierarchy.

**Output Example:**
```
AST for hello.spy:
================================================================================
Module
  Body:
    - ExprStmt
      - Call
        Func: Name(print)
        Args: [StringLiteral("Hello, World!")]
================================================================================
```

**Use Case:** When semantic analysis fails, check the AST to verify the parser correctly understood the code structure.

**Example Usage:**
```bash
sharpyc emit ast hello.spy
```

---

### 8. `EmitCSharp()` - Code Generation Debugging

**Lines:** 552-610

**Purpose:** Generates and saves the C# code that would be produced by the Sharpy compiler without compiling it to an assembly.

**Pipeline:**
1. Lex and parse the Sharpy source
2. Create `SymbolTable` and `CodeGenContext`
3. Use `RoslynEmitter` to generate a Roslyn `CompilationUnit`
4. Convert to C# source string via `ToFullString()`
5. Write to file (defaults to `<input>.cs`)

**Limitations:** Only performs lexing, parsing, and code generation—skips semantic analysis. This means the generated C# might not be valid if there are type errors.

**Use Case:** Debugging code generation issues or understanding how Sharpy constructs map to C#.

**Example Usage:**
```bash
sharpyc emit csharp hello.spy --output hello_generated.cs
```

---

### 9. `CompileToBinary()` - Core Compilation Logic

**Lines:** 827-956

**Purpose:** The workhorse method that performs full Sharpy → C# → .NET Assembly compilation.

**Compilation Pipeline:**

```
┌─────────────┐
│ Read Source │
└──────┬──────┘
       │
       ▼
┌─────────────────┐
│ Create Compiler │ (with options: references, module paths)
└──────┬──────────┘
       │
       ▼
┌──────────────────────┐
│ Compile to C# Code   │ (Compiler.Compile)
└──────┬───────────────┘
       │
       ├─ Lexer.TokenizeAll()
       ├─ Parser.ParseModule()
       ├─ Semantic Analysis
       └─ RoslynEmitter.Generate()
       │
       ▼
┌─────────────────────────┐
│ Create ProjectConfig    │ (SingleFileProjectConfig wrapper)
└──────┬──────────────────┘
       │
       ▼
┌──────────────────────────┐
│ AssemblyCompiler.Compile │ (C# → IL)
└──────┬───────────────────┘
       │
       ▼
┌─────────────────┐
│ Write Assembly  │ (.dll or .exe)
└─────────────────┘
```

**Output Path Resolution (Lines 866-878):**
- If `output` is provided, use it directly
- Otherwise, use current directory with input filename + appropriate extension

**SingleFileProjectConfig Usage (Lines 888-900):**
Creates a minimal project configuration to satisfy `AssemblyCompiler`'s expectations. Key properties:
- `assemblyName` - Derived from input filename
- `targetFramework` - Hardcoded to "net8.0" (could be made configurable)
- `outputAssemblyPath` - Explicit output path

**Error Handling:**
- Lexer errors → Exit code 1, display line/column
- Parser errors → Exit code 1, display line/column  
- Semantic errors → Exit code 1, display compiler errors
- Assembly compilation errors → Exit code 1, display Roslyn diagnostics

---

### 10. `CompileProject()` - Project Build Orchestration

**Lines:** 631-723

**Purpose:** Builds a multi-file Sharpy project from a `.spyproj` file.

**Key Steps:**

1. **Load Project:** Parse `.spyproj` XML using `ProjectFileParser.Load()`
2. **Clean (Optional):** Delete `bin/` and `obj/` directories if `--clean` flag is set
3. **Create Compiler:** Initialize with project's references and module paths
4. **Compile:** Call `Compiler.CompileProject()` which handles multi-file compilation
5. **Save C# (Optional):** If `--emit-cs-to` is specified, save generated C# files
6. **Display Results:** Show warnings, errors, and output path

**Project Config Properties Displayed:**
```
Project: MyApp
Configuration: Debug
Output: exe
Source files: 5
```

**Metrics Support:** Outputs `ProjectCompilationMetrics` which includes per-file timing and totals.

**Example Usage:**
```bash
sharpyc project --configuration Release --emit-cs-to ./generated
```

---

### 11. Cache Management Commands

#### `ClearCache(string?)` - Lines 612-629

**Purpose:** Clears the overload discovery cache to force reindexing of .NET assemblies.

**Use Case:** When .NET assemblies change (e.g., package updates), stale cache entries can cause incorrect overload resolution.

**Implementation:** Uses `OverloadIndexCache.ClearAll()`

#### `ShowCacheInfo(string?)` - Lines 725-744

**Purpose:** Displays cache statistics (location, number of assemblies, total size).

**Helper:** `FormatBytes()` (lines 746-759) converts byte counts to human-readable format (B, KB, MB, GB).

---

### 12. Utility Methods

#### `ValidateInputFile()` - Lines 466-478

**Purpose:** Ensures the input file exists before compilation.

**Behavior:**
- Non-existent file → Error message + exit code 1
- Missing `.spy` extension → Warning (but continues)

#### `OutputMetrics()` - Lines 234-266

**Purpose:** Formats and outputs compilation metrics in text or JSON format.

**Output Targets:**
- File → Write to specified path
- Console → Print to stdout

#### `OutputProjectMetrics()` - Lines 268-300

**Purpose:** Similar to `OutputMetrics()` but for `ProjectCompilationMetrics` (includes per-file breakdowns).

#### `CleanProject()` - Lines 761-795

**Purpose:** Deletes `bin/` and `obj/` directories for a clean build.

**Error Handling:** Warnings if deletion fails, but doesn't abort the build.

#### `SaveGeneratedCSharp()` - Lines 797-825

**Purpose:** Saves generated C# code to a directory when `--emit-cs-to` is used.

**Filename Strategy:** Uses the module path's base name + `.cs` extension.

---

## Dependencies

### External Libraries

**System.CommandLine:**
- Modern CLI parsing library (successor to `System.CommandLine.DragonFruit`)
- Provides `Command`, `Option`, `Argument` types
- Handles help text generation automatically
- Validates argument types and counts

### Compiler Pipeline

```csharp
using Sharpy.Compiler;              // Main Compiler class
using Sharpy.Compiler.Lexer;        // Lexer, Token types
using Sharpy.Compiler.Parser;       // Parser, AST nodes
using Sharpy.Compiler.Semantic;     // SymbolTable, BuiltinRegistry
using Sharpy.Compiler.CodeGen;      // RoslynEmitter, CodeGenContext
using Sharpy.Compiler.Discovery.Caching;  // OverloadIndexCache
using Sharpy.Compiler.Diagnostics;  // CompilationMetrics
using Sharpy.Compiler.Logging;      // ICompilerLogger, ConsoleCompilerLogger
```

### Standard Library

- `Sharpy.Core.Exports` - Used to locate `Sharpy.Core.dll` for the `run` command (line 350)

---

## Patterns and Design Decisions

### 1. Command Pattern via Lambdas

**Location:** Throughout `Main()` method

Instead of separate handler classes, each command's logic is defined inline using lambda expressions passed to `SetAction()`:

```csharp
buildCommand.SetAction((parseResult) =>
{
    // Extract values from parseResult
    var input = parseResult.GetValue(buildInputArg)!;
    var type = parseResult.GetValue(buildTypeOpt) ?? "library";
    
    // Invoke handler
    HandleBuildCommand(input, type, ...);
});
```

**Rationale:** 
- Keeps related code together
- Avoids ceremony of separate handler classes
- Still delegates to focused methods for complex logic

### 2. Error Handling Strategy

**Exit Codes:**
- **0** - Success
- **1** - Any error (lexer, parser, semantic, assembly, IO)

**Error Display Pattern:**
```csharp
catch (LexerError ex)
{
    Console.Error.WriteLine($"Lexer error at line {ex.Line}, column {ex.Column}:");
    Console.Error.WriteLine($"  {ex.Message}");
    Environment.Exit(1);
}
```

**Characteristics:**
- Specific error types caught separately
- Location information included when available
- Errors written to `stderr`, success messages to `stdout`
- Immediate exit on error (fail-fast)

### 3. Global Options Pattern

**Lines:** 20-28

Global options are defined once and attached to the root command. They're then retrieved in each subcommand's handler:

```csharp
var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
```

**Benefits:**
- DRY - Options defined once
- Consistent behavior across all commands
- Easy to add new global options

### 4. Separation of Concerns

The CLI doesn't contain compilation logic—it orchestrates components:

```
Program.cs (Orchestration)
    ↓
Compiler (High-level API)
    ↓
Lexer → Parser → Semantic → CodeGen
    ↓
AssemblyCompiler (Roslyn)
```

**Rationale:** Program.cs focuses on user interaction, not compilation details.

### 5. Temporary File Management

**Run Command Pattern (Lines 335-342, 384-398, 406-428):**

```csharp
var isTempOutput = false;
if (outputPath == null)
{
    var tempBaseName = $"{inputFileName}_{Guid.NewGuid():N}";
    outputPath = Path.Combine(Path.GetTempPath(), tempBaseName + ".exe");
    isTempOutput = true;
}

// ... compilation ...

// Cleanup in finally/catch
if (isTempOutput)
{
    File.Delete(outputPath);
    File.Delete(runtimeConfigPath);
    // etc.
}
```

**Characteristics:**
- GUID prevents collisions with concurrent runs
- Cleanup happens even on errors
- Multiple related files tracked (`.exe`, `.pdb`, `.deps.json`, etc.)

---

## Debugging Tips

### 1. Tracing Compilation Pipeline

**Add `--log-level Debug` to any command:**
```bash
sharpyc build hello.spy --log-level Debug
```

This enables verbose logging from the compiler, showing:
- Which phases are executing
- Symbol resolution details
- Type inference steps
- Code generation decisions

### 2. Isolating Failures

**Use `emit` commands to isolate pipeline stages:**

```bash
# Does it lex correctly?
sharpyc emit tokens problematic.spy

# Does it parse correctly?
sharpyc emit ast problematic.spy

# Does it generate valid C#?
sharpyc emit csharp problematic.spy -o output.cs
```

### 3. Inspecting Generated C#

**Two approaches:**

1. **Via emit command:**
   ```bash
   sharpyc emit csharp file.spy -o output.cs
   ```

2. **Via project compilation:**
   ```bash
   sharpyc project --emit-cs-to ./generated
   ```

**Then:** Open the generated C# in an IDE to check for errors or unexpected patterns.

### 4. Metrics Analysis

**Enable metrics to identify performance bottlenecks:**

```bash
sharpyc build file.spy --metrics-format text
```

**Output includes:**
- Lexing time
- Parsing time
- Semantic analysis time
- Code generation time
- Assembly compilation time

### 5. Cache Issues

**If overload resolution behaves incorrectly:**

```bash
# Check cache state
sharpyc cache info

# Clear and rebuild
sharpyc cache clear
sharpyc build file.spy
```

### 6. Debugging Run Command

**The run command does several things:**
1. Compiles to EXE
2. Copies dependencies
3. Executes via `dotnet`

**To debug execution failures:**

```bash
# Compile without running
sharpyc build script.spy --type exe --output ./debug.exe

# Copy Sharpy.Core.dll manually
cp /path/to/Sharpy.Core.dll .

# Run directly
dotnet ./debug.exe
```

### 7. Stack Traces on Unexpected Errors

**Notice the pattern (lines 716-721):**

```csharp
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    if (logLevel == CompilerLogLevel.Debug)
    {
        Console.Error.WriteLine(ex.StackTrace);
    }
    Environment.Exit(1);
}
```

**Stack traces only shown with `--log-level Debug` to avoid overwhelming users.**

---

## Contribution Guidelines

### Adding a New Command

**Template:**

```csharp
// 1. Define the command
var myCommand = new Command("mycommand", "Description of what it does");

// 2. Add arguments
var inputArg = new Argument<FileInfo>("input") { Description = "Input file" };
myCommand.Arguments.Add(inputArg);

// 3. Add options
var myOption = new Option<string?>("--my-option") { Description = "An option" };
myOption.Aliases.Add("-m");
myCommand.Options.Add(myOption);

// 4. Set the action handler
myCommand.SetAction((parseResult) =>
{
    var input = parseResult.GetValue(inputArg)!;
    var option = parseResult.GetValue(myOption);
    var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
    var logFile = parseResult.GetValue(logFileOption);
    
    var logger = CreateLogger(logLevel, logFile);
    HandleMyCommand(input, option, logger);
});

// 5. Add to root command
rootCommand.Subcommands.Add(myCommand);
```

**Don't forget:**
- Support global options (log level, log file, metrics)
- Write errors to `stderr`, success messages to `stdout`
- Use exit code 1 for failures, 0 for success
- Add input validation

### Adding a New Emit Subcommand

**Example: Emit semantic info**

```csharp
var emitSemanticCommand = new Command("semantic", "Emit semantic analysis results");
var emitSemanticInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
emitSemanticCommand.Arguments.Add(emitSemanticInputArg);
emitSemanticCommand.SetAction((parseResult) =>
{
    var input = parseResult.GetValue(emitSemanticInputArg)!;
    var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
    var logFile = parseResult.GetValue(logFileOption);
    var logger = CreateLogger(logLevel, logFile);
    EmitSemantic(input, logger);
});

emitCommand.Subcommands.Add(emitSemanticCommand);
```

### Adding a New Global Option

**Example: Add `--optimize` flag**

```csharp
// 1. Define near other global options (line ~20)
var optimizeOption = new Option<bool>("--optimize") { Description = "Enable optimizations" };
rootCommand.Options.Add(optimizeOption);

// 2. Retrieve in command handlers
var optimize = parseResult.GetValue(optimizeOption);

// 3. Pass to compiler
var compilerOptions = new CompilerOptions
{
    References = references,
    ModulePaths = modulePaths,
    Optimize = optimize  // Add to CompilerOptions class
};
```

### Adding Metrics to a New Command

**Follow the pattern from existing commands:**

```csharp
var myCommand = new Command("mycommand", "...");
// ... setup ...

myCommand.SetAction((parseResult) =>
{
    // ... other values ...
    var metricsFormat = parseResult.GetValue(metricsFormatOption);
    var metricsOutput = parseResult.GetValue(metricsOutputOption);
    
    HandleMyCommand(..., metricsFormat, metricsOutput);
});

// In handler:
void HandleMyCommand(..., string? metricsFormat, FileInfo? metricsOutput)
{
    var result = DoWork();
    OutputMetrics(result.Metrics, metricsFormat, metricsOutput);
}
```

### Improving Error Messages

**Current pattern:**
```csharp
Console.Error.WriteLine($"Error: {ex.Message}");
```

**Better pattern:**
```csharp
Console.Error.WriteLine($"Error: Failed to compile '{inputFile.Name}'");
Console.Error.WriteLine($"  {ex.Message}");
if (logLevel == CompilerLogLevel.Debug)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Stack trace:");
    Console.Error.WriteLine(ex.StackTrace);
}
```

**Guidelines:**
- Include context (what operation failed)
- Add indentation for secondary information
- Show stack traces only in debug mode
- Use colors for different message types (requires library like `Spectre.Console`)

### Testing CLI Commands

**Manual testing checklist:**

1. **Happy path:** Does it work with valid input?
2. **Missing required args:** Does it show helpful error?
3. **Invalid option values:** Does it validate and reject?
4. **File not found:** Does it show clear error?
5. **Help text:** Does `--help` show accurate info?
6. **Global options:** Do log level/file/metrics work?
7. **Exit codes:** Does it return 0 on success, 1 on failure?

**Example test script:**
```bash
#!/bin/bash

# Test build command
sharpyc build samples/hello.spy --type exe -o test.exe
if [ $? -ne 0 ]; then echo "Build failed"; exit 1; fi

# Test run command
sharpyc run samples/hello.spy
if [ $? -ne 0 ]; then echo "Run failed"; exit 1; fi

# Test invalid file
sharpyc build nonexistent.spy 2>&1 | grep -q "does not exist"
if [ $? -ne 0 ]; then echo "Missing file error check failed"; exit 1; fi

echo "All tests passed"
```

### Performance Considerations

**Current bottlenecks:**
1. **Assembly compilation (Roslyn)** - Slowest part of pipeline
2. **Overload discovery** - Mitigated by caching
3. **Module resolution** - Could benefit from caching

**Potential improvements:**
- Parallel compilation of multiple files in projects
- Incremental compilation (only recompile changed files)
- Ahead-of-time overload indexing during package installation
- Persistent symbol table cache

**Measurement:**
- Use `--metrics-format json` for machine-readable timing data
- Add timing to new features
- Profile with `dotnet-trace` for detailed analysis

---

## Summary

`Program.cs` is a well-structured CLI application that follows modern C# conventions. Key strengths:

✅ **Clear separation of concerns** - Orchestration vs. compilation logic  
✅ **Consistent error handling** - Structured catches, exit codes, stderr usage  
✅ **Debuggability** - Emit commands for each pipeline stage  
✅ **Extensibility** - Easy to add new commands/options  
✅ **User-friendly** - Helpful error messages, auto-discovery, metrics  

**Areas for improvement:**
- Add integration tests for CLI commands
- Implement self-contained publish for `run` command
- Make target framework configurable (currently hardcoded to "net8.0")
- Add colorized output for better UX
- Support incremental/parallel compilation

**Next steps for new contributors:**
1. Build the project: `dotnet build`
2. Try commands: `sharpyc build samples/hello.spy`
3. Test emit commands to understand pipeline stages
4. Read related files: `src/Sharpy.Compiler/Compiler.cs`, `AssemblyCompiler.cs`
