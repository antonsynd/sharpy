---
name: implement-plan
description: Implement a verified plan with coordinated agents, lead-owned gates, and mutation-tested guards
argument-hint: "<path/to/plan.md> [--exclude \"section1,section2\"]"
---

Read `docs/design/verification-contract.md` before proceeding; every step below applies it.

Implement a verified plan using coordinated compiler agents. Reads the plan, decomposes it into ordered commit-sized tasks, spawns the right agents, runs the whole-solution gate from the lead, and lands incremental commits whose guards have been shown to fail when broken.

## Argument Handling

Parse `$ARGUMENTS` for:
1. **Plan path** — the first argument (a file path ending in `.md`)
2. **--exclude flag** — optional, comma-separated list of section names to skip

Plans live in `.claude/plans/` (repo-local, gitignored). Per-batch plans created before 2026-08-26 remain in `$HOME/.claude/plans/`. If `$ARGUMENTS` is empty, do **not** pick silently — list the three newest across both directories and ask which to implement:
```bash
ls -t .claude/plans/*.md "$HOME"/.claude/plans/*.md 2>/dev/null | head -3
```

## Pre-Implementation Checklist

Before spawning any agents, perform these checks yourself.

### 1. Read the plan file completely

Note its **Defect Class** and **Adversarial Review** sections (if any), its **Issues to Close** rows with their close criteria, the **blast-radius** line, the **generated artifacts touched** line, and any spec examples it promises. Record the plan's **base sha** — the commit it was verified against (the `@ <sha>` in the stamp) or the commit before its first implementation commit. Every scope check below uses `<base>..HEAD`, never the branch-vs-`mainline` range (on `dev` that spans every batch since the last release).

### 2. Check the verification stamp
- Look for `<!-- Verified by /verify-plan` at the top
- If **absent**: warn the user "This plan has not been verified. Consider running `/verify-plan` first." Ask whether to proceed or stop.
- If **NEEDS REVISION**: stop — "This plan was flagged as needing revision. Please address the issues in the Verification Summary before implementing."
- If **PASS** or **PASS WITH CORRECTIONS**: read the `<!-- Adequacy: CLASS | CELL | N/A -->` line:
  - `CLASS` or `N/A` → proceed
  - `CELL` and any plan input is a bug/ICE/regression issue → **refuse**: "This plan patches a cell, not its class (see the plan's Adequacy verdict). Add a Defect Class section — contract, matrix, standard cure — and re-run `/verify-plan`, or say `override` to implement it as a cell fix knowingly." Proceed only on an explicit override, and record the override in the plan file.
  - Stamp without an Adequacy line (verified before 2026-08-26) → treat as `N/A` for feature plans, and as `CELL` for bug-fix plans unless the plan already has a Defect Class section.

### 3. Report git status
- Run `git status --short`. If there are uncommitted changes, REPORT them to the user and ask whether to proceed — do **not** stash, restore, or clean; the tree may hold a peer's work.

### 4. Check for partially-completed work
- Run `git log --oneline <base>..HEAD` and `git diff <base>...HEAD --stat` to see what the plan has already landed
- Note existing commits per phase so agents don't duplicate work; mark done items in the checklist (§Task Decomposition)

### 5. Establish the baseline (measured, background)
- `.claude/scripts/dotnet-serialized build sharpy.sln` (`dangerouslyDisableSandbox: true`) — if it fails, stop and report the build error
- Start the whole-solution gate **in the background** and continue with read-only setup while it runs (~22 min):
  `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"` (`dangerouslyDisableSandbox: true`, `run_in_background: true`)
- Record it as **`passed/failed/skipped @ <sha> (measured)`** — never a derived number. Read the counts from `.claude/tmp/dotnet-serialized-{0,1,2}.log` (the `-latest.log` symlink can rotate to a peer's run; verify the log's `Test run for` lines name this run).
- If red: **triage before attributing** — check the parent round plan's open-class list (ILCompiles fresh seeds re-roll open classes) and the pre-existing allowlist reds; label each red as `pre-existing (#NNN)` or `unexplained`. Warn the user and ask whether to proceed.

## Team Formation

### 1. Check the harness
Team/task-board tools (`TeamCreate`, `TaskCreate`, `TaskUpdate`, `TaskList`, `TaskGet`, `TeamDelete`) exist only in harnesses that expose them. **Check this session's tool list first.**
- **Present** → create a team `implement-plan`, create tasks with `TaskCreate`, assign via `TaskUpdate`, monitor via `TaskList`.
- **Absent** (the current Claude Code harness) → coordinate with `Agent` (background, `subagent_type` per the table below) + `SendMessage`, and keep the task checklist **in the plan file itself**: add a `## Implementation Checklist` section (one line per task: `- [ ] <task> — <owner> — blocked by <tasks>`) that the lead updates as tasks land. The plan file is the board.

### 2. Choose agents
Determine which agents to spawn based on what the plan touches:

| Role | Agent Type | Spawn When Plan References |
|------|-----------|---------------------------|
| Parser work | `parser-expert` | Lexer/, Parser/, Ast/, TokenType, AST nodes, parsing |
| Semantic work | `semantic-expert` | Semantic/, TypeChecker, NameResolver, TypeResolver, SymbolTable, SemanticInfo, type checking |
| Codegen work | `codegen-expert` | CodeGen/, Lowering/, RoslynEmitter, SyntaxFactory, TypeSyntaxMapper, NameMangler, code generation, emit |
| Core library | `core-library-expert` | Sharpy.Core/, builtins, runtime library, Partial.* |
| Stdlib | `stdlib-expert` | Sharpy.Stdlib/, spy/ sources, modules |
| LSP | `lsp-expert` | Sharpy.Lsp/, handlers, hover/completion/semantic tokens |
| Tests | `test-expert` | Always spawned — every plan needs tests, and every guard needs its mutation step |

`verification-expert` is **not** a teammate (it has no `SendMessage`/task tools — `agents.md` › Teammate Compatibility). The lead spawns it **standalone** at the end (§Final Verification) and reads its report. For any refutation brief (regression control run, sibling-cell probing) pass `model` explicitly at spawn time — its default `haiku` is for "run this filter and report counts" only.

Spawn teammates with `run_in_background`; give each its task text, the relevant plan section, and the Agent Instructions block below. Never run more than one agent that needs the same files.

## Task Decomposition

Break the plan into commit-sized tasks (via `TaskCreate` or the plan-file checklist). Rules:

1. **Ordering**: feature implementation order: Lexer → Parser → Semantic → Validation → Lowering → CodeGen → LSP → Tests
2. **Dependencies**: block Parser on Lexer, Semantic on Parser, etc. (`addBlockedBy` or the checklist's `blocked by`)
3. **Granularity**: one logical commit per task (e.g., "Add TokenType.FormatString to Lexer", "Add FormatStringExpression AST node", "Handle FormatStringExpression in TypeChecker")
4. **Test tasks**: alongside or immediately after each implementation task, never all at the end; each test task includes its **mutation step** (§Guard Delivery Rule)
5. **Class tasks** — if the plan has a **Defect Class** section: create the **matrix/sweep task** (the sibling cells the plan enumerated go green, or the standing harness gains the cells) and the **allowlist-drain task** (every entry the fix drains is deleted, stale entries fail), both blocked by the fix tasks. A bug-fix plan with a Defect Class section but no matrix task is incomplete — add it.
6. **Regen task** — immediately after the **first** task that touches `Semantic/`, `Lowering/`, or `CodeGen/`, not at the end: regenerate spy-stdlib C# (`build_tools/regenerate_spy_stdlib.sh`), spy-test C#, stdlib docs (`build_tools stdlib generate --force`), and the oracle ledger (`/regenerate-snapshots` covers the C# snapshots). Read the regen diff as **findings**: a newly red regenerated test is a live regression until disproven.
7. **Excluded sections**: skip any sections listed in `--exclude`
8. **Final tasks**: a "Run final verification" task (lead + standalone `verification-expert`) and a "Run dotnet format whitespace" task, both blocked by all implementation tasks

## Implementation Workflow

Assign tasks (via `TaskUpdate` `owner`, or by naming the owner on the checklist line) and monitor (via `TaskList`, or by re-reading the checklist and the agents' reports). After **each agent wave**: run `git diff --stat` and `git status --short`, compare against the wave's declared scope, and commit the lead's own work in small slices. A working-tree delta nobody claims is a finding — ask before touching it.

### Agent Instructions

Each agent receives these instructions along with its specific task:

```
You are implementing part of a plan. Your task is described below along with the relevant plan section.

CRITICAL RULES:
- Never modify .expected files to make tests pass — fix the implementation
- RoslynEmitter uses SyntaxFactory exclusively — no string templating, no type or lowering decisions, no reflection; facts it reads are materialized in Symbol.CodeGenInfo or a node-keyed SemanticInfo dictionary (which MUST be added to SemanticInfo.MergeFrom)
- Immutable AST — annotations go in SemanticInfo, not AST nodes
- Axiom precedence: .NET > Type Safety > Python Syntax
- C# 9.0 on netstandard2.1 for Sharpy.Core/Sharpy.Stdlib and for generated code
- Verify Python behavior with `python3 -c "..."` before implementing Python semantics
- Language spec is authoritative — check docs/language_specification/ before implementing; spec examples you add are executed before commit
- TODO/BUG/FIXME comments must reference GitHub issues (create the issue first)
- Fix the class, not the cell: when you discover a sibling cell of the defect you are fixing, FILE the issue AND add it to the plan's Defect Class table — never spot-fix it silently

SHARED TREE (verbatim from docs/design/verification-contract.md §9):
The working tree is shared with other agents. Never run `git checkout`, `git restore`,
`git clean`, `git stash`, `git reset`, or `rm` on repository paths. REPORT `git status`; do not
"make it clean". Stage with explicit per-file pathspecs and check `git diff --cached --stat`
before committing; never `git add -A` or `git add .`. Restore a mutation-test from the copy you
made (`cp`), never from git. Never run `dotnet` directly — use `.claude/scripts/dotnet-serialized`
with `dangerouslyDisableSandbox: true`.

SERIALIZED DOTNET:
The wrapper takes an exclusive lock so only one dotnet process runs at a time; concurrent test runs
consume 5–10 GB RAM EACH. It is a drop-in replacement (same args, output, exit code) and tees output
to `.claude/tmp/dotnet-serialized-{0,1,2}.log` (`-latest.log` symlinks the newest run — it may be a
peer's). A whole-solution run is ~22 min; read a recent log before re-running:
  grep -i "FAIL\|error" .claude/tmp/dotnet-serialized-latest.log

WORKFLOW:
1. Read the plan section for your task
2. Read existing code patterns in the area you're modifying
3. Write tests first or alongside implementation (not after)
4. Implement the changes
5. Edit loop: component-specific tests only —
   `.claude/scripts/dotnet-serialized test --filter "FullyQualifiedName~[Component]" --no-build`
   Do NOT run the whole-solution suite yourself: the lead runs it once per wave in the background.
   If your change touches Semantic/, Lowering/, or CodeGen/, also run the project the plan's
   blast-radius line names for it (e.g. `FullyQualifiedName~Stdlib`), still filtered.
6. Guard delivery (if your task adds a test/guard/harness): make a copy of the production file
   (`cp`), break the guarded thing (invert the predicate or revert the production hunk), run the
   test — it must go RED; restore from the copy; run again — GREEN. Record both counters in the
   commit body: "mutation: broken → red (N failed), restored → green". If it stays green when
   broken, do NOT ship it as a guard — report it as a finding to the lead. An absence assertion
   needs a positive control on the same input.
7. Run `dotnet format whitespace`
8. Stage ONLY the specific files you changed by explicit pathspec; check `git diff --cached --stat`
9. Commit with a descriptive message referencing the plan section; use the commit trailer(s) the
   harness provides for this session
10. Report: what landed (commit hash), the mutation outcomes, any sibling cells filed, `git status`
    (mark your task completed via TaskUpdate if the harness has it)
```

### Gap Discovery

During implementation, if agents discover:
- **Tech debt**: create a GitHub issue with `gh issue create --title "..." --body "..."` (`dangerouslyDisableSandbox: true`; check for duplicates first with `gh issue list --search "..."`)
- **Bugs / sibling cells**: create a GitHub issue and add it to the plan's Defect Class table; add a `// BUG(#NNN): ...` comment only where a workaround remains in code
- **Missing features**: create a GitHub issue and add a `// TODO(#NNN): ...` comment
- Every TODO/FIXME/BUG comment MUST reference an issue number

### Guard Delivery Rule

Any task that adds a test, guard, or harness ships with its **mutation step** and reports both outcomes in the commit body (`broken → red (N failed), restored → green`). Restore via the `cp` copy, never via git. A guard that stays green when broken is a **finding**, not a deliverable: the lead decides whether the fix is inert (fallback path one call later), the exemption is the subject (parameterize the one falsifiable arm), or the defect does not reproduce — and files or widens accordingly. Absence assertions carry a positive control.

### Incremental Commits

After each task is completed by an agent:
1. Verify the agent staged only relevant files (`git show --stat HEAD`)
2. The commit message references the plan section, e.g. `feat: Add FormatStringExpression AST node (plan step 2)`, and — for guard commits — carries the mutation outcomes
3. Use the commit trailer(s) the harness provides for the session (`Co-Authored-By:` / `Claude-Session:`); never hard-code a model name
4. Update the checklist (or the task board) and re-check `git diff --stat` against the wave's scope

## Final Verification

After all implementation tasks are complete, the lead runs the gate and spawns `verification-expert` standalone (pass `model` explicitly; it refutes, it does not confirm):

1. `.claude/scripts/dotnet-serialized build sharpy.sln` — must succeed
2. **Whole-solution gate** in the background: `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --no-build` — compare against the baseline `@ <sha> (measured)`; every new red gets a **control run** at the plan's base sha (a `git worktree` there) before it is attributed
3. **Blast-radius sweeps** the plan named: `FrontEndParityTests` (LSP parity), and the GapDiscovery sweeps CI runs as separate steps — `--filter "Category=GapDiscovery&FullyQualifiedName~InteropConformance"`, `~MetamorphicCorpus`, `~DifferentialExecution` — for any change under `Semantic/`, `Lowering/`, or `CodeGen/`
4. **Staleness gates**: run `/push`'s generated-artifact gates (spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger) — a diff here is a finding, not noise
5. **Spec examples**: execute every example the plan promised in `docs/language_specification/` or `DiagnosticExplanations` (`/spy-run`); outputs must discriminate
6. **Allowlists**: every entry the plan's fixes drain is gone; no entry was added without an issue reference
7. `dotnet format whitespace` — no changes needed
8. `git diff --stat` and `git status --short` re-check after the last agent wave — every delta is claimed by a commit or an agent report

## Cleanup and Report

After final verification:

1. If a team exists: shut down teammates via `SendMessage` (`type: "shutdown_request"`) and delete the team via `TeamDelete`; otherwise confirm every background agent has reported
2. Mark the plan's Implementation Checklist complete (or list what was deferred and why)
3. Present a summary report to the user:

```markdown
## Implementation Summary

**Plan:** [plan file path] · **Adequacy:** [CLASS / CELL (override) / N/A]
**Branch:** [current branch] · **Scope:** [base sha]..[HEAD sha] · **Commits:** [count]

### What Was Done
- (each completed task with commit hash)

### Guards mutation-tested
- N red-when-broken · M vacuous → [filed as #… / fixed in <hash>]

### Sibling cells found
- (each cell → issue number, added to the plan's Defect Class table)

### What Was Deferred
- (items deferred with GitHub issue numbers)

### Test Results
- **Baseline:** X passed, Y failed, Z skipped @ <sha> (measured)
- **Final:** X passed, Y failed, Z skipped @ <sha> (measured)
- **Control runs:** (each attributed red → result at base sha)
- **Sweeps run:** (parity / interop / metamorphic / differential — pass/fail @ sha)
- **Allowlist delta:** (entries drained / added-with-issue)
- **New tests added:** N

### Files Changed
(output of `git diff <base>...HEAD --stat`)
```
