# Custom Agents

Specialized agents for Sharpy development. Each agent has domain expertise and clear boundaries.

> **See also:** [copilot-instructions.md](copilot-instructions.md) for architecture, [instructions/](instructions/) for component guides.

## The Three Axioms

All agents follow this priority order when axioms conflict:

| Priority | Axiom | Principle |
|----------|-------|-----------|
| 1 (Highest) | .NET Runtime | Compiles to valid C# 9.0 for .NET CLR |
| 2 | Static Typing | Non-nullable by default, explicit types |
| 3 (Yields) | Python Syntax | Python 3 syntax and idioms |

## Agent Reference

### Implementation Agents

| Agent | Domain | Edits |
|-------|--------|-------|
| `implementer` | Full implementation + PRs | All |
| `task-planner` | Task decomposition into phases | Read-only |
| `code-reviewer` | PR review (security, performance, SOLID) | Read-only |
| `test-expert` | Tests: unit, integration, file-based | `*Tests/` |

### Compiler Component Experts

| Agent | Component | Key Files |
|-------|-----------|-----------|
| `lexer-expert` | Tokenization | `Compiler/Lexer/`, `Token.cs` |
| `parser-expert` | AST construction | `Compiler/Parser/`, `Ast/*.cs` |
| `semantic-expert` | Type checking, name resolution, symbols | `Compiler/Semantic/`, `TypeChecker*.cs` |
| `codegen-expert` | C# emission via Roslyn | `Compiler/CodeGen/`, `RoslynEmitter*.cs` |
| `core-library-expert` | Standard library | `Sharpy.Core/`, `Partial.*/` |
| `cli-expert` | CLI commands | `Sharpy.Cli/`, `Program.cs` |

### Axiom Guardians (Advisory, Read-Only)

| Agent | Guards | Catches |
|-------|--------|---------|
| `net-axiom-guardian` | Axiom 1: .NET compatibility | C# 10+ features, invalid interop |
| `python-axiom-guardian` | Axiom 2: Python syntax | Unnecessary C#-isms, non-Pythonic API |
| `type-safety-guardian` | Axiom 3: Static typing | Dynamic typing, null safety violations |
| `axiom-arbiter` | Conflict resolution | Applies precedence rules |
| `unity-compatibility-guardian` | Unity/IL2CPP | AOT-unfriendly patterns, C# 10+ |
| `design-philosophy-guardian` | Developer happiness | Complexity creep, unnecessary features |

### Verification Agents (Read-Only)

| Agent | Purpose | Output |
|-------|---------|--------|
| `verification-expert` | Runs tests, reports results | Test reports |
| `spec-adherence` | Verifies impl matches spec | Compliance reports with citations |
| `hallucination-defense` | Fact-checks .NET/Python/Roslyn claims | Verification results |
| `documentation-sync` | Keeps docs synchronized with code | Doc update PRs |

## Key Rules for All Agents

1. **Never modify test expectations to pass** — fix the implementation
2. **Axiom precedence**: .NET > Type Safety > Python Syntax
3. **Verify Python behavior** — `python3 -c "..."` before implementing
4. **Language spec is authoritative** — check `docs/language_specification/` before implementing
5. **Follow existing patterns** — search codebase for similar code
6. Run tests before/after changes
7. **C# targets**: `Sharpy.Core` uses C# 9.0 (`netstandard2.0;netstandard2.1`); other projects use `net10.0` with `LangVersion latest`

## Commands

```bash
dotnet build sharpy.sln && dotnet test               # Build + test all
dotnet format whitespace                             # Format before commit
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Debug parser
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy  # Debug lexer
python3 -c "..."                                     # Verify Python behavior
```

## Feature Implementation Flow

For language features, component experts work in this order:

```
lexer-expert → parser-expert → semantic-expert → codegen-expert → test-expert
```

1. **Lexer** — Add tokens if needed (`Token.cs`, `Lexer.cs`)
2. **Parser** — Add AST records (`Ast/*.cs`), parsing rules in `Parser*.cs` (6 partial files)
3. **Semantic** — Add type rules (`TypeChecker*.cs` — 5 partial files), validators if needed
4. **CodeGen** — Emit C# via SyntaxFactory (`RoslynEmitter*.cs` — 8 partial files)
5. **Tests** — Unit tests per component + `.spy`/`.expected` file-based tests

## Semantic Analysis Pipeline (Critical Knowledge)

The semantic phase runs **five ordered passes**. Understanding this is critical:

```
NameResolver.ResolveDeclarations()  → Pass 1: build symbol table
NameResolver.ResolveInheritance()   → Pass 2: resolve base classes
TypeResolver.ResolveTypes()         → Pass 3: resolve type annotations
TypeChecker.CheckModule()           → Pass 4: type checking + inference
ValidationPipeline.Validate()       → Pass 5: operators/protocols/access
```

**Key registries**: `OperatorRegistry`, `ProtocolRegistry`, `BuiltinRegistry`, `PrimitiveCatalog`

## Symbol Architecture

Symbols use **reference equality** (overridden from record default) because properties are set progressively across passes:

- `SemanticInfo` — Maps AST nodes → types/symbols (uses `ReferenceEqualityComparer`)
- `SemanticBinding` — Stores computed data separately, materialized at phase boundaries
- `Symbol.CodeGenInfo` — Precomputed during semantic analysis for emitter use


## Testing Quick Reference

| Test Type | Location | Format |
|-----------|----------|--------|
| Unit tests | `*Tests/` | xUnit `[Fact]`/`[Theory]` |
| Integration | `Integration/` | Inherit `IntegrationTestBase` |
| File-based | `Integration/TestFixtures/` | `.spy` + `.expected` or `.error` |
| Multi-file | `Integration/TestFixtures/{dir}/` | `main.spy` + siblings + `main.expected` |
| Warnings | `Integration/TestFixtures/` | `.spy` + `.warning` |
| Skip | Add `.skip` file | Contains skip reason |
