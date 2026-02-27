# Skills Quick Reference

All skills are invoked with `/{skill-name}`.

## Build & Test (Smart Truncation + Logging)

All skills below capture full output to `.claude/tmp/*.log` while showing truncated output. Test skills auto-build before running.

| Skill | Usage | Log File | On Failure Shows |
|-------|-------|----------|------------------|
| `/build` | Build solution | `last-build.log` | Last 100 lines |
| `/build-verbose` | Build with diagnostics | `last-build-verbose.log` | Last 100 lines |
| `/run-tests` [filter] | Build + run tests with optional filter | `last-test-run.log` | Last 80 lines |
| `/test-fixture` `<name>` | Build + run specific test | `last-test-fixture.log` | Last 80 lines |
| `/format` | Format whitespace | `last-format.log` | Last 50 lines |
| `/regenerate-snapshots` | Build + update `.expected.cs` files | `last-snapshot-regen.log` | Last 80 lines |

## Debug & Development

| Skill | Usage | Log File | Notes |
|-------|-------|----------|-------|
| `/spy-emit-csharp` `<file.spy>` | Emit generated C# | `last-spy-emit-csharp.log` | Shows last 100 lines |
| `/spy-emit-ast` `<file.spy>` | Emit parsed AST | `last-spy-emit-ast.log` | Shows last 100 lines |
| `/spy-emit-tokens` `<file.spy>` | Emit lexer tokens | `last-spy-emit-tokens.log` | Shows last 100 lines |
| `/spy-run` `<file.spy>` | Execute .spy file | `last-spy-run.log` | Shows full output on success |
| `/spy-project` `<.spyproj>` | Run multi-file project | `last-spy-project.log` | Shows full output on success |
| `/verify-python` `<expr>` | Verify Python behavior | — | Direct execution |
| `/clean-dotnet` | Kill zombie dotnet processes | — | Use when builds hang |

## Git Workflow

| Skill | Usage | Notes |
|-------|-------|-------|
| `/commit` [message] | Stage and commit changes | Auto-generates message if none provided |
| `/push` [--close-issues N,N] | Push current branch | Optionally close GitHub issues |

## Analysis & Planning

| Skill | Usage | Notes |
|-------|-------|-------|
| `/compiler-audit` [focus-area] | Comprehensive compiler health audit | Spawns parallel read-only agents |
| `/create-plan` `<issues or desc>` | Create implementation plan | From GitHub issues or description |
| `/verify-plan` `<plan.md>` | Verify plan accuracy against codebase | Adds verification stamp |
| `/implement-plan` `<plan.md>` | Implement plan with agent team | Incremental commits |
| `/verify-implementation` `<plan.md>` | Verify implementation, fix gaps | Post-implementation audit |
| `/add-test-fixture` `<desc>` | Create file-based integration test | .spy + .expected/.error pair |
| `/dogfood-run` [N] | Run dogfooding iterations | Defaults to 5 iterations |
| `/dogfood-analyze` [dir] | Analyze dogfood results | Classifies failures C1-C5 |

## Investigating Failures

When a skill fails, use the full log for deeper investigation without re-running:

```bash
# Read the full log (use the Read tool)
/read .claude/tmp/last-test-run.log

# Search for specific patterns
grep "Exception" .claude/tmp/last-test-run.log
grep "error" .claude/tmp/last-build.log
```

## Log File Cleanup

Logs are overwritten on each skill run. To clean up:

```bash
rm -rf .claude/tmp/*.log
```

## Skill Structure

Skills are implemented as directories with `SKILL.md` files containing:
- YAML frontmatter with `name`, `description`, and optional `argument-hint`
- Documentation for usage and expected behavior
- Optional `disable-model-invocation: true` to prevent automatic invocation
