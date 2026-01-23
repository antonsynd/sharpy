# Custom Agents

Specialized agents for Sharpy development. Each agent has domain expertise and clear boundaries.

> **See also:** [copilot-instructions.md](copilot-instructions.md) for full architecture and patterns.

## Quick Reference

| Agent | Domain | Edits |
|-------|--------|-------|
| `implementer` | Full implementation + PRs | All |
| `code-reviewer` | PR review | Read-only |
| `task-planner` | Task decomposition | Read-only |
| `test-expert` | Testing | `*Tests/` |
| `lexer-expert` | Tokenization | `Compiler/Lexer/` |
| `parser-expert` | AST construction | `Compiler/Parser/` |
| `semantic-expert` | Type checking | `Compiler/Semantic/` |
| `codegen-expert` | C# emission | `Compiler/CodeGen/` |
| `core-library-expert` | Stdlib | `Sharpy.Core/` |
| `cli-expert` | CLI | `Sharpy.Cli/` |

### Axiom Guardians (Advisory, Read-Only)

| Agent | Guards |
|-------|--------|
| `net-axiom-guardian` | Axiom 1: .NET/C# 9.0 compatibility |
| `python-axiom-guardian` | Axiom 2: Python syntax fidelity |
| `type-safety-guardian` | Axiom 3: Static typing |
| `axiom-arbiter` | Conflict resolution |

## Key Rules

1. **Never artificially make tests pass** — fix bugs instead
2. **Axiom precedence**: .NET > Type Safety > Python Syntax
3. Run tests before/after changes
4. Follow existing code patterns

## Commands

```bash
dotnet build sharpy.sln                              # Build
dotnet test                                          # Test
dotnet format                                        # Format
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
python3 -c "..."                                     # Verify Python behavior
```
