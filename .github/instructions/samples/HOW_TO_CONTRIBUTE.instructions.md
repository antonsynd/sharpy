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
```

## Adding a Sample

1. Create `.spy` file or project directory
2. Include comments explaining what's demonstrated
3. Test compilation: `dotnet run --project src/Sharpy.Cli -- build <file>`
4. Update `samples/README.md`

## Sample Quality Standards

- ✅ Compiles successfully
- ✅ Self-documenting with comments
- ✅ Works on .NET 9.0 and 10.0
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
   - Update samples/README.md to list the new sample

5. **Test the sample:**
   ```bash
   dotnet run --project src/Sharpy.Cli -- samples/new_feature_example.spy
   ```

6. **Add integration test** (optional but recommended):
   ```csharp
   // In Sharpy.Compiler.Tests/Integration/SampleTests.cs
   [Fact]
   public void CompileNewFeatureExample()
   {
       var source = File.ReadAllText("samples/new_feature_example.spy");
       var assembly = Compile(source);
       Assert.NotNull(assembly);
   }
   ```

### Updating an Existing Sample

1. **Make changes** to the sample code
2. **Test compilation:**
   ```bash
   dotnet run --project src/Sharpy.Cli -- samples/updated_sample.spy
   ```
3. **Update documentation** if behavior changes
4. **Update integration tests** if applicable
5. **Ensure it still demonstrates its purpose**

### Testing Samples

**Manual testing:**
```bash
# Compile the sample
dotnet run --project src/Sharpy.Cli -- samples/calculator_app/calculator.spyproj

# Run the compiled output
dotnet samples/calculator_app/bin/Debug/net9.0/calculator.dll
```

**Integration testing:**
```bash
# Run integration tests that use samples
dotnet test --filter "FullyQualifiedName~Integration"
```

### Debugging Sample Compilation

```bash
# Emit tokens to debug lexer issues
dotnet run --project src/Sharpy.Cli -- emit tokens samples/problem.spy

# Emit AST to debug parser issues
dotnet run --project src/Sharpy.Cli -- emit ast samples/problem.spy

# Check for detailed error messages
dotnet run --project src/Sharpy.Cli -- build samples/problem.spy 2>&1 | less
```

## Sample Categories

### Language Feature Demos
- **Purpose:** Show how to use specific language features
- **Examples:** `type_system_showcase.spy`
- **Should include:** Clear comments, multiple examples of the feature

### Integration Examples
- **Purpose:** Show how features work together
- **Examples:** `calculator_app/`
- **Should include:** Real-world structure, best practices

### Interop Examples
- **Purpose:** Demonstrate .NET interoperability
- **Examples:** `dotnet_interop_example.spy`
- **Should include:** Import statements, usage examples

### Module/Package Examples
- **Purpose:** Show project organization
- **Examples:** `SampleModule/`, `calculator_app/`
- **Should include:** `__init__.spy`, proper structure

## Best Practices for Samples

### Code Style
- Use clear, descriptive names
- Add comments explaining what's being demonstrated
- Follow Sharpy coding conventions
- Keep it simple unless demonstrating complexity

### Documentation
- Add file-level comment explaining purpose
- Document non-obvious code
- Include expected output where applicable
- Add README for complex samples

### Organization
- Group related samples together
- Use consistent project structure
- Name files descriptively
- Keep samples focused on one topic

### Maintenance
- Update samples when language changes
- Remove deprecated features
- Test samples regularly
- Keep documentation current

## Example: Creating a New Sample

**Goal:** Demonstrate exception handling

**1. Create the file:**
```bash
touch samples/error_handling_example.spy
```

**2. Write the sample:**
```python
# samples/error_handling_example.spy
# This sample demonstrates exception handling in Sharpy

def safe_divide(a: float, b: float) -> float?:
    """
    Safely divide two numbers.
    Returns None if division by zero.
    """
    try:
        return a / b
    except ZeroDivisionError as e:
        print(f"Error: {e}")
        return None

def main() -> None:
    # Example 1: Successful division
    result1 = safe_divide(10.0, 2.0)
    if result1 is not None:
        print(f"10.0 / 2.0 = {result1}")

    # Example 2: Division by zero
    result2 = safe_divide(10.0, 0.0)
    if result2 is None:
        print("Division by zero handled")

    # Example 3: Multiple exception types
    try:
        # Some operation that might fail
        value = int("not a number")
    except ValueError as e:
        print(f"ValueError caught: {e}")
    except Exception as e:
        print(f"Other error: {e}")
    finally:
        print("Cleanup code runs here")
```

**3. Test it:**
```bash
dotnet run --project src/Sharpy.Cli -- samples/error_handling_example.spy
```

**4. Update samples/README.md:**
```markdown
### error_handling_example.spy
Demonstrates exception handling with try/except/finally blocks.
Shows different exception types and null-safe error handling.
```

## Dependencies

None - samples are standalone Sharpy programs.

## Related Documentation

- **Main README:** `README.md` (root)
- **CLI Guide:** `.github/instructions/Sharpy.Cli/HOW_TO_CONTRIBUTE.instructions.md`
- **Language Manual:** `docs/manual/`
- **Integration Tests:** `.github/instructions/Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md`

## Getting Help

- Look at existing samples for structure and style
- Check language documentation for feature usage
- Run samples through the compiler to test
- Review integration tests to see how samples are used in testing
