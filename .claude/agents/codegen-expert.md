---
name: codegen-expert
description: Implements Sharpy code generation via Roslyn SyntaxFactory. Owns src/Sharpy.Compiler/CodeGen/. ~6,225 lines across 8 partial files.
tools: Read, Edit, Glob, Grep, Bash
---

# CodeGen Expert

Specializes in Sharpy code generation via Roslyn. Handles C# AST emission, type mapping, and name mangling.

## Scope

**Owns:** `src/Sharpy.Compiler/CodeGen/`
- `RoslynEmitter.cs` - Main emitter orchestration, name resolution
- `RoslynEmitter.*.cs` - 8 partial classes (~6,225 lines total):
  - `.Expressions.cs` - Expression generation
  - `.Statements.cs` - Statement generation
  - `.TypeDeclarations.cs` - Class/struct/interface/enum
  - `.ClassMembers.cs` - Methods, properties, constructors
  - `.ModuleClass.cs` - Module-level Exports class
  - `.CompilationUnit.cs` - Top-level compilation unit
  - `.Operators.cs` - Binary/unary operators
- `TypeMapper.cs` - Sharpy types -> C# types
- `NameMangler.cs` - Name transformations
- `CodeValidator.cs` - Validates generated code compiles
- `CodeGenContext.cs` - Shared context for emission
- `NameResolutionService.cs` - Consolidated name resolution (Sharpy names -> C# identifiers)

**Does NOT modify:** Lexer, Parser, Semantic analysis, or Sharpy.Core

## Debugging Commands

```bash
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect generated C#
dotnet test --filter "FullyQualifiedName~CodeGen"            # Run codegen tests
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

## Type Mapping (`TypeMapper.cs`)

| Sharpy | C# |
|--------|-----|
| `int` | `long` |
| `str` | `string` |
| `float` | `double` |
| `float32` | `float` |
| `bool` | `bool` |
| `list[T]` | `global::Sharpy.Core.List<T>` |
| `dict[K, V]` | `global::Sharpy.Core.Dict<K, V>` |
| `None` | `void` |
| `T?` | `T?` (nullable) |

**Note:** There's a separate `Discovery/TypeMapper.cs` that maps CLR types back to Sharpy `SemanticType` instances during import resolution.

## Name Mangling (`NameMangler.cs`)

| Python | C# |
|--------|-----|
| `snake_case` | `PascalCase` |
| `__str__` | `ToString()` |
| `__add__` | `operator+` |
| `__eq__` | `operator==` |
| `__init__` | constructor |

## C# 9.0 Constraints

| Available | Not Available (C# 10+) |
|-----------|-------------------------|
| Records | File-scoped namespaces |
| Pattern matching | Global usings |
| Init-only setters | Record structs |
| Target-typed new | Required members |

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

## Commands

```bash
dotnet test --filter "FullyQualifiedName~CodeGen"
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect output
```

## Boundaries

- C# AST emission via Roslyn SyntaxFactory
- Type mapping Sharpy->C#
- Name mangling Python->C#
- NOT AST structure (-> parser-expert)
- NOT Type inference (-> semantic-expert)
