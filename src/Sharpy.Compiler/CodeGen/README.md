# Code Generation

This directory contains the Roslyn-based C# code generator.

## Key Files

- `RoslynEmitter.cs` - Main emitter, orchestrates code generation and name resolution
- `RoslynEmitter.*.cs` - Partial classes for different AST node types (11 files total):
  - `.Expressions.cs` - Expression generation
  - `.Expressions.Access.cs` - Member access, indexing, slicing
  - `.Expressions.Literals.cs` - Literal expressions (numbers, strings, collections)
  - `.Expressions.Operators.cs` - Binary/unary operator expressions
  - `.Statements.cs` - Statement generation
  - `.TypeDeclarations.cs` - Class/struct/interface/enum generation
  - `.ClassMembers.cs` - Methods, properties, constructors
  - `.ModuleClass.cs` - Module class generation (named after source file)
  - `.CompilationUnit.cs` - Top-level compilation unit
  - `.Operators.cs` - Operator method emission
- `CodeGenContext.cs` - Shared context for emission
- `TypeMapper.cs` - Maps Sharpy types to C# types
- `NameMangler.cs` - Converts snake_case to PascalCase, dunder methods
- `CodeValidator.cs` - Validates generated code compiles

## Generated Code Structure

A Sharpy module generates a C# namespace containing a static module class.
The module class is named after the source file (PascalCase), or `Program` for
entry point files. Types (classes, structs, interfaces, enums) are nested inside
the module class. Directory hierarchy is expressed via nested `partial static class`
wrappers, not additional namespaces.

1. **Module Class** (named after source file, or `Program` for entry points)
   - Static fields (module-level variables)
   - Static constants
   - Static methods (module-level functions)
   - Nested type declarations (classes, structs, interfaces, enums)
   - `Main()` method (entry point files only)
   - `[SharpyModule("name")]` attribute (non-Program classes)

Example:

```python
# geometry.spy
counter: int = 0

class Point:
    x: int
    y: int

def helper() -> int:
    return 42

def main():
    p = Point(1, 2)
```

Generates:

```csharp
namespace MyProject
{
    public static partial class Program
    {
        public static int Counter = 0;

        public class Point  // Nested inside module class
        {
            public int X;
            public int Y;
        }

        public static int Helper() => 42;
        public static void Main() { ... }
    }
}
```

For subdirectory files, wrapper classes are generated:

```csharp
// lib/math/ops.spy → wrappers [Lib, Math], module Ops
namespace MyProject
{
    public static partial class Lib
    {
        public static partial class Math
        {
            [SharpyModule("lib.math.ops")]
            public static partial class Ops
            {
                // module members here
            }
        }
    }
}
```

## Architecture

The emitter uses Roslyn's `SyntaxFactory` exclusively (no string templating):

1. **Module → CompilationUnit**
   - Creates project-level namespace and using directives
   - Creates module class (named after file, or Program for entry points)
   - Wraps module class in directory-based `partial static class` wrappers
   - Nests all type declarations inside the module class

2. **Module-Level Declarations**
   - Entry point files require `main()` function
   - Module-level variables require type annotations
   - Top-level declarations become static fields/methods

3. **Type definitions → C# classes/structs/interfaces/enums**
   - Nested inside the module class
   - Preserves inheritance hierarchies
   - Handles abstract/virtual/override modifiers

4. **Functions → Methods**
   - Parameters mapped to C# parameters
   - Return types mapped via TypeMapper

5. **Expressions → Roslyn expression syntax**
   - Operators mapped appropriately
   - Method calls resolved via CodeGenInfo

## Symbol Resolution

Name resolution uses `CodeGenInfo` computed during semantic analysis. The emitter
uses `Symbol.CodeGenInfo` to determine:

- C# names for identifiers
- Whether a variable is a field vs local
- Proper scoping for imports

Local variables still require tracking during emission for redeclaration handling
(e.g., `x = 1; x = "hello"` produces `x` then `x_1`).

## Type Mapping

`TypeMapper` handles the translation from Sharpy types to C# types:

- `int` → `int`
- `long` → `long`
- `float` → `double`
- `double` → `double`
- `float32` → `float`
- `str` → `string`
- `bool` → `bool`
- `list[T]` → `global::Sharpy.List<T>`
- `dict[K, V]` → `global::Sharpy.Dict<K, V>`
- `set[T]` → `global::Sharpy.Set<T>`
- `None` → `void`

## Name Mangling

`NameMangler` converts Python conventions to C#:

- `snake_case` → `PascalCase` for public members
- `__init__` → constructor
- `__str__` → `ToString()`
- `__eq__` → `operator==`
- `__add__` → `operator+`
