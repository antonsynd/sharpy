# Implement Feature

Implement a Sharpy compiler or standard library feature.

## Task

$ARGUMENTS

## Workflow

1. **Understand** - Parse requirements from the task description
2. **Research** - Search codebase for related code patterns
3. **Consult Spec** - Check `docs/language_specification/` for relevant specs
4. **Plan** - Break into subtasks, identify affected components (Lexer → Parser → Semantic → Validation → CodeGen)
5. **Implement** - Write code following Sharpy conventions
6. **Test** - Run tests, add new tests if needed
7. **Verify** - Ensure all tests pass with `dotnet test`

## Code Conventions

### Sharpy Philosophy
- .NET first, Pythonic second
- Static typing, compile-time resolution
- No dynamic dispatch or runtime type discovery

### Axiom Precedence
When axioms conflict: **Axiom 1 (.NET) > Axiom 3 (Type Safety) > Axiom 2 (Python Syntax)**

### C# Style
- **C# 9.0 target** — no global usings, file-scoped namespaces, or record structs
- PascalCase public, `_camelCase` private fields
- Nullable annotations used correctly
- XML docs on public APIs

## Testing

**CRITICAL:** Never alter expected values to pass tests. Fix the implementation.

```bash
dotnet build sharpy.sln
dotnet test
dotnet test --filter "FullyQualifiedName~ComponentName"
```

## Component Ownership

| Component | Directory |
|-----------|-----------|
| Lexer | `src/Sharpy.Compiler/Lexer/` |
| Parser | `src/Sharpy.Compiler/Parser/` |
| Semantic | `src/Sharpy.Compiler/Semantic/` |
| Validation | `src/Sharpy.Compiler/Semantic/Validation/` |
| CodeGen | `src/Sharpy.Compiler/CodeGen/` |
| Core Library | `src/Sharpy.Core/` |
| CLI | `src/Sharpy.Cli/` |
| Tests | `src/Sharpy.Compiler.Tests/`, `src/Sharpy.Core.Tests/` |
