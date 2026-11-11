---
name: implement-plan
description: Implement a verified plan with coordinated agents, lead-owned gates, and mutation-tested guards
argument-hint: "<path/to/plan.md> [--exclude \"section1,section2\"]"
---

Read `docs/design/verification-contract.md` before proceeding (§10 file-by-class, §11 one-group-per-plan and §12 the verification tier included); every step below applies it.

Implement a plan — a `/verify-plan`-stamped one at `full` tier, or a light design-of-record carrying the `Adequacy-only` stamp and a clean `check_plan_refs.py` report (contract §12) — using coordinated compiler agents. On a light plan the lead writes the task decomposition itself from the plan's acceptance bullets (§Task Decomposition); the plan has no task list by design.

## Argument Handling

Parse `$ARGUMENTS` for:
1. **Plan path** — the first argument (a file path ending in `.md`)
2. **--exclude flag** — optional, comma-separated list of section names to skip

The plan is the file of record `docs/process/plans/<plan-id>.md` (`docs/process/README.md` › The stage trail); presence is not a verdict — Pre-Implementation §2 reads line 1. If `$ARGUMENTS` is empty, list the three newest (`ls -t docs/process/plans/*.md | head -3`) and ask which — never pick silently.

## Pre-Implementation Checklist

Before spawning any agents, perform these checks yourself.

### 1. Read the plan file completely

Note its **Defect Class** and **Adversarial Review** sections (if any), its **Issues to Close** rows with their close criteria, the **blast-radius** line, the **generated artifacts touched** line, and any spec examples it promises. Record the plan's **base sha** — the commit it was verified against (the `@ <sha>` in the stamp) or the commit before its first implementation commit. Every scope check below uses `<base>..HEAD`, never the branch-vs-`mainline` range (on `dev` that spans every batch since the last release).

### 2. Check the tier and the verification stamp
- Read the `## Scope` block's **Tier** line (contract §12). `tier: light` → the stamp must read `Verified by /verify-plan --adequacy (Adequacy-only …)` with result `PASS` or `CONVERGED` (contract §12 Convergence: the Verification Summary's `### Deferred findings` rows are the first commit, resolved in code; `Adequacy: CELL` on a bug-fix plan still asks the owner once, as below); absent → "This light plan has no adequacy pass. Run `/verify-plan <path>` (it runs `--adequacy` for a light plan) first." `NEEDS REVISION` → stop, as for a full plan. Then run `python3 build_tools/check_plan_refs.py <plan> --issues` (outside the sandbox, `gh`) yourself and **refuse on a red report** until it is clean — the light pass corrects nothing inline, so references may have drifted since. Record the tier in the Implementation Summary header; the final `/verify-implementation` runs its light auditor set. No `Tier` line (plans created before 2026-10-28) → `full`.
- **Promotion in flight** (§12): if an implementer hits a decision the charter and plan did not rule, or the fix moves into `Semantic/`, `Lowering/` or `CodeGen/` on a light plan — STOP that lane, write the question with lettered options and the door type under `## Implementation Checklist` (never into `## Design Decisions`), ask the owner, record the answer there, and replace the `## Scope` Tier line with `tier: full (<letter>, promoted at /implement-plan: <reason>)`. Never demote.
- For a `full` plan, look for `<!-- Verified by /verify-plan` at the top:
- If **absent**: warn the user "This plan has not been verified. Consider running `/verify-plan` first." Ask whether to proceed or stop. A first line reading `<!-- DRAFT, not verified` is this case stated by the file itself: `/create-plan` committed the draft for the trail, and no pass has stamped it.
- If **NEEDS REVISION**: stop — "This plan was flagged as needing revision. Please address the issues in the Verification Summary before implementing."
- If **CONVERGED** (contract §12 Convergence): proceed as for PASS, with one exception — `Adequacy: CELL` on a bug-fix plan still needs the owner's word: show the `### Deferred findings` rows and ask once ("converged with CELL — say `override` to implement, or revise"); proceed only on the override and record the owner's words in the plan's `## Revision log`. The `### Deferred findings` rows are Phase 1's first task (a light plan's first commit): each is resolved in code — a map or exclusion entry, an assertion's source expression, a test's census constant — with the assertion or mutation the row names, and the commit body names the row it closes; the plan's design sections (Scope, Defect Class, Design Decisions, Acceptance, Issues to Close) are not edited beyond one `## Revision log` line (`deferred findings resolved in code @ <sha>`) — the Implementation Checklist and override records this skill writes are not design sections. Record `<Adequacy> (converged)` in the Implementation Summary header.
- If **PASS** or **PASS WITH CORRECTIONS**: read the `<!-- Adequacy: CLASS | CELL | N/A -->` line:
  - `CLASS` or `N/A` → proceed
  - `CELL` and any plan input is a bug/ICE/regression issue → **refuse**: "This plan patches a cell, not its class (see the plan's Adequacy verdict). Add a Defect Class section — contract, matrix, standard cure — and re-run `/verify-plan`, or say `override` to implement it as a cell fix knowingly." Proceed only on an explicit override, and record the override in the plan file. A `CONVERGED` stamp does not replace the override — it only changes what the owner is asked (the deferred rows, above).
  - Stamp without an Adequacy line (verified before 2026-08-26) → treat as `N/A` for feature plans, and as `CELL` for bug-fix plans unless the plan already has a Defect Class section.
- Read the `<!-- Scope:` stamp line and the plan's `## Scope` block (contract §11). `MULTI-GROUP` without `override: <owner's words>`, an output/layout-changing phase sharing a gate, or a generator-required group with no stage-0 commit (contract §11) → **refuse**: "This plan bundles more than one group (or skips its generator). Split it per the charter, or say `override` to implement it knowingly." Record an override in the plan file. Plans stamped before 2026-09-27 have no Scope line: derive it from the plan's groups and apply the same rule. A light plan's `Adequacy-only` stamp carries the same `Adequacy:` and `Scope:` lines, so this step is identical at both tiers.

- **Committed copy** — every edit this skill makes to the plan (the `## Implementation Checklist`, an override record, a `## Revision log` line) is committed in the lead's own slices.

- **Branch** (`docs/process/README.md` › The stage trail) — a `light` plan continues on `claude/<group>`, where its stages already are; a `full` plan cuts `claude/<group>` (or a tranche name, `claude/p82-tranche2`) from `origin/dev` once its plan pull request has merged, or from that branch's tip while it is open (say so: the implementation's pull request then carries the plan commits too).

### 3. Report git status
- Run `git status --short`. If there are uncommitted changes, REPORT them to the user and ask whether to proceed — do **not** stash, restore, or clean; the tree may hold a peer's work.

### 4. Check for partially-completed work
- Run `git log --oneline <base>..HEAD` and `git diff <base>...HEAD --stat` to see what the plan has already landed
- Note existing commits per phase so agents don't duplicate work; mark done items in the checklist (§Task Decomposition)

### 5. Establish the baseline (measured, background)
- `.claude/scripts/dotnet-serialized build sharpy.sln` (`dangerouslyDisableSandbox: true`) — if it fails, stop and report the build error
- Establish the whole-solution baseline: reuse the base commit's `mainline` run if it has one (`.claude/scripts/ci-gate --run <id>`, found with `gh run list --workflow dotnet10.yml --commit <sha>`), otherwise push the branch and run `/ci-gate` **in the background** (~20 min) while continuing with read-only setup. If it exits 3, fall back to the local gate (`/ci-gate` › The local fallback, contract §6) and read the counts from the log path that run printed (`dotnet-serialized: log -> <path>`; each invocation has its own).
- Record it as **`passed/failed/skipped @ <sha> (measured)`** — never a derived number.
- If red: **triage before attributing** — check the parent round plan's open-class list (ILCompiles fresh seeds re-roll open classes) and the pre-existing allowlist reds; label each red as `pre-existing (#NNN)` or `unexplained`. Warn the user and ask whether to proceed.

## Team Formation

### 1. Check the harness
Team/task-board tools (`TeamCreate`, `TaskCreate`, `TaskUpdate`, `TaskList`, `TaskGet`, `TeamDelete`) exist only in harnesses that expose them. **Check this session's tool list first.**
- **Present** → create a team `implement-plan`, create tasks with `TaskCreate`, assign via `TaskUpdate`, monitor via `TaskList`.
- **Absent** (the current Claude Code harness) → coordinate with `Agent` (background, `subagent_type` per the table below) + `SendMessage`, and keep the task checklist **in the plan file itself**: append a `## Implementation Checklist` section at the end of the file, after every plan section (one line per task: `- [ ] <task> — <owner> — blocked by <tasks>`), that the lead updates as tasks land. The plan file is the board — a tracked one, so checklist updates are committed with the lead's slices (Pre-Implementation §2).

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

`verification-expert` is **not** a teammate (no `SendMessage`/task tools). The lead spawns it **standalone** at the end (§Final Verification) and reads its report. For any refutation brief (regression control run, sibling-cell probing) pass `model` explicitly at spawn time — its default `haiku` is for "run this filter and report counts" only.

Spawn teammates with `run_in_background`; give each its task text, the relevant plan section, and the Agent Instructions block below. Never run more than one agent that needs the same files.

## Task Decomposition

Break the plan into commit-sized tasks (via `TaskCreate` or the plan-file checklist). Rules:

1. **Ordering**: Lexer → Parser → Semantic → Validation → Lowering → CodeGen → LSP, each blocked by the one before (`addBlockedBy` or the checklist's `blocked by`); Tests per item 3
2. **Granularity**: one logical commit per task (e.g., "Add TokenType.FormatString to Lexer", "Add FormatStringExpression AST node", "Handle FormatStringExpression in TypeChecker")
3. **Test tasks**: alongside or immediately after each implementation task, never all at the end; each test task includes its **mutation step** (§Guard Delivery Rule)
4. **Class tasks** — if the plan has a **Defect Class** section: create the **matrix/sweep task** (the sibling cells the plan enumerated go green, or the standing harness gains the cells) and the **drain task** (every allowlist entry, or stage-0 known-red entry, the fix drains is deleted; stale entries fail), both blocked by the fix tasks. With a stage-0 generator the test is the matrix: drain the known-red cases the plan names, never re-derive the matrix from the plan. A bug-fix plan with a Defect Class section but no matrix task is incomplete — add it.
5. **Regen task** — immediately after the **first** task that touches `Semantic/`, `Lowering/`, or `CodeGen/`, not at the end: regenerate spy-stdlib C# (`build_tools/regenerate_spy_stdlib.sh`), spy-test C#, stdlib docs (`build_tools stdlib generate --force`), and the oracle ledger (`/regenerate-snapshots` covers the C# snapshots). Read the regen diff as **findings**: a newly red regenerated test is a live regression until disproven.
6. **Excluded sections**: skip any sections listed in `--exclude`
7. **Final tasks**: a "Run final verification" task (lead + standalone `verification-expert`) and a "Run dotnet format whitespace" task, both blocked by all implementation tasks

## Implementation Workflow

Assign tasks (via `TaskUpdate` `owner`, or by naming the owner on the checklist line) and monitor (via `TaskList`, or by re-reading the checklist and the agents' reports). After **each agent wave**: run `git diff --stat` and `git status --short`, compare against the wave's declared scope, and commit the lead's own work in small slices. A working-tree delta nobody claims is a finding — ask before touching it. Also pick the guard with the largest claimed red count in the wave's commit bodies and **re-run its recorded mutation yourself** (break → red, restore → green, via `cp`); a recorded pair that does not reproduce is a finding for that lane, not a note.

### Agent Instructions

Each agent receives these instructions along with its specific task:

```
You are implementing part of a plan. Your task is described below along with the relevant plan section.

CRITICAL RULES:
- CLAUDE.md's Critical Rules bind you (your session loads it); generated Stdlib C# also compiles at C# 9.0 (netstandard2.1); spec examples you add are executed before commit
- Fix the class, not the cell — and never spot-fix a sibling silently. A cell INSIDE the plan's contract goes into the Defect Class table and is fixed at the seam in this plan. A cell OUTSIDE it is appended to the round's findings ledger (`<scratchpad>/findings-ledger.md`: cell · program · observed · expected @ sha · lane · reach · candidate class · found-by); you never run `gh issue create` — the lead files by class once per wave (contract §10). A `python3` divergence on a DEVIATION-by-default surface (`docs/design/python-fidelity-scope.md`) is a deviation-ledger row, not a finding
- Language before compiler (layer ladder, CLAUDE.md › Core & Stdlib Conventions): if a task's Semantic/CodeGen change is expressible at a lower rung — `.spy` source, Core/Stdlib C# in the dunder table's spelling (operator overloads, `ISized`/`IBoolConvertible`/`IReverseEnumerable<T>`, `Contains(T)`, `IEnumerable<T>`) discovered by reflection, or a CLR-identity bridge rule — STOP and report before coding; never land a name-keyed (`BuiltinNames.X`) rule silently

SHARED TREE (verbatim from docs/design/verification-contract.md §9):
The working tree is shared with other agents. Never run `git checkout`, `git restore`,
`git clean`, `git stash`, `git reset`, or `rm` on repository paths. REPORT `git status`; do not
"make it clean". Stage with explicit per-file pathspecs and check `git diff --cached --stat`
before committing; never `git add -A` or `git add .`. Restore a mutation-test from the copy you
made (`cp`), never from git. Never run `dotnet` directly — use `.claude/scripts/dotnet-serialized`
with `dangerouslyDisableSandbox: true`.

SERIALIZED DOTNET: CLAUDE.md › Operational Contracts. A whole-solution run is ~30 min; grep the
log the wrapper printed before re-running.

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
7. Run `.claude/scripts/dotnet-serialized format whitespace sharpy.sln` (raw `dotnet format` is hook-blocked; the save hook is per-file and best-effort, so a file a peer created can still be unformatted)
8. Stage ONLY the specific files you changed by explicit pathspec; check `git diff --cached --stat`
9. Commit with a descriptive message referencing the plan section; use the commit trailer(s) the
   harness provides for this session
10. Report: what landed (commit hash), the mutation outcomes, any sibling cells filed, `git status`
    (mark your task completed via TaskUpdate if the harness has it)
```

### Gap Discovery (lanes record, the lead files — contract §10)

Agents never run `gh issue create`. Everything they find outside their task goes to the round's **findings ledger** (`<scratchpad>/findings-ledger.md`, one line per cell: cell · program · observed · expected @ sha · lane · reach · candidate class · found-by). After each wave the lead:
1. De-duplicates the ledger against itself, the plan's Defect Class table, the plan's "Class trackers" list and `gh issue list --label class-tracker --state open`.
2. Posts each cell on its open class tracker (cell comment template in `.claude/skills/batch-issues/comment-templates.md`). A NEW issue (tracking-issue body there, labeled `lane:<lane>` `reach:<reach>` `class-tracker`) only when no open tracker exists and the cell is `reach:program` in a silent-wrong, ice, refusal or runtime-throw lane. Lane `message`/`tooling`, `reach:probe` and DEVIATION-by-default surfaces (`docs/design/python-fidelity-scope.md`) are tracker comments or `docs/deviations.yaml` rows, never new issues (contract §10). Tech debt follows the same path.
3. Hands the issue numbers back so `// BUG(#NNN)` / `// TODO(#NNN)` comments can be written where a workaround remains — every TODO/FIXME/BUG comment MUST reference an issue number.
4. Records the ledger totals in the final report: cells recorded → issues filed (per class) → tracker comments → ledger rows.

### Guard Delivery Rule

Agent Instructions step 6 is the rule. A guard that stays green when broken is a **finding**, not a deliverable: the lead decides whether the fix is inert (fallback path one call later), the exemption is the subject (parameterize the one falsifiable arm), or the defect does not reproduce — and files or widens accordingly. Absence assertions carry a positive control.

### Incremental Commits

After each task is completed by an agent:
1. Verify the agent staged only relevant files (`git show --stat HEAD`)
2. The commit message names the plan's task id (`T3`, `Phase 2`) and — for guard commits — carries the mutation outcomes
3. Use the commit trailer(s) the harness provides for the session; never hard-code a model name
4. Update the checklist (or the task board) and re-check `git diff --stat` against the wave's scope

## Final Verification

After all implementation tasks are complete, the lead runs the gate and spawns `verification-expert` standalone (pass `model` explicitly; it refutes, it does not confirm):

1. `.claude/scripts/dotnet-serialized build sharpy.sln` — must succeed. Before every push (workers' filtered runs miss these):
   - tests filtered `FullyQualifiedName~Sharpy.Compiler.Tests.Infrastructure|FullyQualifiedName~Sharpy.Compiler.Tests.Conformance`;
   - `/push`'s whitespace gate (`--verify-no-changes`; fix with `/format`);
   - `bash build_tools/check_commit_subjects.sh "$(git rev-parse --abbrev-ref --symbolic-full-name @{upstream} 2>/dev/null || echo origin/dev)..HEAD"`: a long subject cannot be fixed after a push without rewriting history.
2. **Whole-solution gate** in the background: push the branch and run `/ci-gate` (the local fallback on exit 3) — compare against the baseline `@ <sha> (measured)`; every new red gets a **control run** at the plan's base sha (`/ci-gate` › Control runs) before it is attributed. No do-not-merge PR
3. **Blast-radius sweeps** the plan named: `FrontEndParityTests` (LSP parity), and the GapDiscovery sweeps CI runs as separate steps — `--filter "Category=GapDiscovery&FullyQualifiedName~InteropConformance"`, `~MetamorphicCorpus`, `~DifferentialExecution` — for any change under `Semantic/`, `Lowering/`, or `CodeGen/`
4. **Staleness gates**: run `/push`'s generated-artifact gates (spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger) — a diff here is a finding, not noise
5. **Spec examples**: execute every example the plan promised in `docs/language_specification/` or `DiagnosticExplanations` (`/spy-run`); outputs must discriminate
6. **Allowlists**: every entry the plan's fixes drain is gone; no entry was added without an issue reference
7. `git diff --stat` and `git status --short` re-check after the last agent wave — every delta is claimed by a commit or an agent report

## Cleanup and Report

After final verification:

1. If a team exists: shut down teammates via `SendMessage` (`type: "shutdown_request"`) and delete the team via `TeamDelete`; otherwise confirm every background agent has reported
2. Mark the plan's Implementation Checklist complete (or list what was deferred and why) and commit the plan file by explicit pathspec (`process(plan): <plan-id> — implementation checklist closed @ <sha>`); `/verify-implementation` appends its verify record to this same committed file
3. **Open the pull request** after Final Verification, when every red at the tip is attributed by a control run (never to reach CI: `/ci-gate` dispatches at the pushed commit). `/push`, then open it into `dev`. **Title:** `<type>(<area>): <group> — <what the plan cures>`, ≤ 72 characters. **Body:** the plan path and stamp line; the issues it closes, with their close criteria (`Refs #N`, never `Closes #N`: `/verify-implementation` closes on evidence, contract §8); **the issues this work filed** and the tracker comments it posted; the gate `passed/failed/skipped @ <sha> (measured)` with its run id; the guards' mutation outcomes; what was deferred; the independent review line (step 4). A tip with an unattributed red gets no pull request yet: the pushed branch is the trail. A session that stops before Final Verification still pushes the branch: it is the only copy that survives the container.
4. **Independent review, before merge** (the no-plan lane takes this step too). Spawn ONE reviewer that wrote none of the code: a general-purpose `Agent`, `model: "opus"` for anything touching the compiler, Core or Stdlib. Give it the diff and the oracle, never the plan (plan auditors inherit its exclusions): the PR number, head and base branches, the sha the shared `sharpyc` was built from (a stale binary fakes verified-by-running), CLAUDE.md's Critical Rules, and "establish Python behaviour with `python3.15` before judging". Its brief:
   - run the `code-review` skill on the PR;
   - hunt a case the PR's own tests do not cover where behaviour is wrong, in the direction you name for this change (silent-wrong, a crash on valid code, a leak the contract forbids) — a bare `code-review` finds less than one told what to hunt;
   - check the diff against CLAUDE.md's Critical Rules and Design Anti-Patterns (no expected value changed to pass; each new guard can fail) and for dead code or debug leftovers;
   - read the PR's own CI run on its head and say which failures are the PR's (local green is not evidence about CI);
   - return merge / merge after fixes / do not merge, findings ranked by severity, each with file:line, a concrete scenario, the smallest fix, and verified-by-running, verified-by-reading or inferred.

   Put these constraints in its prompt AND in the `code-review` arguments: read-only; no checkout, switch, worktree, restore, clean, stash or reset; no dotnet or build unless this prompt states the dotnet lock is free; no `--comment`, no `--fix`; nothing posted on GitHub. Apply each finding or decline it with a reason; declined findings go to the owner, and a `do not merge` verdict stands until a second review clears it. A fix round that adds substantial code (a new abstraction or guard, a changed public surface) gets a second review, as does any `/verify-implementation` remediation commit that adds code. When the last round is settled, fill in the body line `Independent review @ <reviewed head sha>: <verdict>; <n> applied, <m> declined (reasons below)`; until then the PR does not merge. The review is stale unless its sha is the PR's head or an ancestor with only non-code commits since. Nothing enforces this mechanically: the line makes a missing or stale review visible. A no-plan pull request then merges on green (README item 6); a plan's pull request merges at `/verify-implementation` step 5.
5. Present a summary report to the user:

```markdown
## Implementation Summary

**Plan:** [plan file path] · **Adequacy:** [CLASS / CELL (override) / N/A — suffixed `(converged)` on a CONVERGED stamp]
**Branch:** [lane branch] · **Pull request:** [URL, or "none: <the red that blocks it>"] · **Independent review:** [@ <sha>: verdict; n applied, m declined] · **Scope:** [base sha]..[HEAD sha] · **Commits:** [count]

### What Was Done
- (each completed task with commit hash)

### Guards mutation-tested
- N red-when-broken · M vacuous → [filed as #… / fixed in <hash>]

### Findings ledger (contract §10)
- Cells recorded: N → issues filed: M (one per class, lane/reach labels: #…) → tracker comments: K → deviation-ledger rows: J
- In-contract cells added to the Defect Class table and fixed here: (list)
- Round ratio: filed M / issues this plan closes at verify: C

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
