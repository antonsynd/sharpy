---
name: verify-plan
description: Verify a plan for accuracy, architectural soundness, and adequacy (class, not cell)
argument-hint: "<path/to/plan.md>"
---

Read `docs/design/verification-contract.md` before proceeding; every dimension below applies it. **Stance: refute** — for each claim, look for the input that shows it wrong; a dimension passes only after naming what was tried. A plan whose file paths are all accurate can still fail: a bug-fix plan that patches one cell of a class is **NEEDS REVISION**, however correct its references.

Reads the plan, extracts every verifiable claim, checks each against the codebase at a named sha, grades the plan's *adequacy* against the defect class it claims to fix, and edits the plan directly: corrections inline, a stamp at the top, a Verification Summary at the end.

## Argument Handling

If `$ARGUMENTS` is non-empty, use it as the path to the plan file.

If `$ARGUMENTS` is empty, plans are split across two directories — `.claude/plans/` (repo-local, gitignored; the default since 2026-08-26) and `~/.claude/plans/` (older per-batch plans). Never pick one silently: list the three newest across both and ask which to verify.
```bash
ls -t .claude/plans/*.md ~/.claude/plans/*.md 2>/dev/null | head -3
```

Read the plan file completely before proceeding. Record `git rev-parse --short HEAD` — every check below is made against that sha, and the stamp names it (line numbers drift; a sha does not).

## Probing rules

- Never run `dotnet` directly — `.claude/scripts/dotnet-serialized` (drop-in wrapper; needs `dangerouslyDisableSandbox: true`), or the `/spy-run` / `/spy-emit` / `/quick-check` skills for inline Sharpy source.
- An ICE claim (SPY0908 / CS-leak) is confirmed or refuted with **`run`**, never `emit` — `emit csharp` succeeds on broken C#, and `emit diagnostics` cannot see the C# stage. A failing `run` cannot show warnings; use `emit diagnostics` for those.
- `python3 -c "..."` for every claimed Python behavior; `print(x)` is never a type probe — bind to a deliberately wrong annotation (`b: bool = expr`).
- `gh` needs `dangerouslyDisableSandbox: true` (TLS in the sandbox).

## Verification Dimensions

Check each dimension in order. For each claim, verify against the codebase with Glob, Grep, and Read (or the MCP graph servers when connected). Record what was tried for each — "not refuted" without a named probe is not a pass.

### 1. Structural Accuracy

Verify every concrete reference in the plan:
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

Flag as error: violations that would break the pipeline. Flag as warning: suboptimal placement.

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
- **Matrix larger than the repro lists** — the issues' repros are a symptom report, not a test plan; the plan enumerates sibling cells (position × operand form, callee kind × usage form, route × entry point, warm × cold …). Check it against the touched dispatch/switch sites yourself: list the arms; every arm the fix does not reach is a cell the plan must name.
- **Meta-class + standard cure** — from the table in `verification-contract.md` §1 (SPY0908-as-net, silent wrong behavior, qualified/bare divergence, position missing a shared check, warm ≠ cold, vacuous instrument); the cure is a seam, sweep, or harness — not N patches
- **Standing harness** — the `gap-discovery-contracts.md` harness that should have caught it and which allowlist entries the fix drains, or "none; Phase N adds one"
- **Owner rulings** — cited by issue-comment link; the plan does not re-ask a ruled question

A plan that patches one arm of a mirrored/parallel-site structure without a completeness scan is **NEEDS REVISION** — regardless of Dimensions 1–5. If the plan has no Defect Class section for a bug input, draft one from the issues and the touched sites, insert it marked `[ADDED: adequacy]` for the author, and grade the plan `CELL` until the author confirms the matrix.

Adequacy verdict: **CLASS** (contract, matrix, cure, harness all present and checked), **CELL** (any missing, or the fix lands on one arm), **N/A** (no bug/ICE/regression input — docs-only, feature work, tooling).

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
- **Whole-solution gate named** — `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"` across all projects as the commit gate; filtered runs only as the edit loop; counts reported `@ sha (measured)`; a red attributed only after a control run at the base sha
- **Regen scheduled early** — spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger regenerated right after the first codegen-touching task, not at push time
- **Generated artifacts listed** — which of the above the plan's changes will touch
- **Allowlist deltas** — entries the fix drains (deleted in the same commit); no entry added without an issue

Flag as warning: any missing item; add it as a Missing Step.

## Output

After verification, edit the plan file directly.

### 1. Add the verification stamp at the very top of the file

```markdown
<!-- Verified by /verify-plan on YYYY-MM-DD @ <sha> -->
<!-- Verification result: [PASS / PASS WITH CORRECTIONS / NEEDS REVISION] -->
<!-- Adequacy: [CLASS / CELL / N/A] -->
```

Result:
- **PASS** — no errors; at most minor suggestions
- **PASS WITH CORRECTIONS** — errors found and corrected inline; the plan is now accurate
- **NEEDS REVISION** — architectural or correctness issues that need the author's judgment, **or Dimension 6 failed** (a cell-only bug-fix plan is NEEDS REVISION however accurate its references)

Adequacy: `CLASS` / `CELL` / `N/A` per Dimension 6. `/implement-plan` refuses a `CELL` plan for bug-fix inputs unless the user explicitly overrides; `N/A` is for plans with no bug/ICE/regression input.

### 2. Add a Verification Summary section at the end of the plan

```markdown
## Verification Summary

**Result:** [PASS / PASS WITH CORRECTIONS / NEEDS REVISION]
**Adequacy:** [CLASS / CELL / N/A]
**Verified on:** YYYY-MM-DD @ <sha>
**Plan file:** [path]

### Corrections Made
- (each inline correction with before/after)

### Adequacy
- **Contract:** [named / missing] — <the sentence, or what the verifier drafted>
- **Matrix:** [larger than repro lists / equals repro lists / missing] — <cells the verifier found the plan does not name>
- **Cure:** [seam / sweep / harness / N patches] — <meta-class row>
- **Harness:** [named + allowlist delta / "Phase N adds one" / missing]

### Falsifiability
- <guard name>: mutation [named: … / MISSING — expected: …]; exemption [not subject / IS subject]; positive control [yes / n/a / MISSING]; by-direction probe [specified / MISSING]
- (one row per proposed test/guard/harness)

### Warnings
- (each warning with explanation)

### Missing Steps Added
- (suggestions for missing steps, including blast-radius/gate/regen items)

### Unchecked Claims
- (claims that couldn't be verified, with the reason and what would verify them)
```

### 3. Fix errors inline

For each error, edit the plan text directly. Mark corrections `[CORRECTED: reason]`; mark drafted sections `[ADDED: adequacy]`; mark added steps `[ADDED: missing step]` so the author can review every change.

Present a brief summary to the user: result, adequacy verdict, the corrections that changed a decision (not every path fix), and the warnings the author must act on before `/implement-plan`.
