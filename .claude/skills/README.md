# Skills Quick Reference

All skills are invoked with `/{skill-name}`; sources live in `.claude/skills/<name>/SKILL.md` (this table is generated from their frontmatter — regenerate it when a skill directory is added or removed). Process rules every workflow skill applies: `docs/design/verification-contract.md`.

## dotnet execution and logs

Every skill that runs `dotnet` goes through `.claude/scripts/dotnet-serialized` (exclusive lock — concurrent test runs OOM the machine; a PreToolUse hook blocks unwrapped `dotnet` build/test/run; the wrapper needs `dangerouslyDisableSandbox: true`). The wrapper tees stdout+stderr to `.claude/tmp/dotnet-serialized-{0,1,2}.log` (3-slot rotation) with `.claude/tmp/dotnet-serialized-latest.log` symlinked to the newest run. Read those logs instead of re-running a suite (~22 min wall clock); note `-latest.log` can rotate to a peer's run.

## Build & Test

| Skill | Arguments | Purpose |
|-------|-----------|---------|
| `/build` | — | Build the Sharpy solution with smart output truncation |
| `/run-tests` | `[filter]` | Run Sharpy tests — summary on success, last 80 lines on failure, full log saved |
| `/format` | — | Format code whitespace per project conventions (also auto-formatted on save by Claude hook) |
| `/regenerate-snapshots` | — | Regenerate C# snapshot tests and spy stdlib after intentional codegen changes |
| `/gap-analysis` | — | Run all gap discovery tests and present a unified summary |
| `/property-stress` | `[rounds=10] [filter]` | Stress-test property tests across many rounds with fresh random seeds |
| `/benchmark` | — | Run compiler or cross-language benchmarks and compare results |
| `/clean-dotnet` | — | Kill zombie dotnet processes and clean build artifacts (kill specific PIDs, never blanket `pkill`) |

## Debug & Development

| Skill | Arguments | Purpose |
|-------|-----------|---------|
| `/spy-emit` | `<mode> <file.spy or inline source>` | Emit compiler output (`csharp`, `ast`, `tokens`, `diagnostics`) — SPY0908 never shows here |
| `/spy-run` | `<file.spy or inline source>` | Compile and run a Sharpy source file or inline source (the only command that surfaces SPY0908) |
| `/quick-check` | `<file.spy or inline source>` | Emit C# and run a .spy file or inline source in one shot |
| `/verify-python` | `<expression or code>` | Run a Python 3 expression or snippet to verify behavior before implementing Sharpy semantics |
| `/lsp-hover` | `<file.spy> <line> <col>` | Get LSP hover tooltip for a position in a .spy file (emulates VS Code hover) |
| `/lsp-review` | `[fixture path or directory]` | Interactive LSP review session — user reports hover/coloring issues, Claude files GitHub issues |
| `/playground` | — | Run the Sharpy playground (Blazor WASM) locally with hot reload |

The `/spy-*` and `/quick-check` skills accept inline source via the Write tool, avoiding bash-escaping issues with `#` and backticks.

## Scaffolding

| Skill | Arguments | Purpose |
|-------|-----------|---------|
| `/add-test-fixture` | `<description of test>` | Create a file-based integration test (.spy + .expected/.error pair) |
| `/add-stdlib-module` | — | Scaffold a new Sharpy.Stdlib module (spy-sourced or handwritten C#) with all required files, conventions, docs, and tests |

## Git & Release

| Skill | Arguments | Purpose |
|-------|-----------|---------|
| `/commit` | `[optional commit message override]` | Stage and commit current changes with an auto-generated message |
| `/push` | `[--close-issues 123,456]` | Push current branch to remote origin (runs the generated-artifact staleness gates CI enforces) |
| `/bump-version` | `[--apply] [--major\|--minor\|--patch]` | Suggest and apply a semver version bump based on commits since the last tag |
| `/close-issues` | `<plan.md or issue numbers: 123,456>` | Close GitHub issues that have been implemented, with verification |

## Planning & Verification

Plans live in `.claude/plans/` (repo-local, gitignored); per-batch plans created before 2026-08-26 remain in `~/.claude/plans/` — pass the path explicitly for those.

| Skill | Arguments | Purpose |
|-------|-----------|---------|
| `/create-plan` | `<issue numbers or description>` | Create an implementation plan from GitHub issues or a description |
| `/verify-plan` | `<path/to/plan.md>` | Verify a plan for accuracy and architectural soundness |
| `/implement-plan` | `<path/to/plan.md> [--exclude "section1,section2"]` | Implement a verified plan with coordinated agents, lead-owned gates, and mutation-tested guards |
| `/verify-implementation` | `<path/to/plan.md>` | Verify completed plan implementation, fix gaps/bugs/regressions, and commit fixes |
| `/compiler-audit` | `[focus-area]` | Run a comprehensive compiler health audit |

## Dogfooding

| Skill | Arguments | Purpose |
|-------|-----------|---------|
| `/dogfood-run` | `[number_of_iterations]` | Run dogfooding iterations to test the Sharpy compiler |
| `/dogfood-analyze` | `[directory_name]` | Analyze dogfood results and classify failures by root cause |

## Investigating Failures

```bash
grep -n "Failed\|error" .claude/tmp/dotnet-serialized-latest.log   # newest run (may be a peer's)
ls -lt .claude/tmp/dotnet-serialized-*.log                          # pick the slot by mtime
```

A log proves a green only if its binaries postdate the change under test — check the `Test run for` lines against the commit.

## Skill Structure

Skills are directories with a `SKILL.md` containing YAML frontmatter (`name`, `description`, optional `argument-hint`, optional `disable-model-invocation: true`) followed by the instructions the skill executes.
