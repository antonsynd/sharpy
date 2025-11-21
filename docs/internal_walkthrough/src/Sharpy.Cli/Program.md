# Walkthrough: Program.cs

**Source File**: `src/Sharpy.Cli/Program.cs`

---

## Overview

`Program.cs` is the entry point for the Sharpy compiler command-line interface (`sharpyc`). This file is responsible for:

- **Command-line parsing** using System.CommandLine library
- **Routing user commands** to appropriate compiler functionality
- **Error handling** and user-friendly output
- **Orchestrating the compilation pipeline** for both single files and projects

Think of this as the "receptionist" of the Sharpy compiler - it takes user requests, validates them, and delegates to the appropriate backend components (Lexer, Parser, Semantic Analyzer, Code Generator, Assembly Compiler).

### Role in the Project

```
User Command (sharpyc build hello.spy)
         ↓
    Program.cs (this file)
         ↓
    Command Routing & Validation
         ↓
    Compiler Pipeline Components
         ↓
    Generated .NET Assembly
```

---

## Class/Type Structure

### Main Class: `Program`

This is a static class with a single public entry point (`Main`) and several private helper methods.

**Key Inner Type:**
- `SingleFileProjectConfig` - A wrapper class that adapts single-file compilation to the project compilation infrastructure

---

## Key Functions/Methods

### 1. `Main(string[] args)` - Entry Point

**Purpose**: The application entry point that sets up all CLI commands and handles argument parsing.

**What it does**:
1. Creates a root command for `sharpyc`
2. Defines global options (logging, metrics)
3. Defines five major subcommands: `build`, `run`, `project`, `emit`, `cache`
4. Parses arguments and invokes the appropriate handler

**Command Structure**:
```
sharpyc
├── build       - Compile single file to exe/dll
├── run         - Compile and execute single file
├── project     - Build from .spyproj file
├── emit        - Debug commands
│   ├── tokens   - Show lexer output
│   ├── ast      - Show parser output
│   └── csharp   - Show generated C# code
└── cache       - Manage overload discovery cache
    ├── clear    - Clear cache
    └── info     - Show cache stats
```

**Important Pattern**: Uses System.CommandLine's fluent API for command definition:
```csharp
var buildCommand = new Command("build", "Compile a Sharpy source file");
buildCommand.Arguments.Add(buildInputArg);
buildCommand.Options.Add(buildTypeOpt);
buildCommand.SetAction((parseResult) => { /* handler */ });
```

---

### 2. `CreateLogger(CompilerLogLevel, FileInfo?)` - Logger Factory

**Purpose**: Creates appropriate logger based on user's logging preferences.

**Return Values**:
- `NullLogger.Instance` - When logging is disabled (default)
- `ConsoleCompilerLogger` - When logging to console or file

**Key Insight**: Sharpy supports redirecting compiler logs to a file, useful for debugging complex compilation issues.

---

### 3. `HandleBuildCommand(...)` - Single File Compilation

**Purpose**: Compiles a single `.spy` file to a binary (exe or dll).

**Flow**:
1. Validate input file exists
2. Call `CompileToBinary()` with appropriate parameters

**Parameters**:
- `inputFile` - The `.spy` source file
- `outputType` - "exe" or "library" (default: library)
- `output` - Optional output path
- `references` - .NET assemblies to reference
- `modulePaths` - Paths to search for Sharpy modules

**Example**:
```bash
sharpyc build hello.spy --type exe -o hello.exe
```

---

### 4. `HandleRunCommand(...)` - Compile and Execute

**Purpose**: Compiles a Sharpy file and immediately runs it.

**Key Implementation Details**:
1. **Temporary files**: If no output specified, creates temp files with unique GUID names
2. **Sharpy.Core.dll copying**: Manually copies the standard library DLL to output directory (required for execution)
3. **Cleanup**: Removes temporary files after execution (even on errors)
4. **Process execution**: Uses `dotnet <assembly>` to run the compiled executable

**Important Quirk**: The TODO comment at line 356 highlights a future improvement - use self-contained publishing instead of manual DLL copying.

**Cleanup Files**:
- `.exe` - Main executable
- `.runtimeconfig.json` - .NET runtime config
- `.deps.json` - Dependency manifest
- `.pdb` - Debug symbols
- `Sharpy.Core.dll` - Standard library

**Exit Code**: Propagates the exit code from the executed program to the shell.

---

### 5. `HandleProjectCommand(...)` - Project Compilation

**Purpose**: Compiles a multi-file Sharpy project from a `.spyproj` file.

**Auto-discovery**: If no project file specified, searches current directory for `.spyproj` files using `ProjectFileParser.FindProjectFile()`.

**Flow**:
1. Resolve project file path (explicit or auto-discovered)
2. Display build information
3. Delegate to `CompileProject()`

**Example**:
```bash
# Auto-discover project in current directory
sharpyc project

# Explicit project file
sharpyc project samples/calculator_app/calculator.spyproj --configuration Release
```

---

### 6. `EmitTokens(FileInfo, ICompilerLogger)` - Debug: Lexer Output

**Purpose**: Developer debugging tool that shows tokenization output.

**Output Format**:
```
Tokens for hello.spy:
================================================================================
   0: Keyword              @ L1:C1 = 'def'
   1: Identifier           @ L1:C5 = 'greet'
   2: LeftParen            @ L1:C10
   ...
================================================================================
Total tokens: 42
```

**Use Case**: When debugging lexer issues or verifying token positions.

---

### 7. `EmitAst(FileInfo, ICompilerLogger)` - Debug: Parser Output

**Purpose**: Shows the Abstract Syntax Tree generated by the parser.

**Implementation**: Uses `AstDumper` to format the tree structure.

**Use Case**: Verifying parser correctly interprets syntax, debugging parser bugs.

---

### 8. `EmitCSharp(FileInfo, FileInfo?, ICompilerLogger)` - Debug: Code Generation Output

**Purpose**: Shows the C# code generated from Sharpy source.

**Flow**:
1. Lex and parse the Sharpy source
2. Create code generation context (symbol table, builtins)
3. Use `RoslynEmitter` to generate C# code
4. Write to file (default: replace `.spy` with `.cs`)

**Use Case**: Understanding how Sharpy constructs map to C#, debugging code generation issues.

**Example**:
```bash
# Generate hello.cs from hello.spy
sharpyc emit csharp hello.spy

# Custom output path
sharpyc emit csharp hello.spy -o generated/Hello.cs
```

---

### 9. `CompileProject(...)` - Project Compilation Implementation

**Purpose**: The core project compilation logic.

**Flow**:
1. Load project configuration from `.spyproj`
2. Handle `--clean` if requested (delete bin/ and obj/)
3. Display build information
4. Create compiler with options
5. Compile all source files
6. Optionally save generated C# (if `--emit-cs-to` specified)
7. Display warnings and errors
8. Output metrics if requested

**Error Handling**: Catches specific exceptions (`FileNotFoundException`, `InvalidDataException`) and provides contextual error messages.

**Debug Mode**: Shows stack traces when `--log-level Debug` is set.

---

### 10. `CompileToBinary(...)` - Single File to Assembly

**Purpose**: Compiles a single Sharpy file to a .NET assembly.

**Two-Stage Process**:
1. **Sharpy → C#**: Use `Compiler.Compile()` to generate C# code
2. **C# → Assembly**: Use `AssemblyCompiler.CompileToAssembly()` to create DLL/EXE

**Important Pattern**: Creates a `SingleFileProjectConfig` to adapt single-file compilation to the project infrastructure:

```csharp
var projectConfig = new SingleFileProjectConfig(
    projectFilePath: inputFile.FullName,
    rootNamespace: inputFileName,
    assemblyName: assemblyName,
    outputType: outputType,
    // ... other parameters
);
```

**Output Path Logic**:
- If user specifies `--output`: Use that path
- Otherwise: `<input_filename>.dll` or `.exe` in current directory

---

### 11. `SingleFileProjectConfig` - Adapter Class

**Purpose**: Allows single-file compilation to reuse project compilation infrastructure.

**Design Pattern**: Adapter pattern - wraps a manually constructed project config.

**Why it exists**: `AssemblyCompiler` expects a `ProjectConfig`, but single-file compilation doesn't have a `.spyproj`. This class bridges the gap.

**Override**: The `OutputAssemblyPath` property is overridden to use the user-specified or computed output path.

---

### 12. Cache Management Methods

#### `ClearCache(string?)`
Deletes the overload discovery cache (improves performance for .NET interop).

#### `ShowCacheInfo(string?)`
Displays cache statistics: directory, number of assemblies, total size.

**Cache Purpose**: The overload index cache stores metadata about .NET methods to speed up overload resolution during compilation.

---

### 13. Utility Methods

#### `ValidateInputFile(FileInfo)`
- Checks file exists (exits with error code 1 if not)
- Warns if extension is not `.spy`

#### `OutputMetrics(...)` / `OutputProjectMetrics(...)`
- Formats compilation metrics as text or JSON
- Writes to file or console
- Useful for performance analysis

#### `FormatBytes(long)`
- Converts byte count to human-readable format (B, KB, MB, GB)

#### `CleanProject(ProjectConfig)`
- Deletes `bin/` and `obj/` directories
- Used with `--clean` flag

#### `SaveGeneratedCSharp(...)`
- Saves generated C# files when `--emit-cs-to` is used
- Creates output directory if needed

---

## Dependencies

### External Libraries
- **System.CommandLine** - Modern command-line parsing framework
- **Sharpy.Compiler** - Core compiler components
  - `Lexer` - Tokenization
  - `Parser` - AST generation
  - `RoslynEmitter` - C# code generation
  - `AssemblyCompiler` - Assembly compilation
  - `ProjectFileParser` - .spyproj parsing
- **Sharpy.Core** - Standard library (referenced to copy DLL)

### Key Compiler Components Used
```csharp
using Sharpy.Compiler;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Diagnostics;
```

---

## Patterns and Design Decisions

### 1. Command Pattern
Each command (`build`, `run`, `project`) has a dedicated handler method that encapsulates its logic.

### 2. Global Options
Logging and metrics options are defined globally and inherited by all subcommands:
```csharp
rootCommand.Options.Add(logLevelOption);
rootCommand.Options.Add(logFileOption);
```

### 3. Error Handling Strategy
- **Lexer/Parser errors**: Catch specific exceptions, display with line/column info
- **General errors**: Catch `Exception`, display message, exit with code 1
- **Warnings**: Display but don't fail the build
- **Stack traces**: Only show in debug mode

### 4. Separation of Concerns
- `Program.cs` handles **CLI concerns** (parsing, validation, output)
- Delegates **compilation logic** to `Sharpy.Compiler`
- Delegates **assembly generation** to `AssemblyCompiler`

### 5. Fail-Fast Principle
Uses `Environment.Exit(1)` immediately on errors rather than throwing exceptions. This ensures clean termination with proper exit codes.

### 6. Resource Cleanup
The `run` command demonstrates proper cleanup in both success and error paths:
```csharp
try {
    // Compile and run
}
catch (Exception) {
    // Clean up temp files
    throw;
}
finally {
    // Additional cleanup if needed
}
```

---

## Debugging Tips

### 1. Tracing Command Execution
Add a console write at the start of handler methods:
```csharp
static void HandleBuildCommand(...)
{
    Console.WriteLine($"[DEBUG] Building: {inputFile.FullName}");
    // ... rest of method
}
```

### 2. Inspecting Generated C#
Use the `emit csharp` command to see what C# code is generated:
```bash
sharpyc emit csharp problematic.spy -o debug.cs
```

### 3. Verbose Logging
Enable debug logging to see compiler internals:
```bash
sharpyc build hello.spy --log-level Debug --log-file compiler.log
```

### 4. Metrics Analysis
Use metrics to identify performance bottlenecks:
```bash
sharpyc build hello.spy --metrics-format json --metrics-output metrics.json
```

### 5. Common Error Points
- **File not found**: Check `ValidateInputFile()` - is the path correct?
- **Compilation fails**: Check error output - is it lexer, parser, semantic, or code gen?
- **Assembly compilation fails**: Look at Roslyn errors - usually C# syntax issues in generated code
- **Run command fails**: Check if Sharpy.Core.dll is being copied correctly (line 350-354)

### 6. Debugging the Run Command
The run command has complex cleanup logic. If debugging issues:
1. Comment out cleanup code temporarily to inspect generated files
2. Check the temp directory: `Path.GetTempPath()`
3. Verify Sharpy.Core.dll is in the same directory as the executable

---

## Contribution Guidelines

### Types of Changes to This File

#### ✅ Good Changes
1. **Adding new commands** - e.g., `test`, `format`, `check`
2. **Adding new options** - e.g., `--verbose`, `--no-warnings`
3. **Improving error messages** - Make them more actionable
4. **Adding metrics output** - More compilation statistics
5. **Better auto-discovery** - e.g., find nearest .spyproj in parent directories
6. **Self-contained publishing** - Fix the TODO at line 356

#### ⚠️ Requires Careful Consideration
1. **Changing command names** - Breaking change for users
2. **Changing option names** - Breaking change for scripts
3. **Changing exit codes** - May break CI/CD pipelines
4. **Removing commands** - Deprecate first

#### ❌ Avoid
1. **Adding compilation logic here** - Belongs in `Sharpy.Compiler`
2. **Complex business logic** - Keep this file focused on CLI concerns
3. **Direct file I/O for compilation** - Use compiler abstractions

### Testing Your Changes

Since there are no automated tests for Sharpy.Cli, test manually:

1. **Test all affected commands**:
   ```bash
   # Single file
   sharpyc build test.spy
   sharpyc run test.spy
   
   # Project
   sharpyc project samples/calculator_app/calculator.spyproj
   
   # Debug commands
   sharpyc emit tokens test.spy
   sharpyc emit ast test.spy
   sharpyc emit csharp test.spy
   ```

2. **Test error cases**:
   ```bash
   # Non-existent file
   sharpyc build nonexistent.spy
   
   # Syntax error
   sharpyc build broken.spy
   ```

3. **Test edge cases**:
   ```bash
   # No project file
   sharpyc project
   
   # Custom output paths
   sharpyc build test.spy -o custom/path/output.dll
   ```

### Code Style Guidelines

**Follow existing patterns**:
- Static methods for all handlers
- Consistent error output to `Console.Error`
- Use `Environment.Exit(1)` for failures
- Always show user-friendly messages

**Error Message Format**:
```csharp
Console.Error.WriteLine($"Error: {specific_problem}");
Console.Error.WriteLine($"  {helpful_context}");
Environment.Exit(1);
```

### Future Improvements (Potential Contributions)

1. **Self-contained publishing** (line 356 TODO)
   - Bundle .NET runtime with executable
   - Eliminate need to manually copy Sharpy.Core.dll

2. **Watch mode**:
   ```bash
   sharpyc watch hello.spy  # Recompile on changes
   ```

3. **Interactive mode**:
   ```bash
   sharpyc repl  # Sharpy REPL
   ```

4. **Better project discovery**:
   - Search parent directories for .spyproj
   - Support multiple projects in solution

5. **Package management**:
   ```bash
   sharpyc package install some-sharpy-lib
   ```

6. **Parallel compilation**:
   - Option to compile project files in parallel
   - `--parallel` flag

7. **Incremental compilation**:
   - Only recompile changed files
   - `--incremental` flag

8. **Better metrics**:
   - Compilation time breakdown by phase
   - Memory usage statistics
   - Cache hit rates

---

## Quick Reference

### Command Cheat Sheet

```bash
# Build commands
sharpyc build <file.spy>                      # Compile to library
sharpyc build <file.spy> --type exe           # Compile to exe
sharpyc build <file.spy> -o custom.dll        # Custom output

# Run command
sharpyc run <file.spy>                        # Compile and execute
sharpyc run <file.spy> --args arg1 arg2       # Pass arguments

# Project commands
sharpyc project                               # Auto-discover and build
sharpyc project <file.spyproj>                # Build specific project
sharpyc project --configuration Release       # Release build
sharpyc project --clean                       # Clean before build

# Debug commands
sharpyc emit tokens <file.spy>                # Show tokens
sharpyc emit ast <file.spy>                   # Show AST
sharpyc emit csharp <file.spy>                # Show generated C#

# Cache commands
sharpyc cache info                            # Show cache stats
sharpyc cache clear                           # Clear cache

# Global options (any command)
--log-level Debug                             # Enable logging
--log-file compiler.log                       # Log to file
--metrics-format json                         # Output metrics
```

### Method Call Flow Examples

**Single File Compilation**:
```
Main() 
  → HandleBuildCommand()
    → CompileToBinary()
      → Compiler.Compile() [Sharpy → C#]
      → AssemblyCompiler.CompileToAssembly() [C# → DLL]
```

**Project Compilation**:
```
Main()
  → HandleProjectCommand()
    → CompileProject()
      → Compiler.CompileProject()
        → [Multiple files compiled and linked]
```

**Run Command**:
```
Main()
  → HandleRunCommand()
    → CompileToBinary() [compile to temp exe]
    → File.Copy() [copy Sharpy.Core.dll]
    → Process.Start() [execute via dotnet]
    → Cleanup temp files
```

---

## Summary

`Program.cs` is the user-facing interface to the Sharpy compiler. It's a well-structured CLI application that:
- Uses modern command-line parsing (System.CommandLine)
- Provides multiple compilation modes (single file, project, run)
- Offers debugging tools (emit tokens/AST/C#)
- Handles errors gracefully with actionable messages
- Delegates complex logic to appropriate compiler components

When contributing, focus on improving user experience, adding helpful diagnostics, and maintaining the clean separation between CLI concerns and compilation logic.
