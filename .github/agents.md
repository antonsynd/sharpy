# Custom Agents

Specialized agents for Sharpy development. Each agent has domain expertise and clear boundaries.

> **See also:** [copilot-instructions.md](copilot-instructions.md) for architecture and patterns.

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
| `semantic-expert` | Type checking, name resolution | `Compiler/Semantic/`, `TypeChecker*.cs` |
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
4. **Follow existing patterns** — search codebase for similar code
5. Run tests before/after changes

## Commands

```bash
dotnet build sharpy.sln && dotnet test               # Build + test all
dotnet format whitespace                             # Format before commit
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Debug parser
python3 -c "..."                                     # Verify Python behavior
```

## Feature Implementation Flow

For language features, component experts work in this order:

```
lexer-expert → parser-expert → semantic-expert → codegen-expert → test-expert
```

1. **Lexer** — Add tokens if needed (`Token.cs`, `Lexer.cs`)
2. **Parser** — Add AST nodes (`Ast/*.cs`, `Parser.cs`)
3. **Semantic** — Add type rules (`TypeChecker*.cs`, validators)
4. **CodeGen** — Emit C# via SyntaxFactory (`RoslynEmitter*.cs`)
5. **Tests** — Unit tests + `.spy`/`.expected` integration tests
