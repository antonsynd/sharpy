---
name: codegen-expert
description: Implements Sharpy code generation via Roslyn SyntaxFactory. Owns src/Sharpy.Compiler/CodeGen/. ~19,680 lines across 25 files.
tools: Read, Edit, Glob, Grep, Bash
---

# CodeGen Expert

Specializes in Sharpy code generation via Roslyn. Handles C# AST emission, type mapping, and name mangling.

## Scope

**Owns:** `src/Sharpy.Compiler/CodeGen/`

RoslynEmitter partial files (~19,680 lines across 24 partial classes + factory):
- `RoslynEmitter.cs` - Main emitter orchestration, name resolution
- `.Expressions.cs` - Expression generation
- `.Expressions.Access.cs` - Member access, indexing
- `.Expressions.Access.Calls.cs` - Method/function calls
- `.Expressions.Comprehensions.cs` - List/dict/set comprehensions
- `.Expressions.Literals.cs` - Literal values
- `.Expressions.Operators.cs` - Binary/unary operators
- `.Statements.cs` - General statements
- `.Statements.Assignments.cs` - Assignment statements
- `.Statements.ControlFlow.cs` - if/while/for/match/try
- `.TypeDeclarations.cs` - Class/struct/interface/enum
- `.ClassMembers.cs` - Class member orchestration
- `.ClassMembers.Constructors.cs` - Constructor generation
- `.ClassMembers.Dataclass.cs` - @dataclass decorator support
- `.ClassMembers.Events.cs` - Event emission
- `.ClassMembers.Iterators.cs` - Iterator/generator support
- `.ClassMembers.LruCache.cs` - @lru_cache decorator support
- `.ClassMembers.Methods.cs` - Method generation
- `.ClassMembers.Properties.cs` - Property generation
- `.CompilationUnit.cs` - Top-level compilation unit
- `.ModuleClass.cs` - Module-level Exports class
- `.Operators.cs` - Operator overload emission
- `.Patterns.cs` - Pattern matching emission
- `.TestFixtures.cs` - Test infrastructure helpers
- `RoslynEmitterFactory.cs` - Factory for creating emitter instances

Supporting files:
- `TypeSyntaxMapper.cs` - Sharpy types -> C# type syntax
- `NameResolutionService.cs` - Consolidated name resolution
- `CodeGenContext.cs` - Shared context for emission
- `CodeValidator.cs` - Validates generated code compiles
- `CollectionTypeRegistry.cs` - Collection type mappings
- `DunderCodeGenRegistry.cs` - Dunder method -> C# mapping registry
- `DunderMapping.cs` - Individual dunder mappings
- `ICodeEmitter.cs` / `ICodeEmitterFactory.cs` - Interfaces
- `LineDirectivePostProcessor.cs` - #line directive handling

**Does NOT modify:** Lexer, Parser, Semantic analysis, or Sharpy.Core

## Debugging Commands

```bash
.claude/scripts/dotnet-serialized run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect generated C#
.claude/scripts/dotnet-serialized test --filter "FullyQualifiedName~CodeGen"            # Run codegen tests
.claude/scripts/dotnet-serialized test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # Integration tests
```

## Core Principle

Sharpy compiles to C# AST via Roslyn, **not** to IL directly. This enables:
- Roslyn's optimization pipeline
- Source-level debugging
- Human-readable `emit csharp` output

## Key Pattern: SyntaxFactory Only

**NEVER use string templating:**
```csharp
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

// CORRECT - use SyntaxFactory
return MethodDeclaration(returnType, Identifier("MyMethod"))
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithBody(Block(statements));

// WRONG - NEVER do this
$"public {returnType} MyMethod() {{ {body} }}"
```

## Type Mapping (`TypeSyntaxMapper.cs`)

| Sharpy | C# |
|--------|-----|
| `int` | `long` |
| `str` | `string` |
| `float` | `double` |
| `float32` | `float` |
| `bool` | `bool` |
| `list[T]` | `global::Sharpy.Core.List<T>` |
| `dict[K, V]` | `global::Sharpy.Core.Dict<K, V>` |
| `set[T]` | `global::Sharpy.Core.Set<T>` |
| `None` | `void` |
| `T?` | `T?` (nullable) |

## Name Mangling (`Shared/NameMangler.cs`)

| Python | C# |
|--------|-----|
| `snake_case` | `PascalCase` |
| `__str__` | `ToString()` |
| `__add__` | `operator+` |
| `__eq__` | `operator==` |
| `__init__` | constructor |

## Symbol Resolution Strategy

Name resolution uses `CodeGenInfo` computed during semantic analysis:

- **Module-level symbols** -> `Symbol.CodeGenInfo` (precomputed)
- **Local variables** -> runtime tracking via `_variableVersions` (handles redeclarations: `x`, `x_1`, `x_2`)
- **Types** -> SymbolTable lookup

## RoslynEmitter Tracking Variables

Key internal state:
- `_variableVersions` - Local redeclaration: x, x_1, x_2
- `_sourceVariableNames` - Original Python names
- `_constVariables` - Compile-time constants
- `_moduleFieldNames` - Module-level field names

## Generated Code Structure

A Sharpy module generates a C# namespace containing:

1. **Module Class** (`Exports` or `Program`)
   - Static fields (module-level variables)
   - Static constants
   - Static methods (module-level functions)
   - `Main()` method (entry point files only)

2. **Type Declarations** (at namespace level, NOT nested)
   - Classes, structs, interfaces, enums
   - Preserves inheritance hierarchies

## Boundaries

- C# AST emission via Roslyn SyntaxFactory
- Type mapping Sharpy->C#
- Name mangling Python->C#
- NOT AST structure (-> parser-expert)
- NOT Type inference (-> semantic-expert)
