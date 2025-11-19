# Sharpy Sample Programs

This directory contains comprehensive example programs demonstrating Sharpy's features and capabilities.

## Examples Overview

### 1. **dotnet_interop_example.spy**
Demonstrates seamless integration with .NET libraries and the C# ecosystem.

**Features showcased:**
- Importing .NET namespaces (`System`, `System.Collections.Generic`, `System.Linq`)
- Using .NET collection types (`HashSet<T>`, `List<T>`)
- LINQ integration with lambda expressions
- Working with system types (`DateTime`, `Guid`)
- Hybrid Sharpy/C# classes using .NET types internally
- File path operations using `System.IO.Path`
- Console I/O with colored output

**Topics covered:**
- .NET collections
- LINQ queries
- DateTime operations
- GUID generation
- Path manipulation
- Console formatting

### 2. **type_system_showcase.spy**
Comprehensive demonstration of Sharpy's rich type system.

**Features showcased:**
- Type inference with the `auto` keyword
- Explicit type annotations
- Nullable types with `?` suffix
- Null-conditional operator (`?.`)
- Null coalescing operator (`??`)
- Type narrowing with `is not None`
- Type narrowing with `isinstance()`
- Generic types and classes
- Generic functions with type parameters
- Interface-based polymorphism
- Collection generics (`list[T]`, `dict[K,V]`, `set[T]`)

**Topics covered:**
- Type inference
- Null safety
- Type narrowing
- Generics
- Interfaces
- Polymorphism

### 3. **calculator_app/** (Multi-file project with packages)
A complete multi-file Sharpy project demonstrating project compilation and package organization.

**Structure:**
```
calculator_app/
├── calculator.spyproj    # Project file
├── README.md             # Project documentation
└── src/
    ├── main.spy          # Entry point
    ├── ui.spy            # User interface
    └── math/             # Math operations package
        ├── __init__.spy  # Package exports
        ├── basic.spy     # Basic operations
        └── advanced.spy  # Advanced operations
```

**Features showcased:**
- `.spyproj` project files with glob patterns
- Multi-file compilation
- Package structure with `__init__.spy`
- Cross-module imports
- Namespace generation from directory structure
- Debug/Release build configurations

**Topics covered:**
- Project organization
- Package-level exports
- Module imports
- Build configurations
- Assembly generation

### 4. **SampleModule/** (Legacy multi-file example)
A complete multi-file Sharpy module demonstrating project organization.

**Structure:**
```
SampleModule/
├── __init__.spy          # Module initialization
├── models.spy            # Data models and classes
├── utils.spy             # Utility functions
└── main.spy              # Entry point
```

**Features showcased:**
- Multi-file project organization
- Module imports between files
- Public/private APIs
- Code organization patterns
- Cross-module type sharing

## Running the Examples

### Single-File Examples

```bash
# Tokenize an example to see the lexer output
dotnet run --project ../src/Sharpy.Cli -- dotnet_interop_example.spy --emit-tokens

# Parse an example to see the AST
dotnet run --project ../src/Sharpy.Cli -- type_system_showcase.spy --emit-ast
```

### Project Examples

```bash
# Compile the calculator project
cd calculator_app
sharpyc

# Or compile in Release mode
sharpyc --configuration Release

# Run the compiled application
./bin/Debug/net9.0/Calculator.exe
```

### Integration Testing

The examples can be used with the integration test infrastructure:

```csharp
// In tests
var result = CompileAndExecute(File.ReadAllText("samples/dotnet_interop_example.spy"));
Assert.True(result.Success);
```

## Key Language Features Demonstrated

### Pythonic Syntax
- Indentation-based blocks
- Snake_case naming conventions
- F-strings for formatting
- List/dict/set literals
- For/while loops with else clause

### Static Typing
- Compile-time type checking
- Type inference
- Generic type parameters
- Nullable type annotations
- Type narrowing

### .NET Integration
- Import .NET namespaces
- Use C# classes and methods
- Automatic snake_case conversion
- LINQ query support
- Full .NET library access

### Modern Features
- Null-safe operators (`?.`, `??`)
- Lambda expressions
- Type guards (`isinstance`)
- Generic constraints
- Interface polymorphism

## Learning Path

**Recommended order for learning:**

1. **Start with `type_system_showcase.spy`**
   - Learn the basics of Sharpy's type system
   - Understand null safety
   - See how generics work

2. **Explore `dotnet_interop_example.spy`**
   - Learn how to use .NET libraries
   - Understand namespace imports
   - See LINQ integration

3. **Build `calculator_app/`**
   - Understand project files (`.spyproj`)
   - Learn multi-file compilation
   - See package organization with `__init__.spy`
   - Build and run a complete application

4. **Study `SampleModule/`**
   - Alternative module organization approach
   - See different import patterns

## Contributing Examples

When adding new examples:

1. **Document clearly** - Add comments explaining what each section does
2. **Show best practices** - Demonstrate idiomatic Sharpy code
3. **Keep it focused** - Each example should showcase specific features
4. **Test it works** - Ensure examples compile and run successfully
5. **Update this README** - Document new examples added

## Example Template

```python
# ============================================================================
# Example Title
# Brief description of what this example demonstrates
# ============================================================================

# Imports
from system import console

# ============================================================================
# Section 1: Feature Name
# ============================================================================

def demonstrate_feature() -> None:
    """Detailed explanation of the feature"""
    # Implementation
    pass

# ============================================================================
# Main Entry Point
# ============================================================================

def main() -> None:
    """Run all demonstrations"""
    demonstrate_feature()

main()
```

## Additional Resources

- **Language Reference**: `../docs/language_reference.md`
- **Type System**: `../docs/type_system.md`
- **Manual**: `../docs/manual/`
- **Feature Status**: `../docs/feature_support.md`
- **v0.5 Features**: `../docs/v0.5-feature-list.md`

## Questions?

If you have questions about these examples or want to contribute new ones:

1. Check the [documentation](../docs/)
2. Look at the [test files](../src/Sharpy.Compiler.Tests/) for more examples
3. Open an issue on GitHub
4. Submit a pull request with improvements

Happy coding with Sharpy! 🚀
