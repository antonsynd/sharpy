# Samples

Example programs demonstrating Sharpy features. Location: `samples/`

## Current Samples

| Sample | Purpose |
|--------|---------|
| `calculator_app/` | Multi-file project with `.spyproj` |
| `SampleModule/` | Module structure with `__init__.spy` |
| `type_system_showcase.spy` | Type annotations, inference, generics |
| `dotnet_interop_example.spy` | .NET assembly imports |

## Building Samples

```bash
# Single file
dotnet run --project src/Sharpy.Cli -- build samples/type_system_showcase.spy

# Project
dotnet run --project src/Sharpy.Cli -- project samples/calculator_app/calculator.spyproj

# Run compiled output
dotnet samples/calculator_app/bin/Debug/net9.0/calculator.dll
```

## Adding a Sample

1. Create `.spy` file or project directory in `samples/`
2. Include comments explaining what's demonstrated
3. Test compilation:
   ```bash
   dotnet run --project src/Sharpy.Cli -- build samples/your_sample.spy
   ```
4. Update `samples/README.md` to list the new sample

## Sample Quality Standards

- ✅ Compiles successfully on both .NET 9.0 and 10.0
- ✅ Self-documenting with comments
- ✅ Demonstrates specific feature(s) clearly
- ❌ No deprecated features or hardcoded paths

## Project Template

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <PropertyGroup>
        <RootNamespace>MySample</RootNamespace>
        <OutputType>exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include="src/**/*.spy" />
    </ItemGroup>
</Project>
```

## Testing Samples

```bash
# Verify sample compiles
dotnet run --project src/Sharpy.Cli -- build samples/my_sample.spy

# Check generated C# if issues arise
dotnet run --project src/Sharpy.Cli -- emit csharp samples/my_sample.spy
```
