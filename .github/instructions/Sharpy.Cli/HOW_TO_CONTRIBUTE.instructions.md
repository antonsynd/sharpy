# Contributing to Sharpy.Cli

## Overview

**Sharpy.Cli** is the command-line interface for the Sharpy compiler (`sharpyc`). It provides the entry point for compiling Sharpy programs and projects.

**Location:** `src/Sharpy.Cli/`

## What's in This Directory

### Core Files
- **`Program.cs`** - Main entry point, command-line argument parsing using System.CommandLine
- **`Sharpy.Cli.csproj`** - Project file
- **`README.md`** - CLI documentation and usage examples

### Key Responsibilities
- Parse command-line arguments (`--emit-tokens`, `--project`, `--configuration`, etc.)
- Validate input files and options
- Invoke the compiler pipeline (lexer, parser, semantic analyzer, code generator)
- Handle errors and display user-friendly messages
- Support both single-file and project compilation modes

## How to Build

```bash
# From repository root
dotnet build src/Sharpy.Cli/Sharpy.Cli.csproj

# From Sharpy.Cli directory
cd src/Sharpy.Cli
dotnet build
```

## How to Run

```bash
# From repository root
dotnet run --project src/Sharpy.Cli -- <args>

# Examples:
dotnet run --project src/Sharpy.Cli -- snippets/hello.spy --emit-tokens
dotnet run --project src/Sharpy.Cli -- --project samples/calculator_app/calculator.spyproj
dotnet run --project src/Sharpy.Cli -- --help
```

### Common Commands

**Emit tokens for a file:**
```bash
dotnet run --project src/Sharpy.Cli -- snippets/hello.spy --emit-tokens
```

**Compile a project:**
```bash
dotnet run --project src/Sharpy.Cli -- --project samples/calculator_app/calculator.spyproj
```

**Compile in Release mode:**
```bash
dotnet run --project src/Sharpy.Cli -- --configuration Release
```

## How to Test

Currently, there are no automated tests for Sharpy.Cli. Testing is done manually:

1. **Test single-file compilation:**
   ```bash
   dotnet run --project src/Sharpy.Cli -- test.spy --emit-tokens
   ```

2. **Test project compilation:**
   ```bash
   dotnet run --project src/Sharpy.Cli -- --project samples/calculator_app/calculator.spyproj
   ```

3. **Test error handling:**
   - Try compiling a non-existent file
   - Try compiling a file with syntax errors
   - Try using conflicting options

### Adding Tests (Future)

When adding tests:
- Create a new test project `Sharpy.Cli.Tests`
- Test command-line argument parsing
- Test error handling and exit codes
- Test integration with compiler components
- **Do NOT artificially make tests pass** - fix the root cause or mark as skipped with a detailed explanation

## Important Things to Note

### Architecture
- Uses **System.CommandLine** for argument parsing
- Delegates compilation to `Sharpy.Compiler.Compiler` and `Sharpy.Compiler.AssemblyCompiler`
- Handles both single-file and multi-file (project) compilation
- Returns appropriate exit codes (0 = success, non-zero = error)

### Current Features (Implemented)
- ✅ `--emit-tokens` - Display lexer tokens
- ✅ `--project <path>` - Compile a `.spyproj` file
- ✅ `-c, --configuration <Debug|Release>` - Build configuration
- ✅ Error handling with user-friendly messages
- ✅ Auto-discovery of `.spyproj` files

### Not Yet Implemented
- ❌ `--emit-ast` - Display AST
- ❌ `--output <path>` - Custom output path (single-file mode)
- ❌ `--module-path <path>` - Module search paths (single-file mode)
- ❌ `--clean` - Delete bin/ directories
- ❌ `--emit-cs-to <path>` - Save intermediate C# code

### Error Handling
- All errors written to stderr
- Non-zero exit codes on errors
- Clear error messages for common issues:
  - Missing input file
  - Invalid Sharpy syntax
  - Conflicting options
  - Unimplemented features

## Common Development Tasks

### Adding a New Command-Line Option

1. Define the option in `Program.cs` using System.CommandLine API
2. Add handler logic to process the option
3. Update `README.md` with documentation
4. Test the option manually
5. Consider adding integration tests (future)

### Adding a New Compilation Mode

1. Add the option to `Program.cs`
2. Implement the mode in the handler
3. Delegate to appropriate compiler component
4. Add error handling
5. Document in `README.md`

### Improving Error Messages

1. Identify where the error occurs
2. Add context (file name, line number, suggestion)
3. Write to stderr with clear formatting
4. Test with various error scenarios

## Dependencies

- **Sharpy.Compiler** - Core compilation logic
- **System.CommandLine** - Command-line argument parsing
- **.NET 9.0/10.0** - Runtime

## Best Practices

### Code Quality
- Keep `Program.cs` focused on CLI concerns (parsing, validation, output)
- Delegate compilation logic to `Sharpy.Compiler`
- Provide clear, actionable error messages
- Return appropriate exit codes

### Testing
- **Test manually** after making changes
- **Test error cases** (missing files, invalid syntax, etc.)
- **Test different options** and combinations
- **Do NOT skip test failures** - fix the root cause or document why it's skipped

### Documentation
- Update `README.md` when adding features
- Include examples for new options
- Document implementation status (implemented vs. planned)

## Examples

### Example Test File

Create `test.spy`:
```python
def greet(name: str) -> None:
    message: str = f"Hello, {name}!"
    print(message)

greet("World")
```

Run:
```bash
dotnet run --project src/Sharpy.Cli -- test.spy --emit-tokens
```

### Example Project File

Create `myapp.spyproj`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <PropertyGroup>
        <RootNamespace>MyApp</RootNamespace>
        <OutputType>exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include="src/**/*.spy" />
    </ItemGroup>
</Project>
```

Run:
```bash
dotnet run --project src/Sharpy.Cli -- --project myapp.spyproj
```

## Related Documentation

- **Main README:** `README.md` (repository root)
- **CLI README:** `src/Sharpy.Cli/README.md`
- **Compiler Guide:** `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`
