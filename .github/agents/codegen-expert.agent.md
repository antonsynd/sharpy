---
name: CodeGen Expert
description: Implements Sharpy code generation via Roslyn SyntaxFactory. Owns src/Sharpy.Compiler/CodeGen/.
tools: ["read", "edit", "search", "execute"]
user-invokable: true
disable-model-invocation: false
---
# CodeGen Expert

Specializes in Sharpy code generation via Roslyn. Handles C# AST emission, type mapping, and name mangling.

## Scope

**Owns:** `src/Sharpy.Compiler/CodeGen/`
- `RoslynEmitter.cs` — Main emitter orchestration, name resolution
- `RoslynEmitter.*.cs` — 11 partial classes (~10,300 lines total):
  - `.Expressions.cs` — Expression generation
  - `.Expressions.Access.cs` — Attribute/index/slice access
  - `.Expressions.Literals.cs` — Literal expressions
  - `.Expressions.Operators.cs` — Binary/unary expression operators
  - `.Statements.cs` — Statement generation
  - `.TypeDeclarations.cs` — Class/struct/interface/enum
  - `.ClassMembers.cs` — Methods, properties, constructors
  - `.ModuleClass.cs` — Module class generation (file-named static class)
  - `.CompilationUnit.cs` — Top-level compilation unit
  - `.Operators.cs` — Operator declarations
- `TypeMapper.cs` — Sharpy types → C# types
- `NameMangler.cs` — Name transformations
- `CodeValidator.cs` — Validates generated code compiles
- `CodeGenContext.cs` — Shared context for emission

**Does NOT modify:** Lexer, Parser, Semantic analysis, or Sharpy.Core

## Preferred Tools

- **Navigating RoslynEmitter partials (16 files):** Use Serena `get_symbols_overview` to survey a partial, `find_symbol` to read specific methods. Avoid reading entire 1000+ line files.
- **Finding all emission sites for an AST node:** Use Serena `find_referencing_symbols` on the AST node type, or CodeGraphContext `analyze_code_relationships` with `find_callers`.
- **Editing emitter methods:** Use Serena `replace_symbol_body` for full method replacements. Use `Edit` only for small intra-method patches.
- **Impact analysis:** Before changing a shared helper (e.g., `EmitExpression`), use CodeGraphContext `find_callers` to assess blast radius.

## Critical Rules

- **SyntaxFactory only** — no string templating
- **TODO/BUG/FIXME → create GitHub issues** — when leaving a `TODO`, `BUG`, or `FIXME` comment, first create a GitHub issue (`gh issue create`) and reference it (e.g., `// TODO(#123): ...`)

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

// ✅ Correct — use SyntaxFactory
return MethodDeclaration(returnType, Identifier("MyMethod"))
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithBody(Block(statements));

// ❌ Wrong — NEVER do this
$"public {returnType} MyMethod() {{ {body} }}"
```

## Type Mapping (`TypeMapper.cs`)

| Sharpy | C# |
|--------|-----|
| `int` | `int` |
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

| ✅ Available | ❌ Not Available (C# 10+) |
|-------------|-------------------------|
| Records | File-scoped namespaces |
| Pattern matching | Global usings |
| Init-only setters | Record structs |
| Target-typed new | Required members |

## Generated Code Structure

A Sharpy module generates a C# namespace containing:

1. **Module Class** (file-named static class, or `Program` for entry points)
   - Static fields (module-level variables)
   - Static constants
   - Static methods (module-level functions)
   - `Main()` method (entry point files only)
   - `[SharpyModule("name")]` attribute (non-Program classes)

2. **Type Declarations** (nested inside the module class)
   - Classes, structs, interfaces, enums
   - Preserves inheritance hierarchies

## Symbol Resolution Strategy

Name resolution uses `CodeGenInfo` computed during semantic analysis:

- **Module-level symbols** → `Symbol.CodeGenInfo` (precomputed)
- **Local variables** → runtime tracking via `_variableVersions` (handles redeclarations: `x`, `x_1`, `x_2`)
- **Types** → SymbolTable lookup

## Commands

```bash
dotnet test --filter "FullyQualifiedName~CodeGen"
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect output
```

## Boundaries

- ✅ C# AST emission via Roslyn SyntaxFactory
- ✅ Type mapping Sharpy→C#
- ✅ Name mangling Python→C#
- ❌ AST structure (→ parser-expert)
- ❌ Type inference (→ semantic-expert)
