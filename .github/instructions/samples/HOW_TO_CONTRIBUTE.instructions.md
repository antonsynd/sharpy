# Contributing to Samples

## Overview

**samples/** contains example Sharpy programs and projects that demonstrate language features and serve as integration tests for the compiler.

**Location:** `samples/`

## What's in This Directory

### Directory Structure

```
samples/
├── README.md                    # Samples overview
├── calculator_app/              # Complete calculator application
│   ├── calculator.spyproj      # Project file
│   ├── src/                    # Source files
│   │   ├── __init__.spy        # Package initialization
│   │   ├── calculator.spy      # Main calculator logic
│   │   └── operations.spy      # Operation implementations
│   └── README.md               # Calculator app documentation
├── SampleModule/                # Example module structure
│   ├── __init__.spy            # Module exports
│   └── utilities.spy           # Utility functions
├── type_system_showcase.spy     # Type system examples
└── dotnet_interop_example.spy   # .NET interop examples
```

### Current Samples

**calculator_app/**
- Full working application
- Multi-file project with `.spyproj`
- Demonstrates project structure
- Shows module imports
- Tests project compilation

**SampleModule/**
- Module organization example
- `__init__.spy` usage
- Package-level exports

**type_system_showcase.spy**
- Type annotations
- Type inference
- Generic types
- Nullable types

**dotnet_interop_example.spy**
- Importing .NET assemblies
- Using C# libraries
- Calling .NET methods

## Purpose of Samples

### Documentation
- Teach users how to use Sharpy
- Demonstrate best practices
- Show real-world usage patterns

### Testing
- Integration testing for compiler
- End-to-end validation
- Real-world code compilation
- Project structure validation

### Validation
- Ensure language features work together
- Test multi-file compilation
- Verify .NET interop
- Check module system

## How to Build Samples

### Build a Single Sample File
```bash
# Compile a single-file sample
dotnet run --project src/Sharpy.Cli -- samples/type_system_showcase.spy

# Emit tokens (for debugging)
dotnet run --project src/Sharpy.Cli -- samples/type_system_showcase.spy --emit-tokens
```

### Build a Sample Project
```bash
# Build calculator app
dotnet run --project src/Sharpy.Cli -- --project samples/calculator_app/calculator.spyproj

# Build in Release mode
dotnet run --project src/Sharpy.Cli -- --project samples/calculator_app/calculator.spyproj --configuration Release
```

## How to Run Samples

### Run Compiled Output
```bash
# After building calculator app
dotnet samples/calculator_app/bin/Debug/net9.0/calculator.dll
```

### Test in Integration Tests
Samples are often used in integration tests in `Sharpy.Compiler.Tests/Integration/`:

```csharp
[Fact]
public void CompileCalculatorApp()
{
    var projectPath = "samples/calculator_app/calculator.spyproj";
    var assembly = CompileProject(projectPath);
    Assert.NotNull(assembly);
}
```

## Important Things to Note

### Sample Quality Standards

**Samples should:**
- ✅ Compile successfully with latest compiler
- ✅ Follow Sharpy best practices
- ✅ Include comments explaining what they demonstrate
- ✅ Be self-contained (or clearly document dependencies)
- ✅ Have clear, descriptive names
- ✅ Include README if it's a complex project
- ✅ Work on both .NET 9.0 and .NET 10.0

**Samples should NOT:**
- ❌ Use deprecated features
- ❌ Have syntax errors or warnings
- ❌ Be overly complex (unless demonstrating complex features)
- ❌ Depend on external files not in the repository
- ❌ Include hardcoded paths or environment-specific settings

### Sample Project Structure

**For multi-file projects:**
```
my_sample/
├── my_sample.spyproj    # Project file
├── src/                 # Source files
│   ├── __init__.spy     # Package exports
│   ├── main.spy         # Entry point
│   └── utils.spy        # Utilities
└── README.md            # Documentation
```

**Project file template:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <PropertyGroup>
        <RootNamespace>MySample</RootNamespace>
        <OutputType>exe</OutputType>
        <Configuration>Debug</Configuration>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include="src/**/*.spy" />
    </ItemGroup>
</Project>
```

## Common Development Tasks

### Adding a New Sample

1. **Choose what to demonstrate:**
   - New language feature?
   - Real-world use case?
   - Integration pattern?
   - .NET interop?

2. **Create the sample:**
   ```bash
   # Single file
   touch samples/new_feature_example.spy

   # Or multi-file project
   mkdir samples/new_app
   touch samples/new_app/new_app.spyproj
   mkdir samples/new_app/src
   touch samples/new_app/src/__init__.spy
   ```

3. **Write the code:**
   ```python
   # samples/new_feature_example.spy
   # This sample demonstrates <feature>

   def example() -> None:
       # Example code here
       pass

   def main() -> None:
       example()
   ```

4. **Add documentation:**
   - Add comments in the code
   - Create README.md for complex samples
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
dotnet run --project src/Sharpy.Cli -- samples/problem.spy --emit-tokens

# Check for detailed error messages
dotnet run --project src/Sharpy.Cli -- samples/problem.spy 2>&1 | less
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
