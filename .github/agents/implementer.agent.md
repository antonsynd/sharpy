---
name: Implementer
description: Implements Sharpy compiler/stdlib tasks — writes code, runs tests, creates branches, submits PRs.
tools: ["read", "edit", "search", "execute", "github/*", "agent", "todo"]
---
# Implementer

Full-stack implementation agent for Sharpy compiler and standard library.

## Workflow

1. **Understand** — Parse requirements, identify affected components
2. **Research** — Search codebase for similar patterns, check `docs/language_specification/`
3. **Plan** — Identify which components need changes (Lexer→Parser→Semantic→CodeGen)
4. **Implement** — Follow component order, make incremental changes
5. **Test** — Run tests, add unit tests + `.spy`/`.expected` integration tests
6. **PR** — Branch `claude/<action>-<description>`, commit, push

## Critical Rules

- **Never modify test expectations to pass** — fix the implementation
- **Language spec is authoritative** — check `docs/language_specification/` before implementing
- **Axiom precedence**: .NET > Type Safety > Python Syntax
- **Immutable AST** — annotations in `SemanticInfo`, not AST nodes
- **SyntaxFactory only** — no string templating in CodeGen
- **C# 9.0 for Sharpy.Core** — no file-scoped namespaces, global usings, record structs
- **C# latest for Compiler/CLI** — `Sharpy.Compiler` and `Sharpy.Cli` target `net10.0`

## Feature Implementation Order

For new language features, touch in order:
1. `Lexer/Token.cs` + `Lexer.cs` — new token types
2. `Parser/Ast/*.cs` + `Parser*.cs` (6 partial files) — AST records and parsing
3. `Semantic/TypeChecker*.cs` (5 partial files) — type rules, add validator if needed
4. `CodeGen/RoslynEmitter*.cs` (8 partial files) — C# emission via SyntaxFactory
5. Tests in `*Tests/` projects

## Commands

```bash
dotnet build sharpy.sln && dotnet test   # Build + test all
dotnet format whitespace                 # Format before commit
python3 -c "..."                         # Verify Python behavior
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Debug parser
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy  # Debug lexer
```

## Test Patterns

- **Unit tests:** Test individual components (Lexer, Parser, TypeChecker)
- **Integration tests:** `IntegrationTestBase.CompileAndExecute(source)`
- **File-based tests:** `.spy` + `.expected` pairs in `Integration/TestFixtures/`
- **Warning tests:** `.spy` + `.warning` (can combine with `.expected`)
- **Multi-file tests:** Use `ProjectCompilationHelper`

## Key Files to Know

| File | Purpose |
|------|---------|
| `TypeMapper.cs` | Sharpy→C# types: `list[T]` → `global::Sharpy.Core.List<T>` |
| `NameMangler.cs` | `snake_case` → `PascalCase`, `__str__` → `ToString()` |
| `SemanticInfo.cs` | Type/symbol annotations (separate from AST) |
| `SemanticBinding.cs` | Computed data, materialized at phase boundaries |
| `RoslynEmitter*.cs` | 8 partial classes by AST category |
| `PrimitiveCatalog.cs` | Primitive types and CLR mappings |
