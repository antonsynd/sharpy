---
applyTo: "src/Sharpy.Cli/**"
---
# Sharpy.Cli

Command-line interface for the Sharpy compiler. Location: `src/Sharpy.Cli/`

## Key Files

- `Program.cs` — Entry point using `System.CommandLine`
- Delegates to `Sharpy.Compiler.Compiler` (single-file) and `AssemblyCompiler` (projects)

## Commands

```bash
# Run a single file
dotnet run --project src/Sharpy.Cli -- run hello.spy

# Build a single file
dotnet run --project src/Sharpy.Cli -- build hello.spy

# Build a project
# dotnet run --project src/Sharpy.Cli -- project samples/calculator_app/calculator.spyproj

# Debug: inspect generated C#
dotnet run --project src/Sharpy.Cli -- emit csharp hello.spy

# Debug: inspect AST
dotnet run --project src/Sharpy.Cli -- emit ast hello.spy

# Debug: inspect tokens
dotnet run --project src/Sharpy.Cli -- emit tokens hello.spy

# Help
dotnet run --project src/Sharpy.Cli -- --help
```

## Adding a CLI Option

1. Define option in `Program.cs` using `System.CommandLine` API:
   ```csharp
   var myOption = new Option<string>("--my-option", "Description");
   command.AddOption(myOption);
   ```
2. Add handler logic in the command handler
3. Test manually (no automated CLI tests)
4. Update `src/Sharpy.Cli/README.md`

## Project File Format (`.spyproj`)

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

## Error Handling

- Write errors to stderr
- Return non-zero exit codes on failure
- Include file/line context in error messages

## Debugging Compilation Issues

```bash
# Check generated C# for codegen bugs
dotnet run --project src/Sharpy.Cli -- emit csharp problematic.spy

# Check AST for parser bugs
dotnet run --project src/Sharpy.Cli -- emit ast problematic.spy

# Check tokens for lexer bugs
dotnet run --project src/Sharpy.Cli -- emit tokens problematic.spy
```
