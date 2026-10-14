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

- `-t, --type <exe|library>` - Output type (default: exe)
- `-o, --output <path>` - Output file path
- `-r, --reference <assembly>` - Add .NET assembly references (a path, or a file name found on a module path). A reference that resolves nowhere is an error (SPY0305). With `compile app.spyproj` and `project`, added to the project's own `<Reference>` items. Also taken by `emit diagnostics` and `emit hover`, so an entry importing a referenced library can be analysed.
- `-p, --project-reference <project>` - Refused: sharpyc does not build .NET projects; build it with `dotnet build` and pass the assembly with `-r`
- `-m, --module-path <path>` - Additional module search paths (a relative path is relative to the working directory). An absolute import is resolved below each one, on every command that takes it (`run`, `compile`, `build`, `project`, `emit csharp`, `emit diagnostics`, `emit hover`; lexing and parsing need no search path, so `emit tokens`/`ast`/`parse` do not): with `-m mods`, `from helper import f` finds `mods/helper.spy` and `import pkg.mod` finds `mods/pkg/mod.spy`. A directory that does not exist is an error. With `compile app.spyproj` and `project`, added to the project's own `<ModulePath>` items.

`compile` accepts both a `.spy` file and a `.spyproj`. Every option it accepts is either honoured, refused for that input kind, or documented in `--help` as ignored for it (`--type` for a `.spyproj`, `--incremental`/`--clean` for a `.spy` file; `--self-contained` is refused for a `.spyproj`) — its table is the `"compile"` table of `CommandOptionEffects`, the one option-to-effect registry for every command.

### Project Options

- `-c, --configuration <Debug|Release>` - Build configuration (default: Debug)
- `--incremental` - Enable incremental compilation (skip unchanged files)
- `--clean` - Delete bin/ and obj/ directories before building
- `--emit-cs-to <dir>` - Save generated C# code to directory, mirroring each source's path relative to the project directory (`pkg/lib.spy` → `<dir>/pkg/lib.cs`)
- `-r, --reference <assembly>` / `-m, --module-path <path>` - As for `compile app.spyproj`: added to the project's own `<Reference>`/`<ModulePath>` items; a reference that resolves nowhere is SPY0305 and a missing directory an error, before anything is built

### Options by Command

Every option a command accepts — its own and the global ones — is honoured, refused (with a message, before anything is read or written), or documented in `--help` as having no effect. The registry is `CommandOptionEffects`; `CliOptionEffectSweepTests` checks every cell, and `ReadmeOptionTableTests` checks this table against it.

| Command | Input | `-r` / `-m` | Deploys references | Global options without effect |
|---|---|---|---|---|
| `build` | `.spy` | yes | no — writes the assembly only | none |
| `compile` | `.spy`, `.spyproj` | yes | yes, unless `--no-deps` | none |
| `run` | `.spy` | yes | yes, beside the program it runs | none |
| `project` | `.spyproj` | yes | no — writes the assembly only | none |
| `emit tokens` | `.spy` | no — lexing needs no references | — | `--metrics-format`, `--metrics-output`, `--warn-as-error`, `--nowarn`, `--enable-feature` |
| `emit ast` | `.spy` | no — parsing needs no references | — | `--metrics-format`, `--metrics-output`, `--warn-as-error`, `--nowarn`, `--enable-feature` |
| `emit parse` | `.spy` | no — parsing needs no references | — | `--metrics-format`, `--metrics-output`, `--warn-as-error`, `--nowarn`, `--enable-feature` |
| `emit csharp` | `.spy` | yes | — | none |
| `emit diagnostics` | `.spy` | yes | — | `--metrics-format`, `--metrics-output` |
| `emit hover` | `.spy` | yes | — | `--metrics-format`, `--metrics-output` |
| `cache clear` | — | no | — | all nine |
| `cache info` | — | no | — | all nine |
| `explain` | — | no | — | all nine |
| `lsp` | — | no | — | `--log-file`, `--metrics-format`, `--metrics-output`, `--warn-as-error`, `--nowarn`, `--max-errors`, `--verbose`, `--enable-feature` |
| `repl` | — | no | — | `--metrics-format`, `--metrics-output`, `--warn-as-error`, `--nowarn`, `--max-errors` |
| `format` | `.spy` (or a directory of them) | no | — | all nine |
| `server` | — | no | — | `--metrics-format`, `--metrics-output`, `--warn-as-error`, `--nowarn`, `--max-errors`, `--enable-feature` |

A `.spyproj` given to a command that takes `.spy` source is refused with a steer to `compile`/`project`, and a `.spy` given to `project` with a steer to `build`/`run`/`compile` (any other extension is accepted: as source with a warning, or as a project file). A directory is refused on every file-taking command, naming the file it should have been given; `format` walks a directory's `.spy` files.

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

        <!-- .NET assembly references: a path, or a name with a HintPath -->
        <Reference Include="libs/Greeter.dll" />
        <Reference Include="Greeter">
            <HintPath>libs/Greeter.dll</HintPath>
        </Reference>

        <!-- Module search paths for absolute imports -->
        <ModulePath Include="mods" />
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
- `Reference Include="..."` - A .NET assembly the project imports from
  - Without a `<HintPath>` child, `Include` is the file's path: absolute, or relative to the `.spyproj`'s directory. It is not searched for on a `<ModulePath>` or `-m` directory.
  - With a non-empty `<HintPath>` child, the HintPath is the file (absolute, or relative to the `.spyproj`'s directory) and `Include` is the reference's name. A name that differs from the assembly's own is not diagnosed.
  - A reference whose file does not exist is an error (SPY0305), and nothing is compiled or written.
- `ModulePath Include="..."` - A module search path, relative to the `.spyproj`'s directory. An absolute import is resolved below it, as with `-m`. A module found there must also be a `<SpyFile>` of the project (SPY0313).

**Output Structure:**

`project` writes the assembly only, under `bin/<Configuration>/net10.0/` in the `.spyproj`'s
directory:

```
bin/
  Debug/
    net10.0/
      MyApp.exe                  # MyApp.dll for OutputType library
      MyApp.pdb                  # Debug only
      MyApp.deps.json
      MyApp.runtimeconfig.json
```

Nothing else is written beside it: not `Sharpy.Core.dll`, not the standard-library assemblies, and
not the project's `<Reference>` assemblies. `dotnet bin/Debug/net10.0/MyApp.exe` therefore fails to
load them. `build` is the same for a single file: it writes the assembly only.

`compile` and `run` deploy. Beside the assembly they copy `Sharpy.Core.dll`, the standard-library
assemblies the program uses (every default one for a `.spyproj`), and every `-r` or `<Reference>`
assembly whether the program uses it or not. Each copied assembly brings the dependencies that sit
beside it, managed and native. The output directory then runs with `dotnet`:

```bash
sharpyc compile app.spyproj -o out/MyApp.dll    # or: sharpyc compile main.spy -r libs/Greeter.dll -o out/main.dll
dotnet out/MyApp.dll
```

`compile --no-deps` skips the copy. `compile --self-contained` and `run --self-contained` publish
an executable that carries every reference. `compile` defaults to the `Release` configuration, so
without `-o` it writes `bin/Release/net10.0/` for a `.spyproj` and `bin/Release/` for a `.spy`.

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
