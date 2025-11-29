# Sharpy.Cli

Command-line interface for the Sharpy compiler. Location: `src/Sharpy.Cli/`

## Key Files

- `Program.cs` - Entry point using System.CommandLine
- Delegates to `Sharpy.Compiler.Compiler` and `AssemblyCompiler`

## Commands

```bash
# Build single file
dotnet run --project src/Sharpy.Cli -- build snippets/hello.spy

# Build project
dotnet run --project src/Sharpy.Cli -- project samples/calculator_app/calculator.spyproj

# Help
dotnet run --project src/Sharpy.Cli -- --help
```

## Adding a CLI Option

1. Define option in `Program.cs` using System.CommandLine API
2. Add handler logic
3. Test manually (no automated CLI tests)
4. Update `README.md`

## Error Handling

- Write errors to stderr
- Return non-zero exit codes on failure
- Include file/line context in error messages

## Project File Format

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

## Related Documentation

- **Main README:** `README.md` (repository root)
- **CLI README:** `src/Sharpy.Cli/README.md`
- **Compiler Guide:** `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`
