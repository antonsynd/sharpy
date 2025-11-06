# sharpyc - Sharpy Compiler CLI

The `sharpyc` command-line tool is the official compiler for the Sharpy programming language.

## Building

```bash
cd src/Sharpy.Cli
dotnet build
```

## Usage

```bash
sharpyc <input.spy> [options]
```

### Options

#### Emit Modes

- `--emit-tokens` - Emit lexer tokens for the input file (IMPLEMENTED)
  - Displays all tokens produced by the lexer with their types, positions, and values
  - Useful for debugging lexical analysis

- `--emit-ast` - Emit the abstract syntax tree (NOT IMPLEMENTED YET)
  - Will display the parsed AST structure

#### Compilation Options (NOT IMPLEMENTED YET)

- `-t, --output-type <library|exe>` - Specify output type (default: library)
  - `library` - Compile to a .NET DLL
  - `exe` - Compile to a .NET executable

- `-o, --output <path>` - Specify output file path
  - Default output name is based on input file name

- `-r, --reference <path>` - Add .NET DLL reference(s)
  - Can be specified multiple times for multiple references
  - Example: `-r System.Drawing.dll -r MyLib.dll`

- `-p, --project-reference <path>` - Add .NET project reference(s)
  - Can be specified multiple times for multiple references
  - Example: `-p ../MyLib/MyLib.csproj`

## Examples

### Emit Tokens (Currently Implemented)

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

### Future Examples (Not Implemented Yet)

```bash
# Compile to a library
sharpyc mylib.spy -t library -o mylib.dll

# Compile to an executable
sharpyc main.spy -t exe -o main.exe

# Compile with .NET references
sharpyc myapp.spy -r System.Drawing.dll -r ../libs/MyLib.dll

# Compile with project references
sharpyc myapp.spy -p ../MyLib/MyLib.csproj

# Emit AST
sharpyc myfile.spy --emit-ast
```

## Implementation Status

### ✅ Implemented
- Command-line argument parsing (using System.CommandLine)
- Input file validation
- `--emit-tokens` mode with formatted token output
- Error handling for lexer errors

### 🚧 Not Implemented Yet
- `--emit-ast` - AST emission
- Compilation to library/executable
- .NET reference handling (`--reference`, `--project-reference`)
- Output file generation (`--output`)
- Output type selection (`--output-type`)

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
