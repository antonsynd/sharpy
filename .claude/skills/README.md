# Skills Quick Reference

All skills are invoked with `/{skill-name}` (or the path prefix if organized in subdirectories).

## Test & Build Skills (Smart Truncation + Logging)

All skills below capture full output to `.claude/tmp/*.log` while showing truncated output:

| Skill | Usage | Log File | On Failure Shows |
|-------|-------|----------|------------------|
| `/run-tests` [filter] | Run tests with optional filter | `last-test-run.log` | Last 80 lines |
| `/build` | Build solution | `last-build.log` | Last 100 lines |
| `/build-verbose` | Build with diagnostics | `last-build-verbose.log` | Last 100 lines |
| `/format` | Format whitespace | `last-format.log` | Last 50 lines |
| `/regenerate-snapshots` | Update `.expected.cs` files | `last-snapshot-regen.log` | Last 80 lines |
| `/test-fixture` `<name>` | Run specific test | `last-test-fixture.log` | Last 80 lines |

## Debug/Development Skills

| Skill | Usage | Log File | Notes |
|-------|-------|----------|-------|
| `/spy-emit-csharp` `<file.spy>` | Emit generated C# | `last-spy-emit.log` | Shows last 100 lines |
| `/spy-emit-ast` `<file.spy>` | Emit parsed AST | `last-spy-emit.log` | Shows last 100 lines |
| `/spy-emit-tokens` `<file.spy>` | Emit lexer tokens | `last-spy-emit.log` | Shows last 100 lines |
| `/spy-run` `<file.spy>` | Execute .spy file | `last-spy-run.log` | Shows full output on success |

## Existing Skills (unchanged)

- `/compiler-audit` [focus-area] - Comprehensive compiler health audit
- `/verify-plan` `<plan.md>` - Verify a plan for accuracy
- `/verify-implementation` `<plan.md>` - Verify implementation, fix gaps
- `/add-test-fixture` `<desc>` - Create file-based integration test
- `/dogfood-analyze` [dir] - Analyze dogfood results
- `/dogfood-run` - Run dogfooding iterations
- `/verify-python` `<expr>` - Verify Python behavior

## Investigating Failures

When a skill fails, use the full log for deeper investigation without re-running:

```bash
# Read the full log (use the Read tool or cat)
cat .claude/tmp/last-test-run.log

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
