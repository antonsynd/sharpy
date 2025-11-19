# sharpyc - Sharpy Compiler CLI

The `sharpyc` command-line tool is the official compiler for the Sharpy programming language.

## Building

```bash
cd src/Sharpy.Cli
dotnet build
```

## Usage

### Single File Compilation

```bash
sharpyc <input.spy> [options]
```

### Project Compilation

```bash
sharpyc [--project <path.spyproj>] [options]
```

If `--project` is not specified, the compiler will automatically search for a `.spyproj` file in the current directory.

### Options

#### Emit Modes

- `--emit-tokens` - Emit lexer tokens for the input file (single-file mode only)
  - Displays all tokens produced by the lexer with their types, positions, and values
  - Useful for debugging lexical analysis

- `--emit-ast` - Emit the abstract syntax tree (NOT IMPLEMENTED YET)
  - Will display the parsed AST structure

#### Project Options

- `--project <path>` - Compile a Sharpy project file
  - Auto-discovers `.spyproj` file if not specified
  - Compiles all source files in the project

- `-c, --configuration <Debug|Release>` - Build configuration (default: Debug)
  - Determines output directory: `bin/Debug` or `bin/Release`

#### Compilation Options (NOT IMPLEMENTED YET)

- `-o, --output <path>` - Specify output file path (single-file mode)
  - Default output name is based on input file name

- `--module-path <path>` - Add module search path(s) (single-file mode)
  - Can be specified multiple times for multiple paths
  - Example: `--module-path ./libs --module-path ../common`

## Examples

### Emit Tokens (Single-File Mode)

```bash
# Display tokens for a Sharpy file
dotnet run --project src/Sharpy.Cli -- hello.spy --emit-tokens
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

### Project Compilation

```bash
# Compile a project in Debug mode (default)
sharpyc

# Compile a specific project file
sharpyc --project myapp.spyproj

# Compile in Release mode
sharpyc --configuration Release
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
    net9.0/
      MyApp.exe
      MyApp.dll
      MyApp.pdb
  Release/
    net9.0/
      MyApp.exe
      MyApp.dll
```

### Future Examples (Not Implemented Yet)

```bash
# Compile single file with custom output
sharpyc myfile.spy -o output.dll

# Compile with module search paths
sharpyc main.spy --module-path ./libs --module-path ../common

# Emit AST
sharpyc myfile.spy --emit-ast
```

## Implementation Status

### ✅ Implemented
- Command-line argument parsing (using System.CommandLine)
- Input file validation
- `--emit-tokens` mode with formatted token output
- Error handling for lexer errors
- **Project compilation** with `.spyproj` files
  - Glob pattern support for source files
  - Multi-file compilation with shared symbol table
  - Cross-module imports and resolution
  - Namespace generation from project structure
  - Debug/Release configuration support
  - Assembly generation (.exe/.dll)
- **`__init__.spy` support** for package-level exports
- **Improved error messages** with file context

### 🚧 Not Implemented Yet
- `--emit-ast` - AST emission
- `--module-path` option for single-file compilation
- `--output` option for single-file compilation
- `--clean` command to delete bin/ directories
- `--emit-cs-to` option to save intermediate C# code

## Development

### Running with dotnet run

```bash
cd src/Sharpy.Cli
dotnet run -- <args>
```

Example:
```bash
dotnet run -- ../../test_sharpyc.spy --emit-tokens
```

### Testing

Create a simple test file:
```sharpy
# test.spy
def greet(name: str) -> None:
    message: str = f"Hello, {name}!"
    print(message)

x: auto = 42
greet("World")
```

Run the compiler:
```bash
dotnet run --project src/Sharpy.Cli -- test.spy --emit-tokens
```

## Error Handling

The tool provides clear error messages for common issues:

- Missing input file
- Invalid Sharpy syntax (lexer errors)
- Conflicting options
- Unimplemented features

All errors are written to stderr and return non-zero exit codes.
