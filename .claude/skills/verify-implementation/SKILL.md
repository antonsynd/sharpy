---
name: verify-implementation
description: Verify completed plan implementation with refuting auditors, a control run, and a class-status report; fix gaps/bugs/regressions and commit fixes
argument-hint: "<path/to/plan.md>"
---

Read `docs/design/verification-contract.md` before proceeding; every audit below applies it.
**Stance: refute, not confirm** — an auditor reports NOT REFUTED only after naming what it tried.

Verify that a plan has been fully and correctly implemented. Reads the plan, derives completion from its acceptance bullets and "Issues to Close", runs the whole-solution gate plus a **control run at the plan's base sha**, spawns four refuting auditors (completeness, regression, class-cure, sibling-cell prober), fixes what they find, and commits the fixes.

## Argument Handling

If `$ARGUMENTS` is non-empty, use it as the path to the plan file.

If `$ARGUMENTS` is empty, plans are split across two directories (repo-local `.claude/plans/`, gitignored, is the default; per-batch plans created before 2026-08-26 live in `~/.claude/plans/`). List the three newest across both and **ask** which one — never pick silently:
```bash
ls -t .claude/plans/*.md ~/.claude/plans/*.md 2>/dev/null | head -3
```

Read the plan file completely before proceeding.

## Operational contract (applies to every command below)

- **Every `dotnet` invocation goes through `.claude/scripts/dotnet-serialized`** (drop-in: same args, output, exit code). Raw `dotnet` is blocked by `.claude/hooks/enforce-dotnet-serialized.sh`. The wrapper must run with `dangerouslyDisableSandbox: true` (sandboxed callers exit 125); `gh` needs the same.
- Output is teed to `.claude/tmp/dotnet-serialized-{0,1,2}.log` (`-latest.log` symlinks the newest, and may rotate to a *peer's* run — check the `Test run for <path>` line and the binary timestamp before trusting a log). Read logs instead of re-running (~22 min wall clock for the whole solution).
- **Counts carry their sha:** every number in this skill's outputs is written `passed/failed/skipped @ <sha> (measured)`. A derived number is never written in that format.
- Commits use the trailer the harness provides for this session; never hard-code a model name.

## Pre-Verification Checklist

Before spawning any agents, perform these checks yourself:

### 1. Validate plan file

- Confirm the file exists and is readable
- Check for the `/verify-plan` stamp — `<!-- Verified by /verify-plan` near the top
  - **Absent**: warn the user, proceed
  - **NEEDS REVISION**: warn the user, proceed, note it in the final report
  - **PASS** / **PASS WITH CORRECTIONS**: proceed
  - `<!-- Adequacy: CELL -->` on a bug-fix plan: the Sibling-cell prober (Agent 4) is mandatory and must build its own matrix; say so in the report
- Locate the `/implement-plan` evidence — `git log --oneline` for commits referencing the plan, or the plan's own implementation checklist/commit table

### 2. Establish the scope: base sha → HEAD

The audit range is **the plan's own commit range**, never the branch-vs-`mainline` range (on `dev` that spans every batch since the last release).

- **Base sha** = the parent of the plan's first implementation commit. Read it from the plan's implementation commit table/checklist, or from `git log` on the commit messages that reference the plan. If neither identifies it unambiguously, **ask the user** — do not guess.
- Record `BASE=<sha>` and `HEAD=<sha>`; every `git diff`/`git log` below uses `$BASE..HEAD`.

### 3. Establish the baseline @ HEAD

- `.claude/scripts/dotnet-serialized build sharpy.sln` — must succeed. If it fails, **stop and report** (the implementation is broken; nothing downstream can be measured).
- `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"` — run in the **background** and continue with the read-only steps; record `passed/failed/skipped @ HEAD (measured)` when it returns. This is the commit gate (contract §6); a filtered run is never a substitute.
- `dotnet format whitespace --verify-no-changes` — record whether formatting is clean.
- If the working tree is dirty with a peer's work, do the build and gate in a `git worktree add ../sharpy.worktrees/wt-head HEAD` instead of the shared tree.

### 4. Identify the plan's deliverables

Extract from the plan — **acceptance bullets and "Issues to Close" first**, prose second:
- Every **acceptance bullet** per phase, verbatim, with the close criterion it names
- Every **issue** in "Issues to Close" and its row's criterion
- Every **file path**, **step/task**, **test** (unit, fixture, harness), **diagnostic code**, **AST node / type / validator / emitter method**, **Sharpy.Core** member
- Every **docs/spec deliverable** (spec section, `DiagnosticExplanations` entry, design doc) — same weight as code
- The **Defect Class** section (violated contract, matrix, standing harness, owner rulings) and the **Adversarial Review** section (blast radius, generated artifacts) if present. If the plan has neither and it fixes a bug, note `no matrix in plan` — Agent 4 builds one.

Build a **completeness checklist** — every deliverable, each with the evidence that would prove it (what runs, what it must print).

## Team Formation

**Check the session's tool list first.** If `TeamCreate`/`TaskCreate` exist, a team is optional. In the current harness they do not; spawn the four auditors as **background `Agent` calls in one message** (they are independent) and collect their final reports — read-only agents (`code-reviewer`, `verification-expert`) are never teammates (see `.github/agents.md` › Teammate Compatibility).

**Models.** `verification-expert` and `net-axiom-guardian` are defined with `model: haiku`, which is right for "run this filter and report counts" and wrong for a refutation brief. Spawn **Agents 2, 3, and 4 with `model: "opus"`** explicitly. Agent 1 may run on its definition's default.

Every agent prompt below **embeds this block verbatim** (contract §9):

> The working tree is shared with other agents. Never run `git checkout`, `git restore`,
> `git clean`, `git stash`, `git reset`, or `rm` on repository paths. REPORT `git status`; do not
> "make it clean". Stage with explicit per-file pathspecs and check `git diff --cached --stat`
> before committing; never `git add -A` or `git add .`. Restore a mutation-test from the copy you
> made (`cp`), never from git. Never run `dotnet` directly — use `.claude/scripts/dotnet-serialized`
> with `dangerouslyDisableSandbox: true`.

Give every agent: `BASE`, `HEAD`, the plan path, the baseline counts `@ HEAD (measured)`, and the block above. Auditors do not edit or commit; they report.

### Agent 1: Completeness — refute "it's done" (`code-reviewer`)

```
You are auditing whether a plan was fully implemented, in the range $BASE..$HEAD. Your job is to
REFUTE the claim "this plan is done". Report an item DONE only after naming the evidence you
checked. Completion is derived from the plan's ACCEPTANCE BULLETS and "Issues to Close" rows,
not from "the code landed".

SHARED TREE: The working tree is shared with other agents. Never run `git checkout`, `git restore`,
`git clean`, `git stash`, `git reset`, or `rm` on repository paths. REPORT `git status`; do not
"make it clean". Stage with explicit per-file pathspecs and check `git diff --cached --stat`
before committing; never `git add -A` or `git add .`. Restore a mutation-test from the copy you
made (`cp`), never from git. Never run `dotnet` directly — use `.claude/scripts/dotnet-serialized`
with `dangerouslyDisableSandbox: true`.

For EVERY item in the completeness checklist:
1. Glob: the referenced file exists.
2. Grep: the referenced functions/classes/methods/types/diagnostic codes exist where claimed.
3. Read: the content does what the acceptance bullet says — not merely that a file exists.
4. Fixtures (.spy + .expected/.error/.warning): both halves exist; the .expected output would
   DIFFER with the bug present (an example that prints the same thing either way proves nothing).
5. Docs/spec deliverables weigh the same as code: a promised spec section or
   DiagnosticExplanations entry that greps to nothing in docs/language_specification/ or
   Diagnostics/DiagnosticExplanations.cs is MISSING, not "docs later".

Tells of an unfinished close (check each explicitly and report which you ran):
- A new file with NO CALLERS: `grep -rc <symbol> src/<consumer project>` = 0 outside its own file.
- A promised spec section that greps to nothing.
- An allowlist entry (Conformance/*-allowlist*.txt and siblings) that the fix should have drained
  but is still present — cite the entry and the issue it names.
- An "Issues to Close" row whose close criterion has no executing evidence at $HEAD.

Report each item as DONE / PARTIAL (what is missing) / MISSING / DIVERGED (how), each with the
evidence line ("grep hit at path:line", "fixture X prints Y @ $HEAD"). End with: NOT REFUTED
items (with what you tried) vs REFUTED items.
```

Provide the full completeness checklist and the plan's "Issues to Close" table.

### Agent 2: Regression — refute "nothing broke" (`verification-expert`, `model: "opus"`)

```
You are checking for regressions introduced in $BASE..$HEAD. Your job is to REFUTE the claim
"nothing broke". Every count you report is written `passed/failed/skipped @ <sha> (measured)`.

SHARED TREE: The working tree is shared with other agents. Never run `git checkout`, `git restore`,
`git clean`, `git stash`, `git reset`, or `rm` on repository paths. REPORT `git status`; do not
"make it clean". Stage with explicit per-file pathspecs and check `git diff --cached --stat`
before committing; never `git add -A` or `git add .`. Restore a mutation-test from the copy you
made (`cp`), never from git. Never run `dotnet` directly — use `.claude/scripts/dotnet-serialized`
with `dangerouslyDisableSandbox: true`.

1. Whole-solution gate at HEAD, in a worktree so peers' edits cannot move under you:
   `git worktree add ../sharpy.worktrees/wt-verify-head $HEAD` then, inside it,
   `.claude/scripts/dotnet-serialized build sharpy.sln` and
   `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"`.
   A filtered run is not the gate. A log whose binaries predate the build is not a green.
   (If the lead's background gate already produced a log for $HEAD, read it — but confirm the
   `Test run for` lines and timestamps belong to $HEAD.)

2. CONTROL RUN before attributing any red: for every failing test, run the SAME test (same filter)
   in a worktree at $BASE: `git worktree add ../sharpy.worktrees/wt-verify-base $BASE`. Red at HEAD and
   green at BASE = regression. Red at both = pre-existing; report it, do not attribute it. Also
   triage any red against the open-class list in the parent round plan (ILCompiles/CsClean fresh
   seeds re-roll open classes — a red may be a fresh-seed discovery, not this plan's doing).

3. Blast-radius sweeps the plan names (Adversarial Review › Blast radius), and always when the
   range touches Semantic/ or CodeGen/: Sharpy.Stdlib.Tests and Sharpy.Cli.Tests are in the gate;
   run separately with the wrapper:
   - `--filter "FullyQualifiedName~FrontEndParityTests"` (LSP parity)
   - `--filter "Category=GapDiscovery&FullyQualifiedName~InteropConformance"`
   - `--filter "Category=GapDiscovery&FullyQualifiedName~MetamorphicCorpus"`
   - `--filter "Category=GapDiscovery&FullyQualifiedName~DifferentialExecution"`
   CI runs these as separate steps and excludes them from the main Compiler step, so the gate
   alone does not cover them.

4. Regenerate generated artifacts in the HEAD worktree and READ THE DIFF AS FINDINGS:
   `build_tools/regenerate_spy_stdlib.sh` (spy-sourced stdlib C#), the spy-test C# regen, stdlib
   docs (`build_tools stdlib generate --force`), the oracle ledger. A newly red regenerated test is
   a live regression until disproven; a non-empty diff that the range did not commit is a
   staleness finding (the /push gate will fail).

5. Allowlist diff: `git diff $BASE..$HEAD -- '*allowlist*'` plus every Conformance/*.txt ledger.
   New entries WITHOUT an issue reference = violation. Entries the range's fixes should have
   drained but did not = drain-on-fix debt. Run the allowlist staleness checks
   (e.g. PropertySeedDisciplineTests and each sweep's stale-entry assertion).

6. Expectation tampering, within the range only:
   `git diff $BASE..$HEAD --name-status -- '*.expected' '*.skip' '*.error' '*.warning'`.
   Modified .expected = flag (plan violation unless the plan's acceptance bullet says the output
   changes and why). New .skip = flag with its reason line.

7. Representative execution: 5 fixtures the range touched, each via
   `.claude/scripts/dotnet-serialized run --project src/Sharpy.Cli -- run <file>` — SPY0908
   surfaces only under `run`, never under `emit`.

Remove your worktrees when done (`git worktree remove`). Output: PASS/FAIL per check, each with
its sha and the control-run result; a list of regressions (red @ HEAD, green @ BASE) and a
separate list of pre-existing reds (red at both).
```

Provide the baseline counts `@ HEAD (measured)` and the plan's blast-radius and generated-artifacts lines.

### Agent 3: Class-cure review — refute "it's the root cause" (`code-reviewer`, `model: "opus"`)

```
You are reviewing the changes in $BASE..$HEAD. Your job is to REFUTE the claim "this fix cures
the class" — that it landed at the shared seam rather than on one arm, and that its guards are
load-bearing. Report NOT REFUTED only after naming what you tried.

SHARED TREE: The working tree is shared with other agents. Never run `git checkout`, `git restore`,
`git clean`, `git stash`, `git reset`, or `rm` on repository paths. REPORT `git status`; do not
"make it clean". Stage with explicit per-file pathspecs and check `git diff --cached --stat`
before committing; never `git add -A` or `git add .`. Restore a mutation-test from the copy you
made (`cp`), never from git. Never run `dotnet` directly — use `.claude/scripts/dotnet-serialized`
with `dangerouslyDisableSandbox: true`.

Section A — class cure (do this first)

1. Seam or arm? For each production hunk (`git diff $BASE..$HEAD -- src/Sharpy.Compiler src/Sharpy.Core src/Sharpy.Stdlib src/Sharpy.Lsp`):
   name the contract the plan says it enforces (Defect Class › Violated contract) and check
   whether the change sits at the single point every cell flows through, or in one switch arm /
   one partial file / one entry point while a mirrored sibling (the other operand position, the
   other callee kind, the LSP route, the qualified spelling) is untouched. A one-arm fix with no
   completeness scan is a FINDING even if every test is green.

2. Mutation-test EVERY new or modified test/guard/harness, in a worktree
   (`git worktree add ../sharpy.worktrees/wt-verify-mut $HEAD`):
   a. `cp` the production file aside. Break the guarded thing — invert the predicate or revert
      the production hunk (`git diff $BASE..$HEAD -- <file> | patch -R -p1` inside the worktree).
   b. Run the guard with the wrapper; read BOTH counters. It must go RED.
   c. Restore from your `cp` copy; re-run; it must go GREEN.
   d. Exemption check: if the mutation flows through the test's own exemption (the test excludes
      exactly the cases the predicate decides), the guard is inverted — report it; the fix is to
      parameterize the one falsifiable arm.
   e. Inert-fix check: with the production hunk reverted and the NEW test in place — if the test
      stays green, the fix is inert (usual cause: a fallback path one call later with identical
      logic). Report it as a finding, not a fix.
   f. Absence assertions ("no SPY0908", "0 hits", "no warning") need a positive control on the
      same input; if none exists, report it.
   Record per guard: `broken → red (N failed) / restored → green (N passed)` or VACUOUS / INVERTED / INERT.

3. Refusals by direction: for every diagnostic the range newly emits, list the programs it now
   rejects and run each at $BASE (worktree) with
   `.claude/scripts/dotnet-serialized run --project src/Sharpy.Cli -- run <file>`. Classify:
   ICE-before → diagnostic-now (fix); wrong-output-before → diagnostic-now (fix, note it);
   worked-before → rejected-now (REGRESSION wearing a diagnostic). Use `run`, never `emit`.

4. SPY0908 fixes must name their semantic check or lowering (docs/design/spy0908-policy.md). A fix
   that documents-and-closes, or whose only change is an allowlist entry, is a finding. Probe
   `b: bool = <expr>` where the story is "typed wrong vs never typed": SPY0220 = mistyped,
   silence = Unknown (the bug is upstream and a lowering built now never fires).

5. Rule 2 carriers: every new node-keyed dictionary on SemanticInfo is merged in
   `SemanticInfo.MergeFrom` (grep it); every new fact the emitter reads is materialized on
   Symbol.CodeGenInfo or in SemanticInfo — CodeGen makes no type/lowering decision and does no
   reflection (EmitterCarrierOnlyConformanceTests is the guard; EmitterBannedTokenScanTests is not
   Rule-2 evidence). Recorded ≠ applied: a recorded fact needs an executing test that asserts on
   output values.

Section B — conventions and anti-patterns (second)

- Feature order respected (Lexer → Parser → Semantic → Validation → Lowering → CodeGen → LSP → Tests)
- Immutable AST: no mutable state on AST records; annotations in SemanticInfo
- SyntaxFactory only in CodeGen; no string templating
- Axiom precedence .NET > Type Safety > Python Syntax
- C# 9.0 on the netstandard2.1 target of Sharpy.Core/Sharpy.Stdlib and in generated code
- Spec consistency: docs/language_specification/ does not contradict the change; spec examples
  the range added were executed (ask for the evidence line)
- No dead code, debug leftovers, magic values, copy-paste that should be one seam
- TODO/FIXME/BUG comments reference an issue
- Validators at non-conflicting Order values; symbol lifecycle and materialization boundaries
  respected; CompilerServices additions follow the adapter pattern
- Anti-patterns: "add X because Python has it", runtime type checks, wrapper types over extension
  methods, multiple ways to do one thing, magic behavior

Remove your worktree when done. Output: Section A findings first (SEAM/ARM per hunk; per-guard
mutation record; per-refusal direction; SPY0908 named check; MergeFrom status), then Section B as
CRITICAL / WARNING / SUGGESTION. End with NOT REFUTED claims and what you tried.
```

Provide the plan's Defect Class and Design Decisions sections, `BASE`, and the list of new/modified test files in the range.

### Agent 4: Sibling-cell prober — refute "the matrix is covered" (`verification-expert`, `model: "opus"`)

```
You are probing the cells of the defect class that the plan in $BASE..$HEAD claims to cure. Your
job is to REFUTE the claim "the matrix is covered" by finding a cell that still fails. The
issues' repro lists are a SYMPTOM REPORT, not a test plan — you probe what they do NOT name.

SHARED TREE: The working tree is shared with other agents. Never run `git checkout`, `git restore`,
`git clean`, `git stash`, `git reset`, or `rm` on repository paths. REPORT `git status`; do not
"make it clean". Stage with explicit per-file pathspecs and check `git diff --cached --stat`
before committing; never `git add -A` or `git add .`. Restore a mutation-test from the copy you
made (`cp`), never from git. Never run `dotnet` directly — use `.claude/scripts/dotnet-serialized`
with `dangerouslyDisableSandbox: true`.

1. Take the plan's Defect Class › "Sibling cells this plan must also cover" matrix. If the plan
   has no matrix (or is stamped Adequacy: CELL), BUILD one from the touched dispatch sites: for
   every switch/match/if-chain/partial-file arm the range modified, enumerate its sibling arms and
   the axes they range over (syntactic position × operand form; callee kind × usage form; single
   file × multi-file × LSP route; qualified × bare × aliased spelling; warm × cold), and SAY that
   you built it.

2. For every cell NOT in the issues' repro lists, write a minimal .spy in a scratch directory and
   run it with `.claude/scripts/dotnet-serialized run --project src/Sharpy.Cli -- run <file>`
   (SPY0908 surfaces only under `run`). Compare the output to `python3` where the semantics are
   Python's, or to the spec section the plan cites. When the semantics need a type, probe with a
   deliberately wrong annotation `b: <WrongType> = <expr>`: SPY0220 means the checker typed it
   (mistyped if the message is wrong), silence means Unknown. `print(x)` is never a type probe.

3. When every spelling you vary on one axis agrees, CHANGE AXIS — agreement is evidence the
   defect is not on that axis. Include the warm path (second compile of the same project with
   --incremental, or the LSP route) if the plan's blast-radius line names warm/cold.

4. For fixtures the range added, confirm the .expected output would differ with the bug present:
   revert the production hunk in a worktree (`git worktree add ../sharpy.worktrees/wt-verify-cells $HEAD`,
   `git diff $BASE..$HEAD -- <file> | patch -R -p1`) and run the fixture; identical output means
   the fixture does not discriminate. Restore via your `cp` copy.

Remove your worktree when done. Output a per-cell verdict table:
| axis values | in issue repro? | command | expected | observed @ $HEAD | verdict (PASS / FAIL / ICE / UNTYPED) |
Then: cells probed / passed / failed, whether you built the matrix, and NOT REFUTED with what
you tried. Every FAIL cell is a sibling-cell finding — do not fix it; report it.
```

Provide the plan's Defect Class section (or `no matrix in plan`), the issues' repro lists, and the list of files the range touched.

### Optional Agent 5: `hallucination-defense`

Spawn when the plan's Design Decisions make claims about .NET, Roslyn, or Python behavior, or when the range added spec examples. Brief: "Fact-check each claim below against the actual runtime (`python3 -c`, a scratch C# program, Roslyn docs); for each spec example in `docs/language_specification/` added in `$BASE..$HEAD`, execute it at `$HEAD` via the wrapper and report whether its printed output matches the doc and whether that output would differ with the bug present." Embed the shared-tree block.

## Collect Audit Results

Wait for all auditors to return. Do **not** begin remediation until every one has reported — a partial picture invites cell patches. Compile a unified list, categorized:

| Category | Source |
|----------|--------|
| MISSING / PARTIAL / DIVERGED deliverable | Agent 1 |
| Regression (red @ HEAD, green @ BASE) | Agent 2 |
| Pre-existing red (red at both) | Agent 2 — report, do not attribute |
| Stale / unreferenced allowlist entry; stale generated artifact | Agent 2 |
| Expectation tampering (.expected/.skip in range) | Agent 2 |
| One-arm fix (ARM verdict) | Agent 3 |
| Vacuous / inverted / inert guard | Agent 3 |
| Refusal that restricts working code | Agent 3 |
| SPY0908 fix without a named check; carrier not in MergeFrom | Agent 3 |
| Convention / anti-pattern (CRITICAL / WARNING / SUGGESTION) | Agent 3 |
| Sibling-cell FAIL / ICE / UNTYPED | Agent 4 |
| Non-discriminating fixture | Agent 4 |
| Refuted .NET/Roslyn/Python claim; spec example that does not run | Agent 5 |

## Remediation Phase

Address every item:

| Category | Action |
|----------|--------|
| MISSING / PARTIAL implementation | Implement yourself or delegate; docs/spec halves included |
| Regression | Fix the root cause — never modify `.expected` files |
| Pre-existing red | Leave it; cite the issue/class it belongs to in the report |
| One-arm fix | Move the check to the shared seam, or file the class issue and add the cells to the plan's Defect Class table — a second arm patch is not a remediation |
| Sibling-cell FAIL | Same rule: **a remediation that patches the cell the prober found is itself a finding**. Widen to the class or file the class issue with every failing cell listed |
| Vacuous / inverted / inert guard | Rewrite so it goes red when broken (parameterize the falsifiable arm; add the positive control); if the fix is inert, find the fallback path and fix that |
| Refusal restricting working code | Revert or narrow the refusal; add the working program as a fixture |
| Stale allowlist entry | Delete it in the fix's commit; **never widen an allowlist to absorb a new failure** |
| Stale generated artifact | Regenerate and commit the regen; read its diff as findings |
| Non-discriminating fixture | Change the example so its output differs with the bug present |
| Missing tests | Write them — with the mutation step |
| TODO without issue | `gh issue create`, then update the comment |
| Formatting | `dotnet format whitespace` |
| Dead code / debug leftovers | Remove |

### Remediation Rules

1. **Priority**: build failures > regressions > missing implementations > one-arm fixes and sibling cells > vacuous guards > conventions > missing tests > formatting/cleanup
2. **Never modify `.expected` files** to make tests pass — fix the implementation
3. **Cell patches are findings**: if a fix you are about to write touches only the cell an auditor found, stop — widen to the seam or file the class issue (contract §1)
4. **Never widen an allowlist** to absorb a new failure; entries drain on fix and are never added without an issue reference
5. **Every guard added during remediation gets the mutation step** — break it → red, restore → green, both outcomes in the commit body (contract §2)
6. **Verify each fix by running**, not by reading: the relevant tests via the wrapper, and the program's output for anything that records a fact codegen consumes (contract §4)
7. **Regenerate before the final gate**: spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger; commit the regen
8. **Stage specific files**: explicit per-file pathspecs; check `git diff --cached --stat`; never `git add -A` / `git add .`
9. **Incremental commits**, e.g. `fix: complete missing implementation for [plan step X]`, `fix: resolve regression in [component]`, `fix: move [check] to the shared seam (class [contract])`, `test: mutation-tested guard for [feature]`. Use the commit trailer the harness provides for this session
10. **Whole-solution gate after all fixes** (see Final Verification)

### Delegating Fixes

Check the session's tool list; with team tools absent, delegate via background `Agent` calls and embed the shared-tree block verbatim in every prompt:

| Area | Agent Type |
|------|-----------|
| Parser/Lexer/AST fixes | `parser-expert` |
| Semantic/TypeChecker fixes | `semantic-expert` |
| CodeGen/RoslynEmitter/Lowering fixes | `codegen-expert` |
| Sharpy.Core fixes | `core-library-expert` |
| Sharpy.Stdlib fixes | `stdlib-expert` |
| LSP fixes | `lsp-expert` |
| Test writing (with mutation step) | `test-expert` |
| General implementation | `implementer` |

Provide each agent with: the specific finding, the relevant plan section (including the Defect Class contract), the file(s) involved, the acceptance criterion, and — for any test — the requirement to report `broken → red / restored → green` in the commit body. Agents stage with explicit pathspecs and report `git status`; the lead re-checks `git diff --stat` after each wave.

## Final Verification

After all fixes are committed (`FINAL=<sha>`):

1. `.claude/scripts/dotnet-serialized build sharpy.sln` — must succeed
2. Regeneration is clean: spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger produce no diff at `$FINAL` (the `/push` staleness gates)
3. `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"` — whole solution, `@ $FINAL (measured)`; no red that was green at `$BASE`; pass count ≥ the `$HEAD` baseline minus any tests deliberately removed (say which)
4. The blast-radius sweeps Agent 2 ran (`FrontEndParityTests`, `InteropConformance`, `MetamorphicCorpus`, `DifferentialExecution` as the plan names them) — re-run those the fixes reach
5. `dotnet format whitespace --verify-no-changes` — clean
6. Every spec example the plan promised executes at `$FINAL` with the documented output
7. Every guard added by the plan or by remediation has a recorded mutation outcome
8. `git diff --stat $BASE..$FINAL` — matches the declared scope; no unclaimed working-tree delta (`git status`)

If a step fails, loop back to remediation. Maximum 3 remediation loops — after that, report the residue as unresolved with its evidence. A failing step invalidates its dependents, not its siblings: complete every independent step and label suspect readings.

## Cleanup and Report

1. Remove any worktrees you or the auditors created (`git worktree list`; `git worktree remove <path>`)
2. If a team was created, shut teammates down (`SendMessage` `shutdown_request`) and delete it; with background agents, confirm each has returned
3. Present the verification report:

```markdown
## Implementation Verification Report

**Plan:** [plan file path] (stamp: PASS / PASS WITH CORRECTIONS / NEEDS REVISION / none; Adequacy: CLASS / CELL / N/A)
**Branch:** [current branch]
**Scope:** `$BASE..$FINAL` (base = parent of first implementation commit; N implementation commits, M remediation commits)
**Verified on:** YYYY-MM-DD

### Completeness (Agent 1)

| Status | Count |
|--------|-------|
| DONE (evidence cited) | N |
| Was PARTIAL (now fixed) | N |
| Was MISSING (now fixed) | N |
| DIVERGED (acceptable, why) | N |
| Unresolved | N |

Issues to Close: per row — criterion, evidence `@ sha`, CLOSEABLE / NOT YET.

### Regressions (Agent 2)

- **Baseline @ HEAD:** X passed, Y failed, Z skipped @ <sha> (measured)
- **Post-remediation @ FINAL:** X passed, Y failed, Z skipped @ <sha> (measured)
- **Regressions found (red @ HEAD, green @ BASE):** N (N fixed)
- **Pre-existing reds (red at both):** N — issues/classes cited
- **Expectation changes in range:** N `.expected`, N `.skip` (each justified or reverted)

### Attribution (control run)

- **Control @ BASE:** X passed, Y failed, Z skipped @ <sha> (measured), same filter
- Per attributed red: test, HEAD result, BASE result, verdict

### Class contract status (Agents 3 + 4)

- **Violated contract:** <from the plan, or "built by Agent 4">
- **Standing harness:** <name> — state @ FINAL (green / red / added by this plan / none)
- **Allowlist delta:** −N drained, +N added (each with issue), N stale remaining
- **Cells probed / passed / failed:** N / N / N (matrix source: plan / built)
- **Seam-or-arm verdicts:** N SEAM, N ARM (→ fixed at seam / class issue #NNN filed)
- **Refusals by direction:** N ICE→diagnostic, N wrong→diagnostic, N worked→rejected (→ reverted)

### Guards mutation-tested (Agent 3)

- **Red when broken:** N
- **Vacuous / inverted / inert:** M → fixed: M₁, filed: M₂ (#NNN)
- **Absence assertions given a positive control:** N

### Architectural Review (Agent 3, Section B)

- **Critical:** N (N fixed) · **Warnings:** N (N fixed) · **Suggestions:** N

### Generated artifacts

- Regenerated at: <sha>; diff read as findings: N (list) · staleness gate @ FINAL: clean / red

### Fixes Applied

| Commit | Description | Category |
|--------|-------------|----------|
| abc1234 | ... | missing-impl / regression / seam-fix / guard-fix / regen / cleanup |

### Issues Created

| Issue | Title | Reason |
|-------|-------|--------|
| #NNN | ... | sibling cell / class issue / TODO without issue / deferred work |

### Unresolved Items

(each with the evidence that shows it is still open)

### Files Changed

(output of `git diff --stat $BASE..$FINAL`)
```
