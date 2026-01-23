# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **See also:** [.github/copilot-instructions.md](.github/copilot-instructions.md) for full architecture and patterns.

## Quick Reference

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # Run all tests
dotnet format                                        # Format before committing
dotnet run --project src/Sharpy.Cli -- run file.spy # Compile and execute
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect generated C#
```

## Architecture

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C# → .NET IL
```

- **Compiler**: `src/Sharpy.Compiler/` (Lexer, Parser, Semantic, CodeGen)
- **Stdlib**: `src/Sharpy.Core/` (partial class pattern in `Partial.{Type}/`)
- **CLI**: `src/Sharpy.Cli/`

## Critical Rules

1. **Never modify expected values to make tests pass** — fix the implementation
2. **RoslynEmitter uses SyntaxFactory exclusively** — no string templating
3. **Immutable AST** — annotations go in `SemanticInfo`, not AST nodes
4. **Axiom precedence**: .NET > Type Safety > Python Syntax

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

## Specialized Agents

Domain-specific guidance in `.github/agents/`:

- **Compiler**: `lexer-expert`, `parser-expert`, `semantic-expert`, `codegen-expert`
- **Core**: `implementer`, `code-reviewer`, `task-planner`, `test-expert`
- **Axiom Guardians**: `net-axiom-guardian`, `python-axiom-guardian`, `type-safety-guardian`, `axiom-arbiter`
