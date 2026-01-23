# Custom Agents

Specialized agents for Sharpy development. Each agent has domain expertise and clear boundaries.

> **See also:** [copilot-instructions.md](copilot-instructions.md) for architecture and patterns.

## Quick Reference

### Implementation Agents

| Agent | Domain | Edits |
|-------|--------|-------|
| `implementer` | Full implementation + PRs | All |
| `task-planner` | Task decomposition | Read-only |
| `code-reviewer` | PR review | Read-only |
| `test-expert` | Testing | `*Tests/` |

### Compiler Component Experts

| Agent | Component | Edits |
|-------|-----------|-------|
| `lexer-expert` | Tokenization | `Compiler/Lexer/` |
| `parser-expert` | AST construction | `Compiler/Parser/` |
| `semantic-expert` | Type checking, name resolution | `Compiler/Semantic/` |
| `codegen-expert` | C# emission via Roslyn | `Compiler/CodeGen/` |
| `core-library-expert` | Standard library | `Sharpy.Core/` |
| `cli-expert` | CLI commands | `Sharpy.Cli/` |

### Axiom Guardians (Advisory, Read-Only)

| Agent | Guards |
|-------|--------|
| `net-axiom-guardian` | Axiom 1: .NET/C# 9.0 compatibility |
| `python-axiom-guardian` | Axiom 2: Python syntax fidelity |
| `type-safety-guardian` | Axiom 3: Static typing, null safety |
| `axiom-arbiter` | Conflict resolution between axioms |
| `unity-compatibility-guardian` | Unity/IL2CPP compatibility |
| `design-philosophy-guardian` | Developer happiness, zero-overhead |

### Verification Agents (Read-Only)

| Agent | Purpose |
|-------|---------|
| `verification-expert` | Runs tests, produces reports |
| `spec-adherence` | Verifies impl matches spec |
| `hallucination-defense` | Fact-checks .NET/Python/Roslyn claims |
| `documentation-sync` | Keeps docs synchronized |

## Key Rules

1. **Never artificially make tests pass** — fix bugs instead
2. **Axiom precedence**: .NET > Type Safety > Python Syntax
3. Run tests before/after changes
4. Follow existing code patterns

## Commands

```bash
dotnet build sharpy.sln && dotnet test               # Build + test
dotnet format                                         # Format before commit
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
python3 -c "..."                                     # Verify Python behavior
```
