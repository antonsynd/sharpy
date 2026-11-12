---
name: verify-plan
description: Verify a plan for accuracy, architectural soundness, and adequacy (class, not cell)
argument-hint: "<path/to/plan.md> [--adequacy]"
---

Read `docs/design/verification-contract.md` before proceeding (§10–§12 included); every dimension below applies it.

**Mode by tier (contract §12).** Read the plan's `## Scope` → **Tier** line first.
- `tier: full`, or no Tier line (plans created before 2026-10-28) → the nine-dimension pass below.
- `tier: light` → **`--adequacy` mode**, whether or not the flag was typed: run `check_plan_refs.py` (Dimension 1's mechanical half), then grade **Dimension 6** (every touched dispatch/route arm the matrix lacks), **Dimension 7** (each guard's mutation, exemption, positive control, inert-fix path) and the **design-decision rows of Dimension 4** (each `## Design Decisions` predicate against the code it names, not the plan). Skip Dimensions 2, 3, 5, 8 and 9. Make **no inline corrections**: write findings into the Verification Summary and set the result; the author fixes the plan. The light pass is a **fresh read**: if this session wrote the plan, spawn a fresh agent for the pass and relay its summary — anchoring is the failure this stage exists to break.
- A typed `--adequacy` on a `full` plan, or a request for the nine-dimension pass on a `light` plan, is an override: do it, and write `override: <the user's words>` into `## Scope`.

**Stance: refute** — for each claim, look for the input that shows it wrong; a dimension passes only after naming what was tried. A plan whose file paths are all accurate can still fail: a bug-fix plan that patches one cell of a class is **NEEDS REVISION**, however correct its references.

The nine-dimension pass checks every verifiable claim against the codebase at a named sha, grades the plan's *adequacy* against its defect class, and edits the plan: corrections in place, a stamp at the top, a Verification Summary at the end. `--adequacy` grades and stamps but corrects nothing.

## Argument Handling

**Sync first**, before reading or listing any plan: `git fetch origin && git pull --ff-only`, then find or list it from the remotes (`docs/process/README.md` › The stage trail, item 1).

If `$ARGUMENTS` is non-empty, use it as the path to the plan file.

If `$ARGUMENTS` is empty, list the three newest plans (stage trail item 1) and ask which — never pick silently. The pass edits the working copy `.claude/plans/<plan-id>.md` and commits it over the file of record (Output §4; working-copy rules: `docs/process/README.md` › The stage trail). A plan that already carries a stamp is a re-verification: the new stamp and summary replace the old ones.

Read the plan file completely before proceeding. Record `git rev-parse --short HEAD` — every check below is made against that sha, and the stamp names it (line numbers drift; a sha does not).

## Probing rules

- Never run `dotnet` directly — `.claude/scripts/dotnet-serialized` (drop-in wrapper; needs `dangerouslyDisableSandbox: true`), or the `/spy-run` / `/spy-emit` / `/quick-check` skills for inline Sharpy source.
- An ICE claim (SPY0908 / CS-leak) is confirmed or refuted with **`run`**, never `emit` — `emit csharp` succeeds on broken C#, and `emit diagnostics` cannot see the C# stage. A failing `run` cannot show warnings; use `emit diagnostics` for those.
- `python3 -c "..."` for every claimed Python behavior; `print(x)` is never a type probe — bind to a deliberately wrong annotation (`b: bool = expr`).
- `gh` needs `dangerouslyDisableSandbox: true` (TLS in the sandbox).
- **Run the mechanical check first:** `python3 build_tools/check_plan_refs.py <plan> --issues` (`--issues` needs `gh`, so run it outside the sandbox). It checks every repo path, `file:line`, `SPY` code, PascalCase symbol and `#issue` the plan names (Dimension 1 starts from its report). Spend the model's time on Dimensions 4, 6, 7 and 9 — those change decisions.

## Verification Dimensions

Check each dimension in order. For each claim, verify against the codebase with Glob, Grep, and Read (or the MCP graph servers when connected). Record what was tried for each — "not refuted" without a named probe is not a pass.

### 1. Structural Accuracy

Start from the `check_plan_refs.py` report (probing rules above); verify by hand only what it flags and every reference a Design Decision or the Defect Class rests on:
- **File paths**: Glob confirms every referenced file/directory exists
- **Function/method/class/type names**: Grep confirms they exist where claimed
- **Parameter signatures**: read the actual code and compare
- **Diagnostic codes**: check against `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`; every active code needs a `DiagnosticExplanations` entry
- **Line number references**: read the file and verify content at those lines
- **Issue references**: `gh issue view N` — state, title, and any owner ruling comment the plan relies on

Flag as error: any path, name, code, or issue that doesn't exist or says something else. Fix inline if the correct reference can be determined.

### 2. Consistency with Project Conventions

- **Feature implementation order**: Lexer -> Parser -> Semantic -> Validation -> (Lowering) -> CodeGen -> LSP -> Tests
- **Immutable AST rule**: no mutable state on AST records; annotations go in `SemanticInfo`
- **SyntaxFactory-only rule**: CodeGen changes use Roslyn `SyntaxFactory`, never string templating; the emitter makes no type/lowering decisions and performs no reflection
- **Axiom precedence**: .NET > Type Safety > Python Syntax — flag if the plan violates this
- **C# 9.0 constraint**: `Sharpy.Core`/`Sharpy.Stdlib` multi-target `net10.0;netstandard2.1` — netstandard paths are C# 9.0; generated code is C# 9.0
- **Language spec authority**: check `docs/language_specification/`; flag if the plan contradicts the spec — the implementation moves, never the spec
- **TODO -> issue rule**: no TODO/BUG/FIXME without a `gh issue create` step
- **Test expectations rule**: the plan must never suggest modifying `.expected` files (or widening an allowlist) to make tests pass
- **Spy-sourced stdlib**: for modules in `build_tools/regenerate_spy_stdlib.sh`'s `MODULES`, the C# is generated — the plan edits the `.spy` and regenerates

Flag as warning: any convention violation. Add a note explaining the correct convention.

### 3. Architectural Soundness

- **Pipeline phase ordering**: claimed dependencies match the actual pipeline (NameResolver -> ImportResolver -> TypeResolver -> TypeChecker -> ValidationPipeline -> Lowering -> RoslynEmitter)
- **SemanticInfo vs AST**: no computed data on AST nodes
- **Materialization boundaries**: symbol-owned facts on `Symbol.CodeGenInfo` frozen at `MaterializeCodeGenInfo`; node-owned facts in a `SemanticInfo` dictionary — and **every new node-keyed dictionary is added to `SemanticInfo.MergeFrom`** (otherwise its entries vanish in the per-file → project merge). Grep `MergeFrom` for each dictionary the plan introduces.
- **Validation pipeline placement**: proposed validator `Order` values don't collide; TypeChecker (type mismatches, in-progress inference) vs ValidationPipeline (self-contained AST analyses) split respected
- **Symbol lifecycle**: progressive symbol population across passes respected
- **CompilerServices integration**: new services follow the `CompilerServicesBuilder` adapter pattern
- **Layer choice (ladder)**: every design decision names its rung (CLAUDE.md › Core & Stdlib Conventions). For each rung-4 rule (a `BuiltinNames.X` / type-name comparison or a name alias in `Semantic/`), check the lower rungs yourself: the dunder table (`dunder_methods.md`), the Core protocol interfaces, the `Dict.cs` operator overloads, and the reflection paths `ProtocolMembership.HasClrProtocol` / `TypeInferenceService.TryInferClrBinaryOp` (operators are unioned from BOTH operand types, so a cross-type operator is an overload, not a checker arm). Read the CALLER of any function named `*Fallback`/`*Default` before accepting it as the seam. A rung-4 rule where rung 2 can carry the fact is an error and the plan is NEEDS REVISION until the decision moves down or justifies staying

Flag as error: violations that would break the pipeline, and a name-keyed rule with an available lower rung. Flag as warning: suboptimal placement.

### 4. Correctness

- **Compilation**: will the proposed C# compile? Type errors, missing usings, wrong signatures
- **Edge cases**: empty collections, null, error recovery, `UnknownType` handling
- **Python behavior**: verify every claim with `python3 -c "..."`
- **Type narrowing**: interactions with `_narrowingContext` (`TypeNarrowingContext`) and `NarrowingFlowAnalysis`
- **Variable versioning**: local-variable CodeGen changes vs `_variableVersions` / per-function local spellings on `CodeGenInfo`
- **SPY0908 policy** (`docs/design/spy0908-policy.md`): SPY0908 is a net, not an error channel. A plan that "fixes" an SPY0908 must name the **semantic check or materialized lowering** that replaces it — "emit different C#" alone is not a fix. Reproduce the ICE with `run`. Before the plan designs a lowering for a mistyped expression, apply the untyped-vs-mistyped probe: `b: bool = expr` → SPY0220 means mistyped (fix the mapping arm); silence means `Unknown` (the bug is upstream; a lowering built now never fires).
- **Lowering-stage interactions**: if the plan changes emitted shapes, check whether `Lowering/` already owns them (`LoweringPass*.cs`, `IrPassManager` passes, invoked from `Project/ProjectCompiler.CodeGen.cs` `BuildLoweringIr`) and whether the emitter reads a pass side-table the plan would bypass or duplicate

Flag as warning: unchecked edge cases. Flag as error: demonstrably incorrect claims (say which probe refuted them).

### 5. Completeness

- **All pipeline phases covered**: a new language feature covers Lexer through Tests, plus the spec section and executed examples as a deliverable of equal weight
- **Tests specified**: every implementation change has a test; negative fixtures *and* positive controls
- **New tokens**: flow through Parser, Semantic, CodeGen
- **New AST nodes**: handled in TypeChecker and RoslynEmitter (and the LSP handlers that walk the AST)
- **New validators**: registered in `ValidationPipelineFactory` with correct `Order`; `ValidationPipelineTests` count updated
- **New diagnostics**: `DiagnosticCodes` + `DiagnosticExplanations` entry
- **Imports/usings**: new dependencies imported
- **Issues to Close**: each row carries its close criterion (an acceptance bullet), not just a phase number

Flag as warning: missing steps. Add them as suggestions in the Verification Summary.

### 6. Adequacy (class, not cell)

Applies whenever any input to the plan is a bug, ICE, or regression issue. Grade the plan's **Defect Class** section against `verification-contract.md` §1:

- **Violated contract named** — one sentence: what is supposed to be uniform across which axis
- **Matrix larger than the repro lists** — the issues' repros are a symptom report, not a test plan; the plan enumerates sibling cells (position × operand form, callee kind × usage form, route × entry point, warm × cold …). Check it against the touched dispatch/switch sites yourself: list the arms; every arm the fix does not reach is a cell the plan must name. **This bullet applies to a plan without a generator.** When the `Generator:` row cites stage 0, grade **axis closure from the code**, never by naming cells: run contract §11's Checks (the path rule, then both mutations, by `cp`) and read the test. Each axis is the image of a compiler enumeration (enum, switch arms, roster census) under an explicit map with no default arm, or a closed literal set; the totality assertion covers every map (each source value mapped or in an `excluded…` list with its reason; each roster value the image of a source value; each case's axis values roster values); the expected result is a RULE keyed on axis-value classes; the plan names the excluded axes with reasons. A cell you find by hand means one ROSTER is open: the finding is "close axis X against enumeration Y", answered by a new stage-0 commit, never "add cell Z". Do not probe sibling cells of a closed axis; its run and allowlist pin them. A generator whose every axis is closed is **CLASS** even when you can name a case it generates red. A reflection-sourced roster is graded as contract §12 Convergence says.
- **Meta-class + standard cure** — a row of the table in `verification-contract.md` §1; the cure is a seam, sweep, or harness — not N patches
- **Standing harness** — the `gap-discovery-contracts.md` harness that should have caught it and which allowlist entries the fix drains, or "none; Phase N adds one"
- **Owner rulings** — cited by issue-comment link; the plan does not re-ask a ruled question

A plan that patches one arm of a mirrored/parallel-site structure without a completeness scan is **NEEDS REVISION** — regardless of Dimensions 1–5. If the plan has no Defect Class section for a bug input, draft one from the issues and the touched sites, insert it, list it under Corrections Made, and grade the plan `CELL`. A drafted Defect Class is the author's only once a `## Revision log` line holds `drafted Defect Class: confirmed` or `drafted Defect Class: replaced` verbatim; check for it before grading it as theirs.

Adequacy verdict: **CLASS** (contract, matrix, cure, harness all present and checked — for a generator plan: every axis roster-closed with its totality assertion), **CELL** (any missing, or the fix lands on one arm — for a generator plan: an axis carried as a hand list, named by the enumeration that closes it), **N/A** (no bug/ICE/regression input — docs-only, feature work, tooling).

### 7. Falsifiability

For each test, guard, harness, or fixture the plan proposes (`verification-contract.md` §2–§3):

- **Which mutation turns it red** — the plan names the inversion/revert (invert the predicate, revert the production hunk) and the expected red count; a guard with no named mutation gets a warning naming the concrete mutation the verifier expects
- **Is the exemption the subject** — if the test's own exemption is the predicate under test, breaking the code makes the guard greener; the plan must parameterize the one falsifiable arm
- **Positive control for absence assertions** — "no SPY0908", "0 hits", "no warning" must be paired with the same probe shown to hit when the thing is present
- **Inert-fix check** — the plan says how it will show the new test goes red with the fix reverted (a fallback one call later is the usual cause of an inert fix)
- **Refusals by direction** — any new diagnostic lists the by-direction probe: the same programs on the base commit, classified `ICE` / `wrong output` / `worked` (a `worked` → refused is a regression wearing a diagnostic)
- **Discriminating outputs** — an expected output that would print the same with the bug present proves nothing; flag it

Flag as warning per guard, with the mutation named. Flag as error: a plan whose only evidence for a fix is the issue's own repro.

### 8. Blast Radius & Gates

`verification-contract.md` §5–§7:

- **Sweeps named** — the plan lists which of `Sharpy.Stdlib.Tests`, `Sharpy.Cli.Tests`, `FrontEndParityTests` (LSP), `InteropConformance`, `MetamorphicCorpus`, `DifferentialExecution`, and the warm/cold harness its changes reach (anything in `Semantic/` or `CodeGen/` reaches all of them)
- **Whole-solution gate named** — `/ci-gate` across all projects as the commit gate, with `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"` as its fallback (contract §6); filtered runs only as the edit loop; counts reported `@ sha (measured)`; a red attributed only after a control run at the base sha
- **Regen scheduled early** — spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger regenerated right after the first codegen-touching task, not at push time
- **Generated artifacts listed** — which of the above the plan's changes will touch
- **Allowlist deltas** — entries the fix drains (deleted in the same commit); no entry added without an issue

Flag as warning: any missing item; add it as a Missing Step.

### 9. Scope & Convergence

`verification-contract.md` §10–§11:

- **One group** — the plan's `## Scope` block names one non-chore group; chores riding along touch no seam the group touches (grep the chores' files against the group's). A second group, or an output/layout-changing phase sharing a gate with anything, is an error unless `override: <owner's words>` is recorded.
- **Generator first** — if the Defect Class matrix has ≥2 axes × ≥3 values, or the group is a residue (second suffix), stage 0 holds the generator (or the first task does, with the product hook it needs; contract §11), and the hand matrix covers only the axis the generator cannot reach (the plan says which). Missing = error.
- **Filing rule** — the plan names the class trackers it posts cells to and instructs no implementer to `gh issue create`; cells go to the findings ledger and the lead files by class. A Phase 0 that says "file the siblings" one per cell is a warning; correct it inline.
- **Fidelity scope** — every Python-parity cell names its row in `python-fidelity-scope.md`; a DEVIATION-by-default surface without a promotion ruling is an error (the plan is fixing a non-bug).
- **Filing budget** — the plan states the expected verify cost (matrices, gates) and the prober's budget (matrix + ≤2 axis changes).

Flag as error: a second group, a missing generator where required, a DEVIATION surface planned as a bug. Flag as warning: the rest; correct inline.

## Output

After verification, edit the plan file directly.

### 1. Add the verification stamp at the very top of the file

```markdown
<!-- Verified by /verify-plan on YYYY-MM-DD @ <sha> -->
<!-- Verification result: [PASS / PASS WITH CORRECTIONS / NEEDS REVISION / CONVERGED] -->
<!-- Adequacy: [CLASS / CELL / N/A] -->
<!-- Scope: [ONE-GROUP / MULTI-GROUP (override) / N/A] · Generator: [existing / built-first / not-required / MISSING] -->
```

In `--adequacy` mode all four stamp lines are written: the first reads `<!-- Verified by /verify-plan --adequacy (Adequacy-only: Dimensions 4-design, 6, 7; check_plan_refs clean) on YYYY-MM-DD @ <sha> -->`; the result is `PASS`, `NEEDS REVISION` or `CONVERGED`; `Adequacy:` is Dimension 6's verdict; `Scope:` is copied from the plan's `## Scope` block and `Generator:` row without grading Dimension 9 (a light plan that reads `MULTI-GROUP` or needs a generator is `NEEDS REVISION: promote to full`, §12). The Verification Summary carries only `### Adequacy`, `### Falsifiability`, `### Design decisions checked` and `### Unchecked Claims`, plus `### Deferred findings` on `CONVERGED`; downstream skills read the stamp as a full one.

Result:
- **PASS** — no errors; at most minor suggestions
- **PASS WITH CORRECTIONS** — errors found and corrected inline; the plan is now accurate
- **NEEDS REVISION** — architectural or correctness issues that need the author's judgment, **or Dimension 6 failed** (a cell-only bug-fix plan is NEEDS REVISION however accurate its references), **or Dimension 9 failed** (a second non-chore group without an override, a residue group without its generator, a DEVIATION-by-default surface planned as a bug)
- **CONVERGED** — contract §12 Convergence holds: the `## Revision log` shows the previous pass `NEEDS REVISION, Dimension N` on the SAME dimension, and every finding of this pass is **deferrable** as §12 defines it (one that is not keeps NEEDS REVISION). List them under `### Deferred findings` (each `<roster or claim> ← <enumeration> — fix: <code change> — proved by <assertion / mutation / build / generator>`) and route to `/implement-plan`. A first NEEDS REVISION always earns a revision; `CELL` on a bug-fix plan still needs the owner's word at `/implement-plan`.

Adequacy: `CLASS` / `CELL` / `N/A` per Dimension 6. `/implement-plan` refuses a `CELL` plan for bug-fix inputs unless the user explicitly overrides or the result is `CONVERGED`; `N/A` is for plans with no bug/ICE/regression input.

### 2. Add a Verification Summary section at the end of the plan

```markdown
## Verification Summary

**Result:** [PASS / PASS WITH CORRECTIONS / NEEDS REVISION / CONVERGED]
**Adequacy:** [CLASS / CELL / N/A]
**Verified on:** YYYY-MM-DD @ <sha>
**Plan file:** `docs/process/plans/<plan-id>.md` (committed in §4 at every result, NEEDS REVISION included)

### Corrections Made
- (each correction and added section, before/after)

### Adequacy
- **Contract:** [named / missing] — <the sentence, or what the verifier drafted>
- **Matrix:** [larger than repro lists / equals repro lists / missing] — <cells the verifier found the plan does not name; on a generator plan this line reads instead **Axes:** [all roster-closed / open: <axis> ← <the enumeration that closes it>] and names no cells>
- **Cure:** [seam / sweep / harness / N patches] — <meta-class row>
- **Harness:** [named + allowlist delta / "Phase N adds one" / missing]
- **Layer:** [every decision rung-justified / rung-4 without justification: <decision, and the rung that could carry it>]

### Scope & convergence
- **Group:** [one / MULTI: <groups> / override: <words>] · **Alone:** [yes/no — required? yes/no] · **Gates:** N
- **Generator:** [existing <name> / stage 0 @ <sha>, mutation re-run / not required: <axis> / MISSING — expected: <harness shape>]
- **Trackers named:** [#… / MISSING] · **Fidelity rows:** [named / DEVIATION surface planned: <issue>]

### Falsifiability
- <guard name>: mutation [named: … / MISSING — expected: …]; exemption [not subject / IS subject]; positive control [yes / n/a / MISSING]; by-direction probe [specified / MISSING]
- (one row per proposed test/guard/harness)

### Deferred findings
- (CONVERGED only — each finding as `<roster or claim> ← <enumeration> — fix: <code change> — proved by <assertion / mutation / build / generator>`; a row that names no proof is not deferrable; `/implement-plan` makes these rows its first task)

### Warnings
- (each warning with explanation)

### Missing Steps Added
- (suggestions for missing steps, including blast-radius/gate/regen items)

### Unchecked Claims
- (claims that couldn't be verified, with the reason and what would verify them)
```

### 3. Fix errors in place

Rewrite the plan text so it reads as if first written correctly: no `[CORRECTED…]` or `[ADDED…]` markers, no pass history, no "(first pass)" tags. Each change's before/after lives only in the Verification Summary.

Present a brief summary to the user: result, adequacy verdict, the corrections that changed a decision (not every path fix), the warnings the author must act on before `/implement-plan`, and the commit that holds the stamped plan (§4).

### 4. Commit the stamped plan

Every pass is committed, whatever its result: a NEEDS REVISION summary left in the gitignored working copy is lost with its container. Copy the working copy over `docs/process/plans/<plan-id>.md` and commit that one file on the plan's branch (`docs/process/README.md` › The stage trail: `claude/<group>-plan` with its pull request for a `full` plan; `claude/<group>`, no pull request yet, for a `light` one), then `/push`. In a fresh container, `git fetch origin && git switch <branch>` first; if a `full` plan's plan PR already merged, cut `claude/<group>-plan` again from `origin/dev` with a new PR; a plan held only as a working copy is committed here for the first time.

**Never commit a pass on `dev`**: its ruleset takes pull requests only, so the stamp could not leave the container. Settle the branch before the first edit: `git fetch origin`, then `git switch <branch> && git merge --ff-only` if `origin/<branch>` exists, else `git switch -c <branch> origin/dev`. A commit already made on local `dev`: get onto the branch the same way, `git cherry-pick origin/dev..dev`, then `git branch -f dev origin/dev`. Pushing that branch and opening a `full` plan's pull request is this stage's own step: do it unasked and name both in the summary. If the session's instructions forbid another branch or a pull request, ask once, before the pass starts.

```bash
git add docs/process/plans/<plan-id>.md     # explicit pathspec; `git diff --cached --stat` shows this one file
git commit -m "process(plan): <plan-id> — verified <result> @ <sha>"
```

The routing line names the committed path: `/implement-plan docs/process/plans/<plan-id>.md` on PASS, PASS WITH CORRECTIONS or CONVERGED; `/create-plan revise docs/process/plans/<plan-id>.md` on NEEDS REVISION.

### 5. After NEEDS REVISION or CONVERGED

The revision is `/create-plan revise <path>` (its **Revising a plan** section) in a **fresh context**; it answers each finding by rewriting the text the finding names, never by appending: the verifier that stamped NEEDS REVISION does not revise unless the user says so, and the next pass must be a fresh read. `/implement-plan` stays refused until a new stamp reads PASS or CONVERGED. On **CONVERGED** there is no revision, and the design sections contract §12 Convergence names are not edited.
