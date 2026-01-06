# Custom Agents

Specialized agents for Sharpy development. Each agent has domain expertise and clear boundaries.

## Quick Reference

### Core Agents

| Agent | File | Domain |
|-------|------|--------|
| Implementer | `implementer.agent.md` | Full implementation + PRs |
| Code Reviewer | `code-reviewer.agent.md` | PR review (read-only) |
| Task Planner | `task-planner.agent.md` | Task decomposition |
| Verification Expert | `verification-expert.agent.md` | Read-only verification |

### Compiler Pipeline

| Agent | File | Domain |
|-------|------|--------|
| Lexer Expert | `lexer-expert.agent.md` | Tokenization |
| Parser Expert | `parser-expert.agent.md` | AST construction |
| Semantic Expert | `semantic-expert.agent.md` | Type checking |
| CodeGen Expert | `codegen-expert.agent.md` | C# emission |

### Library & CLI

| Agent | File | Domain |
|-------|------|--------|
| Core Library Expert | `core-library-expert.agent.md` | Sharpy.Core stdlib |
| CLI Expert | `cli-expert.agent.md` | sharpyc CLI |
| Test Expert | `test-expert.agent.md` | xUnit tests |

### Axiom Guardians (Advisory)

| Agent | File | Guards |
|-------|------|--------|
| .NET Axiom Guardian | `net-axiom-guardian.agent.md` | Axiom 1: .NET/C# 9.0 |
| Python Axiom Guardian | `python-axiom-guardian.agent.md` | Axiom 2: Python syntax |
| Type Safety Guardian | `type-safety-guardian.agent.md` | Axiom 3: Static typing |
| Unity Compatibility Guardian | `unity-compatibility-guardian.agent.md` | Unity constraints |
| Axiom Arbiter | `axiom-arbiter.agent.md` | Conflict resolution |
| Design Philosophy Guardian | `design-philosophy-guardian.agent.md` | Overall design |

### Quality & Compliance (Advisory)

| Agent | File | Domain |
|-------|------|--------|
| Spec Adherence | `spec-adherence.agent.md` | Spec compliance |
| Hallucination Defense | `hallucination-defense.agent.md` | Fact-checking |
| Documentation Sync | `documentation-sync.agent.md` | Doc freshness |

## Agent Boundaries

### Read-Only Agents
These agents analyze but never modify code:
- `code-reviewer`, `verification-expert`, `spec-adherence`, `hallucination-defense`
- All axiom guardians

### Domain-Specific Editors
These agents only modify their owned directories:
- `lexer-expert` → `src/Sharpy.Compiler/Lexer/`
- `parser-expert` → `src/Sharpy.Compiler/Parser/`
- `semantic-expert` → `src/Sharpy.Compiler/Semantic/`
- `codegen-expert` → `src/Sharpy.Compiler/CodeGen/`
- `core-library-expert` → `src/Sharpy.Core/`
- `cli-expert` → `src/Sharpy.Cli/`

### Cross-Cutting Agents
- `implementer` — Full codebase access for features
- `task-planner` — Coordinates specialists
- `test-expert` — Tests for all components
- `documentation-sync` — Docs for all components

## Key Rules

All agents must:
- **Never artificially make tests pass** — fix bugs instead
- Run tests before/after changes
- Follow existing code patterns
- Reference language spec when implementing features

## Axiom Precedence

When axioms conflict: **Axiom 1 > Axiom 3 > Axiom 2**
- .NET compatibility > Type safety > Python syntax
- Unless resolved at zero cost

## Common Commands

```bash
dotnet build sharpy.sln          # Build all
dotnet test                       # Run all tests
dotnet format                     # Format before committing

# Filtered tests
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "FullyQualifiedName~CodeGen"

# Verify Python behavior
python3 -c "print([1,2,3].pop())"
```
