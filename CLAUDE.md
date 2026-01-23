# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **See also:** [.github/copilot-instructions.md](.github/copilot-instructions.md) for full architecture and patterns.

## Quick Reference

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # Run all tests
dotnet format whitespace                             # Format before committing
dotnet run --project src/Sharpy.Cli -- run file.spy # Compile and execute
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect generated C#
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Inspect parsed AST
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy  # Inspect lexer tokens
```

## Architecture

```
Source (.spy) → Lexer → Parser (AST) → Semantic → ValidationPipeline → RoslynEmitter → C# → .NET IL
```

| Component | Location | Purpose |
|-----------|----------|---------|
| Compiler | `src/Sharpy.Compiler/` | Lexer, Parser, Semantic, CodeGen |
| Stdlib | `src/Sharpy.Core/` | Runtime library (partial class pattern in `Partial.{Type}/`) |
| CLI | `src/Sharpy.Cli/` | Command-line interface (`sharpyc`) |
| Tests | `src/*.Tests/` | Unit and integration tests |
| Specs | `docs/language_specification/` | Authoritative language specification |

## Critical Rules

1. **Never modify expected values to make tests pass** — fix the implementation
2. **RoslynEmitter uses SyntaxFactory exclusively** — no string templating
3. **Immutable AST** — annotations go in `SemanticInfo`, not AST nodes
4. **Axiom precedence**: .NET > Type Safety > Python Syntax
5. **C# 9.0 target** — no global usings, file-scoped namespaces, or record structs

## Custom Slash Commands

Available in `.claude/commands/`:

| Command | Purpose |
|---------|---------|
| `/project:implement <task>` | Implement a feature end-to-end |
| `/project:review <target>` | Code review (read-only analysis) |
| `/project:plan <feature>` | Decompose complex task into subtasks |
| `/project:test <component>` | Run tests for a component |
| `/project:emit <file.spy>` | Inspect generated C# code |
| `/project:verify-python <expr>` | Verify Python behavior |
| `/project:fix-issue <issue>` | Diagnose and fix a GitHub issue |
| `/project:add-test-fixture <desc>` | Create file-based test |
| `/project:check-axioms <decision>` | Verify axiom compliance |

## Specialized Agents

Domain-specific guidance in `.github/agents/` (20 agents total):

**Implementation Agents:**
- `implementer` — Full implementation + PRs
- `task-planner` — Task decomposition (read-only)
- `code-reviewer` — PR review (read-only)
- `test-expert` — Testing (`*Tests/` edits)

**Compiler Component Experts:**
- `lexer-expert`, `parser-expert`, `semantic-expert`, `codegen-expert`
- `core-library-expert`, `cli-expert`

**Axiom Guardians (Advisory, Read-Only):**
- `net-axiom-guardian` — .NET/C# 9.0 compatibility
- `python-axiom-guardian` — Python syntax fidelity
- `type-safety-guardian` — Static typing, null safety
- `axiom-arbiter` — Conflict resolution
- `unity-compatibility-guardian`, `design-philosophy-guardian`

**Verification Agents (Read-Only):**
- `verification-expert`, `spec-adherence`, `hallucination-defense`, `documentation-sync`

## Testing

```bash
dotnet test --filter "FullyQualifiedName~Lexer"            # By component
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # File-based tests
dotnet test --filter "DisplayName~test_name"               # By test name
```

File-based tests in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`:
- `.spy` + `.expected` pairs for success tests
- `.spy` + `.error` pairs for error tests
- Add `.skip` file to skip a test
