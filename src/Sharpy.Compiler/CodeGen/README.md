# Code Generation

This directory contains the Roslyn-based C# code generator.

## Key Files

- `RoslynEmitter.cs` - Main emitter, orchestrates code generation
- `RoslynEmitter.*.cs` - Partial classes for different AST node types:
  - `.Expressions.cs` - Expression generation
  - `.Statements.cs` - Statement generation
  - `.TypeDeclarations.cs` - Class/struct/interface/enum generation
  - `.ClassMembers.cs` - Methods, properties, constructors
  - `.ModuleClass.cs` - Module-level class (Exports)
  - `.CompilationUnit.cs` - Top-level compilation unit
  - `.Operators.cs` - Binary/unary operators
- `CodeGenContext.cs` - Shared context for emission
- `TypeMapper.cs` - Maps Sharpy types to C# types
- `NameMangler.cs` - Converts snake_case to PascalCase, dunder methods
- `CodeValidator.cs` - Validates generated code compiles

## Architecture

The emitter uses Roslyn's `SyntaxFactory` exclusively (no string templating):

1. **Module → CompilationUnit**
   - Creates namespace and using directives
   - Wraps module in an `Exports` class

2. **Module-Level Declarations**
   - Entry point files require `main()` function
   - Module-level variables require type annotations
   - Top-level declarations become static fields/methods

3. **Type definitions → C# classes/structs/interfaces/enums**
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

- `list[T]` → `global::Sharpy.Core.List<T>`
- `dict[K, V]` → `global::Sharpy.Core.Dict<K, V>`
- `str` → `string`
- `int` → `long`
- `float` → `double`
- `bool` → `bool`
- `None` → `void`

## Name Mangling

`NameMangler` converts Python conventions to C#:

- `snake_case` → `PascalCase` for public members
- `__init__` → constructor
- `__str__` → `ToString()`
- `__eq__` → `operator==`
- `__add__` → `operator+`
