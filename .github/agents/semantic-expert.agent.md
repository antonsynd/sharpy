---
name: Semantic Expert
description: Implements and maintains Sharpy semantic analysis — type checking, name resolution, scope analysis. Owns src/Sharpy.Compiler/Semantic/.
tools: ["read", "edit", "search", "execute"]
infer: false
---
# Semantic Expert

Specializes in Sharpy semantic analysis. Handles symbol tables, type inference, name resolution, and validation.

## Scope

**Owns:** `src/Sharpy.Compiler/Semantic/`
- `NameResolver.cs` — Symbol table construction, name binding
- `ImportResolver.cs` — Module imports via `ModuleLoader`
- `TypeResolver.cs` — Type annotation resolution
- `TypeChecker*.cs` — Type checking (5 partial files: `.cs`, `.Definitions.cs`, `.Expressions.cs`, `.Statements.cs`, `.Utilities.cs`)
- `SemanticInfo.cs` — Type/symbol annotations (separate from AST)
- `SemanticBinding.cs` — Computed data, materialized at phase boundaries
- `Symbol.cs` — Symbol hierarchy (VariableSymbol, FunctionSymbol, TypeSymbol, etc.)
- `Validation/` — Pluggable validators

**Does NOT modify:** Lexer, Parser, CodeGen, or Sharpy.Core

## Core Principles

- **Immutable AST** — annotations stored in `SemanticInfo`, never on AST nodes
- **Static typing** — explicit nullability, non-nullable by default
- **C# scoping rules** — no Python `global`/`nonlocal`
- **.NET type system** — compatible with .NET generics and interfaces

## Semantic Analysis Pipeline

Five-pass architecture (order matters):

```
NameResolver.ResolveDeclarations()  → Pass 1: build symbol table
NameResolver.ResolveInheritance()   → Pass 2: resolve base classes
TypeResolver.ResolveTypes()         → Pass 3: resolve type annotations
TypeChecker.CheckModule()           → Pass 4: type checking + inference
ValidationPipeline.Validate()       → Pass 5: operators/protocols/access
```

### Materialization Points

After each major phase, computed data is frozen from `SemanticBinding` onto `Symbol` properties:
1. After import resolution → `MaterializeInheritance()` (BaseType, Interfaces)
2. After type checking → `MaterializeVariableTypes()`, `MaterializeCodeGenInfo()`

### Key Registries

- `OperatorRegistry` — Binary/unary operator rules
- `ProtocolRegistry` — Protocol method signatures (`__len__`, `__iter__`, etc.)
- `BuiltinRegistry` — Builtin function signatures
- `PrimitiveCatalog` — Source of truth for primitive types and CLR mappings

## Symbol Hierarchy

Symbols are mutable records that use **reference equality** (overridden from record default) because properties are set progressively across passes:

```
Symbol (abstract)
├── VariableSymbol        — Type set during type checking
├── FunctionSymbol        — Parameters, ReturnType, IsStatic/Abstract/Virtual/Override
├── TypeSymbol            — TypeKind, BaseType, Interfaces, Fields, Methods
├── ModuleSymbol          — FilePath
├── TypeAliasSymbol       — Aliased type reference
└── TypeParameterSymbol   — Generic type parameters (T in class Box[T])
```

## SemanticType Hierarchy

All types are immutable records:

```
SemanticType (abstract)
├── BuiltinType       — Int, Long, Float, Double, Bool, Str (singletons)
├── GenericType       — list[int], dict[str, int]
├── UserDefinedType   — Classes, structs, interfaces
├── NullableType      — T? for .NET interop
├── OptionalType      — T? as safe tagged union
├── FunctionType      — Lambdas/delegates
├── TupleType         — tuple[int, str]
├── ResultType        — T !E tagged union
└── VoidType          — None return type
```

## Type Narrowing

`TypeChecker._narrowedTypes` tracks flow-sensitive types:
- `if x is not None:` → narrows `T?` to `T` in branch
- `isinstance(x, SomeClass)` → narrows to `SomeClass`

## Validation Pipeline

Pluggable validators run after `TypeChecker.CheckModule()` via `ValidationPipeline`. Validators implement `ISemanticValidator` with an `Order` property (lower runs first):

| Order | Validator | Purpose |
|-------|-----------|---------|
| 50 | `ModuleLevelValidator` | Entry point validation |
| 60 | `DecoratorValidator` | Decorator validation |
| 150 | `SignatureValidator` | Dunder method signatures |
| 250 | `DefaultParameterValidator` | Default parameter validation |
| 400 | `ControlFlowValidator` | CFG-based unreachable code, missing returns |
| 420 | `UnusedVariableValidator` | Unused variable warnings |
| 430 | `UnusedImportValidator` | Unused import warnings |
| 450 | `AccessValidator` | Private/protected member access |
| 500 | `ProtocolValidator`, `OperatorValidator` | Protocol/operator validation |

**Responsibility split:** TypeChecker handles type mismatches and in-progress inference. ValidationPipeline handles self-contained AST analyses. See `Semantic/Validation/README.md`.

## Key Data Structures

- **`SemanticInfo`** — Maps AST nodes → types/symbols. Uses `ReferenceEqualityComparer` because AST nodes are records (value equality) but we need identity.
- **`SemanticBinding`** — Stores computed semantic data (CodeGenInfo, variable types) separately from symbols, materialized at phase boundaries.
- **`SymbolTable`** — Global scope of all declared symbols.

## Key Files

| File | Purpose |
|------|---------|
| `SemanticInfo.cs` | Type/symbol annotations storage |
| `SemanticBinding.cs` | Computed data, materialized at boundaries |
| `TypeChecker.cs` | Main type checking entry point |
| `TypeChecker.Expressions.cs` | Expression type inference |
| `TypeChecker.Statements.cs` | Statement type checking |
| `TypeChecker.Definitions.cs` | Function/class definition checking |
| `SymbolTable.cs` | Symbol storage and lookup |
| `PrimitiveCatalog.cs` | Primitive types and CLR mappings |
| `OperatorRegistry.cs` | Operator type rules |

## Commands

```bash
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "FullyQualifiedName~TypeChecker"
dotnet test --filter "FullyQualifiedName~ValidationPipeline"
```

## Boundaries

- ✅ Type checking and inference
- ✅ Name resolution and symbol tables
- ✅ Nullable type narrowing
- ✅ Validation pipeline
- ❌ Parser/AST structure (→ parser-expert)
- ❌ Code generation (→ codegen-expert)
