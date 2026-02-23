---
name: implement-plan
description: Implement a plan with a coordinated agent team
argument-hint: "<path/to/plan.md> [--exclude \"section1,section2\"]"
---

Implement a verified plan using a coordinated team of compiler agents. Reads the plan, decomposes it into ordered tasks, spawns appropriate agents, and implements with incremental commits and test-driven development.

## Argument Handling

Parse `$ARGUMENTS` for:
1. **Plan path** — the first argument (a file path ending in `.md`)
2. **--exclude flag** — optional, comma-separated list of section names to skip

If `$ARGUMENTS` is empty, find the most recently modified `.md` file in `~/.claude/plans/`:
```bash
ls -t ~/.claude/plans/*.md | head -1
```

## Pre-Implementation Checklist

Before spawning any agents, perform these checks yourself:

### 1. Read the plan file completely

### 2. Check for verification stamp
- Look for `<!-- Verified by /verify-plan` at the top
- If **absent**: warn the user "This plan has not been verified. Consider running `/verify-plan` first." Ask whether to proceed or stop.
- If stamp says **NEEDS REVISION**: stop and tell the user "This plan was flagged as needing revision. Please address the issues in the Verification Summary before implementing."
- If stamp says **PASS** or **PASS WITH CORRECTIONS**: proceed

### 3. Check git status
- Run `git status` — if there are uncommitted changes, warn the user and ask whether to proceed or stash first

### 4. Check for partially-completed work
- Run `git diff mainline...HEAD --stat` to see what's already been changed on this branch
- If there are existing changes, note them so agents don't duplicate work

### 5. Establish green baseline
- Run `dotnet build sharpy.sln` — if it fails, stop and report the build error
- Run `dotnet test` — record the baseline (pass/fail/skip counts). If tests fail, warn the user and ask whether to proceed

## Team Formation

Create a team using `TeamCreate` with name `implement-plan`.

Determine which agents to spawn based on what the plan touches. Read the plan and check for references to these areas:

| Role | Agent Type | Spawn When Plan References |
|------|-----------|---------------------------|
| Parser work | `parser-expert` | Lexer/, Parser/, Ast/, TokenType, AST nodes, parsing |
| Semantic work | `semantic-expert` | Semantic/, TypeChecker, NameResolver, TypeResolver, SymbolTable, SemanticInfo, type checking |
| Codegen work | `codegen-expert` | CodeGen/, RoslynEmitter, SyntaxFactory, TypeMapper, NameMangler, code generation, emit |
| Core library | `core-library-expert` | Sharpy.Core/, stdlib, builtins, runtime library, Partial.* |
| Tests | `test-expert` | Always spawned — every plan needs tests |
| Final verification | `verification-expert` | Always spawned — final pass after implementation |

## Task Decomposition

Break the plan into commit-sized tasks using `TaskCreate`. Follow these rules:

1. **Ordering**: Tasks must follow the feature implementation order: Lexer -> Parser -> Semantic -> Validation -> CodeGen -> Tests
2. **Dependencies**: Use `addBlockedBy` to enforce ordering — Parser tasks block on Lexer tasks, Semantic blocks on Parser, etc.
3. **Granularity**: Each task should be one logical commit (e.g., "Add TokenType.FormatString to Lexer", "Add FormatStringExpression AST node", "Handle FormatStringExpression in TypeChecker")
4. **Test tasks**: Create test tasks alongside or immediately after each implementation task, not all at the end
5. **Excluded sections**: Skip any sections listed in the `--exclude` flag
6. **Final tasks**: Always create a "Run final verification" task (assigned to `verification-expert`) and a "Run dotnet format whitespace" task, both blocked by all implementation tasks

## Implementation Workflow

Assign tasks to agents via `TaskUpdate` with the `owner` field. Monitor progress via `TaskList`.

### Agent Instructions

Each agent receives these instructions along with their specific task:

```
You are implementing part of a plan. Your task is described below along with the relevant plan section.

CRITICAL RULES:
- Never modify .expected files to make tests pass — fix the implementation
- RoslynEmitter uses SyntaxFactory exclusively — no string templating
- Immutable AST — annotations go in SemanticInfo, not AST nodes
- Axiom precedence: .NET > Type Safety > Python Syntax
- C# 9.0 for Sharpy.Core and generated code only
- Verify Python behavior with `python3 -c "..."` before implementing Python semantics
- Language spec is authoritative — check docs/language_specification/ before implementing
- TODO/BUG/FIXME comments must reference GitHub issues

WORKFLOW:
1. Read the plan section for your task
2. Read existing code patterns in the area you're modifying
3. Write tests first or alongside implementation (not after)
4. Implement the changes
5. Run component-specific tests: `dotnet test --filter "FullyQualifiedName~[Component]"`
6. Run the full test suite: `dotnet test`
7. Run `dotnet format whitespace`
8. Stage ONLY the specific files you changed (never `git add -A` or `git add .`)
9. Commit with a descriptive message referencing the plan section
10. Mark your task as completed via TaskUpdate
```

### Gap Discovery

During implementation, if agents discover:
- **Tech debt**: Create a GitHub issue with `gh issue create --title "..." --body "..."` (check for duplicates first with `gh issue list --search "..."`)
- **Bugs**: Create a GitHub issue and add a `// BUG(#NNN): ...` comment
- **Missing features**: Create a GitHub issue and add a `// TODO(#NNN): ...` comment
- Every TODO/FIXME/BUG comment MUST reference an issue number

### Incremental Commits

After each task is completed by an agent:
1. Verify the agent staged only relevant files
2. The commit message should be descriptive and reference the plan section, e.g.: `feat: Add FormatStringExpression AST node (plan step 2)`
3. Include `Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>` in the commit

## Final Verification

After all implementation tasks are complete, the `verification-expert` runs:

1. `dotnet build sharpy.sln` — must succeed
2. `dotnet test` — compare against baseline (no new failures)
3. File-based integration tests: `dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`
4. Sample `emit csharp` on representative .spy files: pick 3-5 from test fixtures
5. `dotnet format whitespace` — verify no changes needed (clean working tree)
6. `git diff --stat` — summarize all changes made

## Cleanup and Report

After final verification:

1. Shut down all teammates via `SendMessage` with `type: "shutdown_request"`
2. Delete the team via `TeamDelete`
3. Present a summary report to the user:

```markdown
## Implementation Summary

**Plan:** [plan file path]
**Branch:** [current branch]
**Commits:** [count]

### What Was Done
- (list each completed task with commit hash)

### What Was Deferred
- (list any items deferred with GitHub issue numbers)

### Test Results
- **Baseline:** X passed, Y failed, Z skipped
- **Final:** X passed, Y failed, Z skipped
- **New tests added:** N

### Files Changed
(output of `git diff mainline...HEAD --stat`)
```
