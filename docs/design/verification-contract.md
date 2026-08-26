# The verification contract

> **Status:** Policy — 2026-08-26. Applies to every plan, implementation round, verification round,
> and audit in this repository. The workflow skills (`/create-plan`, `/verify-plan`,
> `/implement-plan`, `/verify-implementation`, `/compiler-audit`) open by citing this file; the
> agent definitions under `.claude/agents/` carry the parts that apply to them.
> **Companions:** [gap-discovery-contracts.md](gap-discovery-contracts.md) (the standing class
> harnesses and their ratchet), [spy0908-policy.md](spy0908-policy.md) (SPY0908 is a net, not an
> error channel).

Every item below is a rule, the incident that taught it, and the **mechanical check** — what to
run and what result is red. A verifier that cannot name the check it ran has not applied the
item. The stance throughout is *refute, not confirm*: an audit reports NOT REFUTED only after
naming what it tried.

## 1. Class before cell

A bug is closed by enforcing its **class contract**, not by patching the cell the issue named.
Name the contract that was violated ("`callee[T]` resolves identically for every callee kind",
"the same check fires in every syntactic position"), enumerate the **cell matrix** the contract
ranges over (position × operand form, callee kind × usage form, route table × entry point, warm
× cold …), and treat the issue's repro list as a **symptom report, not a test plan**: measure
more cells than the issue names, and when every spelling you vary agrees, change axis —
agreement is evidence the defect is not on that axis.

**Recurring meta-classes and their standard cures** (the round plans' §2 table; extend it there,
not here):

| Meta-class | Standard cure |
|---|---|
| **SPY0908 as a net** — the C# compiler is doing the refusing | Semantic-time refusal or a materialized lowering that names its check; never document-and-close ([spy0908-policy.md](spy0908-policy.md)) |
| **Silent wrong behavior** — worse than an ICE | Fix first, then a conformance sweep over the sibling cells so the class cannot recur |
| **Qualified/bare & alias divergence** | ONE shared qualified-resolution helper + ONE alias-dereference point re-entering the bare path; the sweep allowlist drains on fix |
| **Position/route missing a shared check** | Move the check to the shared seam; a position × type (or route-table) matrix so a new position cannot skip it |
| **Warm ≠ cold divergence** | Per-analysis state is never trusted after a restore; a warm/cold differential harness |
| **Instruments that pass vacuously** | Fail loudly; a shared helper makes the wrong pattern unwritable; mutation-test the guard (§2) |

*Why:* #1209 (`list("abc")`) and #1220 (`f = dict; f(a=1)` → empty dict) each shipped a defect in
a spelling absent from their own repro lists; #1243 went out as "module-route" when it reproduced
single-file because every spelling varied lay on the wrong axis. The parallel-site class (#1105,
#1106, #1135, #1124, #1125, #1150–#1152) is nothing but one arm of a mirrored structure fixed while
its twin was left alone.

*Check:* the plan (or the close-out) names the contract, the matrix, and the standing harness that
should have caught the defect (or the phase that adds one). Red = a fix that touches one arm of a
mirrored/parallel-site structure with no completeness scan, or a close-out whose evidence is only
the issue's own repro.

## 2. Guards are falsifiable

Before claiming that a test guards a defect, **break the guarded thing and watch the test go red**:
invert the predicate, or revert the production hunk, run the test, read the counter, then restore.
Three outcomes, all of which must be reported:

- **Vacuous guard** — the test stays green with the guard disabled. Do not ship it as a guard.
- **Inverted exemption** — the mutation flows *through* the test's own exemption, so breaking the
  code makes the guard greener. Find the one arm of the predicate whose answer is falsifiable and
  parameterize on it; the exemption must never be the subject.
- **Inert fix** — reverting the production change leaves the new test green. The usual cause is a
  fallback path one call later with identical logic; no input distinguishes the two versions.
  Report it as a finding, not a fix.

An **absence assertion** (grep for zero hits, "no SPY0908", "no warning") needs a **positive
control** on the same input — the same probe must be shown to hit when the thing is present.
Restore mutations with `cp`/`patch` from a copy you made, never with `git checkout`/`restore` in a
shared tree (§9).

*Why:* #1263's rename-dedupe test passed with the dedupe disabled (`true || _seen.Add(...)`), and
the defect turned out not to reproduce at all; #1262's overload-guard fix was inert
(`ResolveImportedFunctionOverload` ran one call later with identical logic); #1351's splice
tripwire exempted every decline *by definition* because the exemption was the emitter's own
predicate; the 2026-08-25 round shipped a fix **and** its harness both vacuous. On the other side,
#1310's staleness check and #851's completion arm both went red under mutation — that is what a
load-bearing guard looks like.

*Check:* for every new test/guard/harness, the commit body records both outcomes — "broken →
red (N failed), restored → green (N passed)". Red = a guard with no recorded mutation, a guard
that stayed green when broken, or an absence assertion with no positive control.

## 3. Refusals are verified by direction

A change that turns a program red must be classified **before/after against the prior commit**:
"replaces an ICE with a diagnostic" is a fix; "restricts a program that used to compile and run
correctly" is a regression wearing a diagnostic. SPY0908 (generated C# failed to compile) is raised
at the C#-compile stage, so it **surfaces only under `run`**, never under `emit csharp` or `emit
diagnostics` — and, conversely, a failing `run` prints only errors, so warnings on that file are
visible only under `emit diagnostics`. When an ICE shows a C# type mismatch, probe **`b: bool =
expr`** first: SPY0220 means the checker has an opinion (mistyped — fix the mapping arm); silence
means the expression is `Unknown` (untyped — the bug is upstream and a lowering built now will
never fire). `print(x)` is never a type probe.

*Why:* #1250 was nearly dismissed as unreproducible because it was checked under `emit`; the
#1358 audit read "no warning" off a failing `run` that cannot print warnings; Batch F (#1291)
diagnosed `char[]` elements as "typed `str` but emitted `char`" when they were `Unknown`, so the
materialization written first could never have fired; #1248 converted a loud SPY0908 into a
silently wrong construction — a regression *in kind*, visible only by running all three cells
before and after.

*Check:* every refusal added by a change lists the programs it newly rejects and, for each, the
prior commit's behavior (`ICE` / `wrong output` / `worked`). Red = a newly rejected program that
worked correctly before, or an ICE report refuted with anything other than `run`.

## 4. Recorded ≠ applied

When a change records a fact the emitter consumes — a node-keyed `SemanticInfo` entry, a
materialized `CodeGenInfo` member, a projection mark, a filled type-argument vector — verifying
the semantic layer proves nothing about the output. **Run the program and compare its values**
end-to-end (against `python3` where the semantics are Python's), not its exit code and not the
diagnostic count. A new node-keyed `SemanticInfo` dictionary that is not in
`SemanticInfo.MergeFrom` is silently dropped in the per-file → project merge (CLAUDE.md Rule 2).

*Why:* #1219 filled the type-argument vector correctly and the emitter built its list from the AST
index (`Pair<int>` → CS0305); #1209 recorded the projection mark and the emitter's own special case
projected twice; #1220 typed the kwargs and the constructor-reference call never read them. Each
passed every diagnostic-level check.

*Check:* the change's test executes the program and asserts on stdout/values. Red = a
"recorded" fact with no executing test, or a new `SemanticInfo` dictionary absent from `MergeFrom`.

## 5. Measurements carry their sha

A test count is a measurement only when it was **measured at a named commit**: report
`passed/failed/skipped @ <sha> (measured)`, never a derived number in the same format. Before
attributing a failure to a change, **run the control** — the same suite on the tree *without* the
change (a `git worktree` at the base commit). An instrument is trusted only after it has been
shown to detect the no-difference case (two cold builds differ by MVID; a probe whose construct
was never emitted passes while exercising nothing; run position alone swings a benchmark 7–10%).
A failing step invalidates its **dependents, not its siblings**: complete every step that does
not consume the failed one and label which readings are suspect. "Unmeasured" describes your
effort, not the hazard — say "unmeasured by me, in N minutes" or measure it.

*Why:* Batch 2's close was ledgered as `20,223` when its last measured run was `20,219` and the
real number two batches later was `20,243` — every downstream "+N vs baseline" was off by the
derivation; a `ResolveDunderOverload` change "verified clean" in `Sharpy.Compiler.Tests` ICEd two
`Sharpy.Stdlib.Tests` fixtures and a peer was nearly blamed; round 8's closing gate stopped at a
stale-artifact red and withheld two real `ConstFoldPassTests` failures; a TOCTOU race ruled
"probability unmeasured" admitted 4 of 8 concurrent wrappers when someone measured it.

*Check:* every count in a report names its sha and method; every attributed red names its control
run. Red = a count without a sha, a "regression" with no control at the base commit, or a gate
that stopped before running its independent steps.

## 6. The commit gate is the whole solution

The gate is `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"` across **all**
test projects, run in the background while doing read-only work; a filtered run is the edit
loop, never the gate. Ask what a change can **reach**, not which file it lives in: anything in
`Semantic/` or `CodeGen/` surfaces in every project that compiles Sharpy source —
`Sharpy.Stdlib.Tests`, `Sharpy.Cli.Tests`, the LSP front-end parity sweep (`FrontEndParityTests`),
and the three GapDiscovery sweeps CI runs as *separate steps* (`InteropConformance` #1034,
`MetamorphicCorpus` #1157, `DifferentialExecution` #1202 — the main Compiler step excludes them,
so a local whole-solution run is the only thing that covers all three at once). A full run is
~22 min wall clock; read `.claude/tmp/dotnet-serialized-*.log` rather than re-running. A log
whose binaries predate the change is not a green.

*Why:* Batch G verified four phases with `FullyQualifiedName~FileBasedIntegrationTests` and pushed
10 live failures — unit tests with no symbol table, a validator-count pin, missing
`DiagnosticExplanations` entries, an interop ratchet, and six stale expectations in another
project; Batch 10's own full-suite log was a stale-binary false green; the `ResolveDunderOverload`
ICE lived only in Stdlib.Tests.

*Check:* the commit that closes a phase cites a whole-solution run at its sha. Red = a close-out
whose only evidence is a filtered run, a log older than the last build, or a sweep the change
reaches that was not run.

## 7. Regenerate early; read the diff as findings

Spy-sourced stdlib C# (`build_tools/regenerate_spy_stdlib.sh`), spy-test C#, stdlib docs
(`build_tools stdlib generate --force`) and the oracle ledger are **generated from the compiler
under test**. Regenerate them at the **start** of any round that touches semantics or codegen,
not as a pre-push chore, and read the regeneration diff as findings: a newly red regenerated test
is a live regression until disproven, and a committed artifact that predates the change is a
fossil that keeps voting green in CI. `/push` runs the staleness gates that CI enforces.

*Why:* `glob_tests.cs` was regenerated one round late and turned red on one line — `glob.iglob`
had stopped being lazy (#1251 → #1354) two rounds earlier, and the fossil had been compiling the
old compiler's output the whole time; the 2026-08-19 batch landed with a broken spy-test regen the
implementer never ran.

*Check:* the round's first codegen-touching commit is followed by a regen commit (or "regen:
no diff @ sha"). Red = a staleness gate failing at `/push` time, or a regen diff attributed to
"noise" without a probe.

## 8. Closed ≠ finished

Completion is derived from **issue state plus the plan's own acceptance bullets and "Issues to
Close"**, never from "the code landed" or from session notes. A close cites execution evidence
at a named sha. Docs and spec deliverables weigh the same as code: run every spec/explanation
example against HEAD before committing it, and prefer examples whose outputs **discriminate**
(an example that prints the same thing with the bug present proves nothing). Every allowlist
entry a fix drains is deleted in the same commit; no entry is ever added without an issue
reference. Two tells of an unfinished close: a new file with **no callers** (`grep -rc` the
symbol in its consumer project), and a promised spec section that greps to nothing.

*Why:* #1315 closed with its spec section unwritten and one acceptance cell never implemented
(`1 << -1` still compiled to `1 << 31`); `NumericCheckedCast.cs` shipped with zero consumers
(`big as! int` still printed 0); `d090e6cd4` added a spec sentence the compiler contradicted
(#1250); plans recorded as "DONE (core phases)" carried 18 open issues.

*Check:* the close-out lists each acceptance bullet with its evidence (`what ran @ sha`). Red =
an issue closed on a bullet with no evidence, a stale allowlist entry after its fix, a spec
example that was not executed, or a helper with no callers.

## 9. Shared-tree contract for agents

Paste this block **verbatim** into every agent prompt that touches the repository:

> The working tree is shared with other agents. Never run `git checkout`, `git restore`,
> `git clean`, `git stash`, `git reset`, or `rm` on repository paths. REPORT `git status`; do not
> "make it clean". Stage with explicit per-file pathspecs and check `git diff --cached --stat`
> before committing; never `git add -A` or `git add .`. Restore a mutation-test from the copy you
> made (`cp`), never from git. Never run `dotnet` directly — use `.claude/scripts/dotnet-serialized`
> with `dangerouslyDisableSandbox: true`.

The lead commits its own work in small slices and re-checks `git diff --stat` after each agent
wave; when the tree is dirty with a peer's work, verify in a `git worktree` at HEAD. Reading a
file and running it are two observations of something that can change between them — pin the
bytes (sha or worktree) before pairing them.

*Why:* "read-only" as an instruction has failed twice — agents read `git restore` as cleanup;
a silent revert leaves no stash and no reflog entry. `git add <file>` stages a peer's uncommitted
hunks in that file too. A fixture measured mid-edit by a peer produced a "context-sensitive"
defect that was a renamed parameter.

*Check:* every agent prompt in the round contains the block; the lead's `git diff --stat` after
each wave matches the wave's declared scope. Red = an agent prompt without the block, or a
working-tree delta nobody claims.
