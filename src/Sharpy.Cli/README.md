# sharpyc - Sharpy Compiler CLI

The `sharpyc` command-line tool is the official compiler for the Sharpy programming language.

## Building

```bash
cd src/Sharpy.Cli
dotnet build
```

## Usage

### Commands

```bash
# Compile and run immediately
sharpyc run <input.spy>

# Compile to binary/library
sharpyc build <input.spy> [options]

# Build a project
sharpyc project <path.spyproj> [options]

# Emit intermediate representations (for debugging)
sharpyc emit tokens <input.spy>   # Lexer tokens
sharpyc emit ast <input.spy>      # Abstract syntax tree
sharpyc emit csharp <input.spy>   # Generated C# code
```

### Build Options

- `-t, --type <exe|library>` - Output type (default: library)
- `-o, --output <path>` - Output file path
- `-r, --reference <assembly>` - Add .NET assembly references
- `-p, --project-reference <project>` - Add .NET project references
- `-m, --module-path <path>` - Additional module search paths

### Project Options

- `-c, --configuration <Debug|Release>` - Build configuration (default: Debug)
- `--incremental` - Enable incremental compilation (skip unchanged files)
- `--clean` - Delete bin/ and obj/ directories before building
- `--emit-cs-to <dir>` - Save generated C# code to directory, mirroring each source's path relative to the project directory (`pkg/lib.spy` → `<dir>/pkg/lib.cs`)

### Generated C# Layout

Every command that writes generated C# writes each module to a path that mirrors its source path,
so `lib.spy` and `pkg/lib.spy` land at `lib.cs` and `pkg/lib.cs`. Two modules that would map to the
same path are refused with a warning; neither overwrites the other.

- `sharpyc project app.spyproj --emit-cs-to <dir>` - under `<dir>`, relative to the project directory
- `sharpyc compile app.spyproj --emit-csharp` - beside the output assembly, relative to the project directory (`-o <file>` sets the assembly path)
- `sharpyc compile main.spy --emit-csharp` - beside the output assembly, relative to `main.spy`'s directory
- `sharpyc emit csharp main.spy --output gen/main.cs` - the entry module to the `--output` file (default: `main.cs` beside `main.spy`); each imported module under that file's directory, relative to `main.spy`'s directory

## Examples

### Run a Sharpy File

```bash
# Compile and execute immediately
dotnet run --project src/Sharpy.Cli -- run hello.spy
```

### Emit Tokens (for debugging lexer)

```bash
dotnet run --project src/Sharpy.Cli -- emit tokens hello.spy
```

Output:
```
Tokens for hello.spy:
================================================================================
   0: Def                  @ L1:C1 = 'def'
   1: Identifier           @ L1:C5 = 'greet'
   2: LeftParen            @ L1:C10 = '('
   ...
================================================================================
Total tokens: 37
```

### Emit Generated C# (for debugging codegen)

```bash
dotnet run --project src/Sharpy.Cli -- emit csharp hello.spy
```

### Project Compilation

```bash
# Compile a project in Debug mode (default)
sharpyc project calculator.spyproj

# Compile in Release mode
sharpyc project calculator.spyproj --configuration Release
```

### Project File Format

Create a `.spyproj` file to define your multi-file Sharpy project:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <PropertyGroup>
        <RootNamespace>MyApp</RootNamespace>
        <OutputType>exe</OutputType>
        <Configuration>Debug</Configuration>
    </PropertyGroup>
    <ItemGroup>
        <!-- Include specific files -->
        <SpyFile Include="src/main.spy" />
        <SpyFile Include="src/utils.spy" />

        <!-- Or use glob patterns -->
        <SpyFile Include="src/**/*.spy" />
    </ItemGroup>
</Project>
```

**Property Groups:**
- `RootNamespace` - Root namespace for generated C# code (e.g., `MyApp`)
- `OutputType` - `exe` for executable, `library` for DLL (default: `library`)
- `Configuration` - Build configuration (default: `Debug`)

**Item Groups:**
- `SpyFile Include="..."` - Source files to compile
  - Supports glob patterns: `**/*.spy`, `src/**/*.spy`, etc.
  - Uses Microsoft.Extensions.FileSystemGlobbing

**Output Structure:**
```
bin/
  Debug/
    net10.0/
      MyApp.exe
      MyApp.dll
      MyApp.pdb
  Release/
    net10.0/
      MyApp.exe
      MyApp.dll
```

## Implementation Status

### ✅ Implemented
- Command-line argument parsing (using System.CommandLine)
- `run` command - compile and execute immediately
- `build` command - compile to binary/library
- `project` command - build multi-file projects
- `emit` subcommands - tokens, ast, csharp
- `cache` command - manage overload discovery cache
- `--clean` option for project builds
- `--emit-cs-to` option to save intermediate C#
- Error handling with formatted output
- **Project compilation** with `.spyproj` files
  - Glob pattern support for source files
  - Multi-file compilation with shared symbol table
  - Cross-module imports and resolution
  - Namespace generation from project structure
  - Debug/Release configuration support
  - Assembly generation (.exe/.dll)
- **`__init__.spy` support** for package-level exports
- **Improved error messages** with file context

## Development

### Running with dotnet run

```bash
cd src/Sharpy.Cli
dotnet run -- <args>
```

Example:
```bash
dotnet run -- run hello.spy
```

### Testing

Create a simple test file:
```python
# test.spy
def greet(name: str) -> None:
    message: str = f"Hello, {name}!"
    print(message)

def main():
    greet("World")
```

Run the compiler:
```bash
dotnet run --project src/Sharpy.Cli -- run test.spy
```

Or emit tokens for debugging:
```bash
dotnet run --project src/Sharpy.Cli -- emit tokens test.spy
```

## Error Handling

The tool provides clear error messages for common issues:

- Missing input file
- Invalid Sharpy syntax (lexer errors)
- Conflicting options
- Unimplemented features

All errors are written to stderr and return non-zero exit codes.
