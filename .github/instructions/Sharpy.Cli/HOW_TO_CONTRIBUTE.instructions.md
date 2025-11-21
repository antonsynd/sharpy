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
dotnet run --project src/Sharpy.Cli -- build snippets/hello.spy
dotnet run --project src/Sharpy.Cli -- project samples/calculator_app/calculator.spyproj
dotnet run --project src/Sharpy.Cli -- --help
```

Note that the compiler can also be installed via Chiri, which is a custom build system wrapper. The most important command is `chiri pkg -- release`, which builds, tests, and installs the Sharpy compiler to `~/.local/bin/sharpyc`.

```bash
# Assuming `chiri` is in PATH, usually at ~/.chiri/bin/chiri
chiri pkg -- release

# Install only (skip tests/build)
chiri pkg -- install
```

### Run the Sharpy Compiler installed via Chiri

```bash
sharpyc <args>

# Examples:
sharpyc build snippets/hello.spy
sharpyc --help
```

### Common Commands

**Compile a project:**
```bash
dotnet run --project src/Sharpy.Cli -- project samples/calculator_app/calculator.spyproj
```

**Compile in Release mode:**
```bash
dotnet run --project src/Sharpy.Cli -- --configuration Release
```

## How to Test

There are no automated tests for Sharpy.Cli. Testing is done manually:

1. **Test single-file compilation:**
   ```bash
   dotnet run --project src/Sharpy.Cli -- build test.spy
   ```

2. **Test project compilation:**
   ```bash
   dotnet run --project src/Sharpy.Cli -- project samples/calculator_app/calculator.spyproj
   ```

3. **Test error handling:**
   - Try compiling a non-existent file
   - Try compiling a file with syntax errors
   - Try using conflicting options

## Important Things to Note

### Architecture
- Uses **System.CommandLine** for argument parsing
- Delegates compilation to `Sharpy.Compiler.Compiler` and `Sharpy.Compiler.AssemblyCompiler`
- Handles both single-file and multi-file (project) compilation
- Returns appropriate exit codes (0 = success, non-zero = error)

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

def main() -> None:
    greet("World")
```

Run:
```bash
dotnet run --project src/Sharpy.Cli -- build test.spy
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
dotnet run --project src/Sharpy.Cli -- project myapp.spyproj
```

## Related Documentation

- **Main README:** `README.md` (repository root)
- **CLI README:** `src/Sharpy.Cli/README.md`
- **Compiler Guide:** `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`
