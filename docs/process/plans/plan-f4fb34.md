<!-- Verified by /verify-plan on 2026-10-15 @ f8c37f986 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: existing (directory-scan universe + raw sampling; hand axis = the row list) -->
# P63 — Typed-generator feature roster (#2288 items 1, 3, 4)

## Context

`src/Sharpy.Compiler.Tests/Properties/Generators/Typed/` generates well-typed Sharpy programs by construction and
two property lanes read them: the **generated** lane (`TypeSoundnessPropertyTests` and the per-feature
`*PropertyTests` — false-refusal + ICE) and the **injected** lane (`ErrorInjector` + `ErrorCompletenessPropertyTests`
— missing-refusal). #2288 (class tracker; lane `tooling`, reach `probe`) names what is missing: a **census** tying the
generator's reach to `docs/language_specification/*.md`, exact-code judging on every refusal lane, and injections for
the typing surfaces the generator already emits but never breaks. Item 2 (the exact-code oracle for `ErrorInjector`)
landed as a chore @ e2446c46b (R-FZ); this plan is items 1, 3 and 4.

- Issue: https://github.com/antonsynd/sharpy/issues/2288
- Ruling R-FZ (owner, 2026-10-08, option a): https://github.com/antonsynd/sharpy/issues/2288#issuecomment-6072442324
- Item 2 landing record (measured drawn codes, N1–N5 mutations): https://github.com/antonsynd/sharpy/issues/2288#issuecomment-6111491824
- Charter: `.claude/plans/remediation-round-2026-09-20-batching.md` §3.6b (row 6b, directly after P54 which is DONE)

## Current State (measured @ f8c37f986, 2026-10-15)

**The instrument.** 19 generator files, 4,657 lines, 78 public `Gen<…>` members plus `SemanticFilter`'s two. Two output
kinds: `Gen<Module>` (an AST the `Unparser` prints — `GenTyped`, `GenClasses`, `GenFunctions`, `GenGenerics`,
`GenInterfaces`, `GenIterators`, `GenExceptions`, `GenDecorators`, `GenAsync`) and `Gen<string>` (source text —
`GenMatchPatterns`, `GenVariance`, `GenOperators`, `GenContextManagers`, `GenFStrings`; `GenImports` yields source
tuples). `ErrorInjector` mutates `Module` only. 24 consumer classes under `Properties/` read the generators
(`grep -rl` @ f8c37f986); 23 of them share `[Collection("PropertySerial")]` (parallelization disabled) — `Properties/Differential/DifferentialExecutionTests` does not [CORRECTED: it is a GapDiscovery sweep, not a property class; verified by grep @ f8c37f986].

**The oracles, by reading** (the class this plan enforces — every row below is a member cell):

| Consumer | Refusal oracle | Verdict |
|---|---|---|
| `ErrorCompletenessPropertyTests` (10), `ClassInheritancePropertyTests.AbstractMethod_MustBeOverridden` | `ErrorInjector.Judge`: exactly one code, every applicable sample, throw = fail, `KnownRedRefusal` pins | **strong** (item 2) |
| `DecoratorPropertyTests.DecoratorStacking_InvalidCombinations` | exact code, every sample | strong |
| `VariancePropertyTests` ×3 (SPY0417/0418/0419), `AsyncPropertyTests.AwaitOutsideAsync` (SPY0273) | exact code but `diagnosed > total / 4` | threshold — 74 % of samples may be unrefused |
| `PatternMatchPropertyTests.MatchNonExhaustive` | `!Success \|\| Any(code…)`, `> total / 4` | any failure counts |
| `InterfacePropertyTests.InterfaceImplementation_FailsWhenMissing` | `StartsWith("SPY03") \|\| StartsWith("SPY04")`, `> total / 4` | prefix |
| `GenericTypePropertyTests.GenericTypeConstraint_IsEnforced` | `Success \|\| Any(StartsWith("SPY0"))`, `catch` counts as handled, `> tested / 2` | **vacuous by construction** — and every sample is SPY0301 (`from collections import Comparable`: no such export), so the constraint lane has never been exercised |
| `GenericTypePropertyTests.WrongTypeArgCount_NeverCrashes`, `TypeMismatchOnGenericField_NeverCrashes` | no throw | no refusal asserted |
| `ContextManagerPropertyTests.MissingEnterOrExit`, `OperatorOverloadPropertyTests.InvalidDunder` | `!Success`, `> total / 2` / `/ 4` | any failure |
| `IteratorPropertyTests.IterProtocol_RequiresBothMethods`, `ExceptionPropertyTests.BareRaise_OnlyInExceptBlock` | `a + b > 0` across two sub-samples | **vacuous** — one accepted valid program satisfies it |
| Soundness lanes: `GenericTypePropertyTests` ×5, `FunctionSemanticsPropertyTests` ×4, `IteratorPropertyTests` ×3, `ExceptionPropertyTests` ×1, `ImportResolutionPropertyTests` ×5 (+1 warning threshold at line 189 and a `StartsWith("SPY0")` refusal lane at line 127), `CombinedFeaturePropertyTests` ×4, `TypedAnalyzePropertyTests` ×1, `ClassInheritancePropertyTests.SuperCall_ResolvesCorrectly` (`passed > 0`) [CORRECTED: counted by `grep -nE '> (total|tested|analyzed|passed) /'` @ f8c37f986 — 38 weak-spelling lines in 15 files under `Properties/Semantic`, not eleven; the Phase 2 scan allowlist seeds with every hit and Phase 3 flips every threshold, or the allowlist cannot reach 0] | `passed > tested / 2` (or `/ 4`) | half the generator's output may be refused |
| `TypeSoundnessPropertyTests` ×4, `ErrorCompletenessPropertyTests` base — and every other `SemanticFilter` caller: `CsCleanPropertyTests` (CodeGen), `DiagnosticSpanPropertyTests`, `SymbolPositionPropertyTests` ×5, `DeterminismPropertyTests`, `TypedAnalyzePropertyTests`, `SemanticInfoConsistencyTests` ×7, `ValidatorOrderingPropertyTests` (24 call sites in 10 classes @ f8c37f986) [CORRECTED: `grep -rn 'SemanticFilter\.'`; Phase 3 deletes the old overloads, so every call site is migrated and every sink asserted] | `SemanticFilter.WellTypedProgram` / `CompilableProgram` **discard** samples the compiler refuses or that throw, silently | the false-refusal lane is unmeasured: a generator whose output is 90 % refused passes |

**Probes @ f8c37f986** (via the built `src/Sharpy.Cli/bin/Debug/net10.0/sharpyc`, binary mtime 15:32 > commit 15:31;
`run`, not `emit`, per contract §3) — the codes the new injections must draw:

| Cell | Program shape | Drew |
|---|---|---|
| Optional stored to `int` without narrowing | `let m: int? = None(); let n: int = m` | SPY0220 |
| Optional operand without narrowing | `let m: int? = Some(1); m + 1` | SPY0222 |
| Use inside the FAILED narrowing branch | `if m is None: let n: int = m` | SPY0220 |
| `isinstance` else-branch member | `if isinstance(a, Dog): … else: a.bark()` | SPY0203 |
| Constraint violated at an INFERRED call | `biggest(Plain(), Plain())` with `[T: Ordered[T]]` (method-based interface) | SPY0237 |
| Constraint violated at an EXPLICIT instantiation | `SortedBox[Plain](Plain())` | **SPY0908 CS0311** — #2212's contract, known red |
| Constraint program, valid control | `biggest(Num(1), Num(2)).v` | runs, prints `2` |
| Spec's own constraint example | `interface IComparable[T]: def __lt__…` | SPY0413 (allowlisted `generics.md::10412babcd # #1952`) |
| `const X` reassigned | `X = 2` | SPY0225 |
| Same-scope `let x` twice | `let x = 1; let x = 2` (inside `def main()` — module-level `let` is SPY0340) | runs, prints `2` — **by spec** (`variable_scoping.md:334`, under `## Variable Shadowing` [CORRECTED: the sentence is in §Variable Shadowing, not §Sibling-Block Redeclaration, which is a `###` under §Assignment Statement and is not a roster anchor]: "`let` always introduces a new variable, so a name can be redeclared in the same scope") |
| `len()` on a class without `__len__` | `len(C())` | SPY0320 |
| Interface member missing | `class Quiet(Speaker)` lacking `speak` | SPY0325 |
| Match expression non-exhaustive | `match x: case True: "yes"` on `bool` | SPY0416 (error) |
| Match STATEMENT non-exhaustive | same, statement form | SPY0463 (**warning** — `Judge` filters `IsError` today). [CORRECTED: re-measured @ f8c37f986 — `GenMatchPatterns.MatchNonExhaustive`'s three variants all put the match inside a `-> str` function with no trailing return, so every sample ALSO draws **SPY0266** (error: must return in all code paths) beside the SPY0463 warning; under the warning arm's "zero errors" rule the lane is red until the generator is repaired (Phase 2 Task 3) so the non-exhaustive match is its ONE broken fact — a `-> None` body or a trailing `return`.] |
| Contravariant parameter in output position | `interface Sink[in T]: def produce(self) -> T` | SPY0419 |
| Override with a different return type | `@override def f(self) -> str` over `-> int` | SPY0908 CS0508 (#2143 pin, unchanged) |
| CLR method with a Sharpy type the bridge refuses | `Math.abs("x")` / `Math.abs([1])` | SPY0220 — but **no typed generator emits a CLR import** (`GenImports` is Sharpy modules only) |

**The spec universe.** 125 construct-bearing `.md` files beside `README.md` (126 entries, all `.md`); 15 have no
`##` heading (`break_continue.md`, `if_statement.md`, …: a title, one block, an *Implementation* line); heading
names are heterogeneous ("Overview", "See Also", "C# Emission", "Implementation Details" beside "Type Constraints"
and "Exhaustiveness Checking"). Four files are not linked from `README.md` (`diagnostic_suppression.md`,
`name_shadowing.md`, `typing_equivalences.md`, `unittest.md`) — the roster reads the directory, never the README.

**Mechanics the plan reuses.** `FixtureRoots.RepositoryRoot` (`src/Sharpy.TestInfrastructure/Integration/FixtureRoots.cs`, the `Sharpy.TestInfrastructure` project [CORRECTED: path]) locates repo files from the test
binaries; the literal-roster pattern is `StdlibModuleGlobalRosterTests` (`src/Sharpy.Stdlib.Tests/Conformance/`; P54: rows + a totality anchor + a stale-row
check) and `AstConstructionCensusTests` (reflection universe + checked roster rows); `build_tools/allowlist_issue_state.py`
scans C# for `new \w*KnownRed\w*("#N"` (`KNOWNRED_CELL_RE`) and for `*-allowlist.txt` rows, so a pinned cell citing a
CLOSED issue reddens the `/push` gate. CI runs the property classes inside the compiler shards
(`COMPILER_MAIN_FILTER` excludes only the five GapDiscovery sweeps); `xunit.runner.json` is `maxParallelThreads: 2`.

## Scope

- **Group:** P63 — typed-generator feature roster (§3.6b; one non-chore group; chores riding along: **none** — the
  §3.0 chores band rides P21e's gate by R-HK, not this one)
- **Alone:** no — tests only; no printed output, emitted layout or snapshot moves (charter §"By design NO snapshot
  moves: … P63/P62 (tests only)")
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger (`<scratchpad>/findings-ledger.md`); the lead files
  by class (contract §10); trackers named at the end of this plan. **No implementer runs `gh issue create`.**
- **Override:** none
- **Filing budget** [ADDED: missing step]: verify cost = the Phase 0 raw census (78 members × 200 samples, `Analyze` only) + one whole-solution gate; the sibling-cell prober's budget is the oracle matrix (24 consumer classes × 6 oracle kinds) plus at most two axis changes (e.g. severity {error, warning}; `Gen<Module>` vs `Gen<string>` source), one ledger line per cell, one issue per class.

## Defect Class

- **Violated contract:** every construct the language spec defines has a roster row stating, for each lane, what the
  typed generator proves — and every lane verdict is *measured*: a generated-lane **Y** means every raw sample the
  named generator member draws analyses clean (or is a pinned known red) and contains the construct; an injected-lane
  **Y** means every applicable sample draws exactly one named code (or is a pinned known red); a spec file with no row,
  a row whose anchor section is gone, and a Y row whose generator never emits the construct are all red.
- **Meta-class:** *Instruments that pass vacuously* (verification-contract.md §1 table) → **standard cure:** fail
  loudly; ONE judging seam makes the wrong oracle unwritable (`ErrorInjector.Judge` becomes the sole refusal verdict for
  AST and source-text generators alike; the filter counts what it discards); a source-scan backstop; mutation-test every
  guard (§2).
- **Known member cells:** #2288 items 1, 3, 4; the oracle table above (eleven weak or vacuous refusal oracles, eleven
  threshold soundness oracles, five filtered soundness oracles); the dead constraint generator (`collections.Comparable`).
- **Sibling cells this plan must also cover:** the full matrix *construct (every spec file, anchored to its defining
  section) × lane {generated, injected} × verdict {Y, N + issue, by design}* — seeded by reading the generator, not by
  the issue's eight-lane list; and the oracle matrix *consumer class (24) × oracle kind {exact-code seam, prefix,
  any-failure, threshold, no-throw-only, filtered}*, which must drain to one kind.
- **Lane / reach:** tooling · probe; **fidelity-scope row:** n/a (no Python-parity surface; the oracle is the Sharpy
  spec and the compiler's own codes).
- **Standing harness that should have caught it:** none — the roster IS the census (Phase 1 adds
  `TypedGeneratorRosterTests`) and Phase 2 adds `PropertyOracleConformanceTests` as the backstop; both join
  `docs/design/gap-discovery-contracts.md` (Phase 5).
- **Generator:** the census axis is spec prose — no generator enumerates it; the universe is the **directory scan**
  of `docs/language_specification/*.md` (reflection-style, cannot go stale), and the rows are literal. The lanes'
  generators already exist and are the subject; **no new generator** (R-FZ, #2288 "Not in scope"). The ≥2-axis ×
  ≥3-value rule (§11) is met by the directory-scan universe + the per-row positive control sampling the generator, not
  by a hand matrix.
- **Owner rulings:** R-FZ (link above) — ordering only; item 2's landing comment fixes the judging vocabulary
  (`InjectionResult`, `KnownRedRefusal`, `Judge`), which this plan extends and never forks; **N-generated rows cite
  #2287** (owner, 2026-10-15 at plan creation, option a — Design Decision 3a).

## Adversarial Review (pre-mortem)

- **Alternative root cause:** the weak oracles may be *hiding* two different things — generator defects (the
  `Comparable` import proves at least one) and compiler false-refusals of valid generated programs (the filter hides
  them). **Discriminating probe (Phase 0):** sample every generator member RAW (no filter) 200× at a fixed seed,
  `Analyze`, and bucket by first code; a bucket whose program is wrong *by the spec* is a generator defect fixed in this
  plan; a bucket whose program is spec-valid is a compiler cell for the ledger (lane false-refusal, filed by class).
- **Alternative layer:** tests only — no ladder rung. The one layer hazard is the roster becoming a *hand model of
  the compiler*: rows anchor to spec files/headings and to parser-produced AST kinds (`MatchStatement`,
  `TypeParameterDef.Constraints`, `VariableDeclaration.IsConst` / `Assignment.IsLet` [CORRECTED: `VariableDeclaration` has `IsConst` only; a typeless `let` is `Assignment.IsLet` — `Statement.cs:154,160`]), never to `DiagnosticCodes` constants (item 2's N2
  mutation — renumbering a constant — must still go red, so expected codes stay literal strings).
- **How the fix could be inert:** (a) a positive control that samples the **filtered** generator — it would never see
  a refused construct; sample RAW. (b) a second judging core for `Gen<string>` — two seams drift; one core
  (`JudgeSource(string, expected)`), `Judge(InjectionResult)` unparses and delegates. (c) a conformance scan whose
  banned spellings do not match the real ones — its positive control is a literal string containing each spelling.
  (d) a row count derived from the roster array — the totality anchor is a literal `const int` (feedback: a totality
  count from the same enum is vacuous).
- **Before/after by direction:** no program's verdict changes (tests only). Every KnownRed pin the plan adds records
  the prior behaviour verbatim (`analyses clean; run is SPY0908 CS0311`) so the pin's drain is a direction record for
  the fixing plan (#2212, #2143).
- **Blast radius:** `Sharpy.Compiler.Tests` only; CI shard wall-clock — the roster's positive control generates and
  parses (no compile) and must finish < 30 s; the rejection census compiles via `Analyze` under `PropertySerial` and
  is budgeted (≤ 40 samples per member) with its measured time recorded. No Stdlib/Cli/LSP reach.
- **Generated artifacts touched:** none (regen: no diff expected; `/push` staleness gates unaffected). The
  `allowlist_issue_state` gate newly sees every `new KnownRedRow("#N"…)` / `new KnownRedRefusal("#N"…)` — a cited
  issue closing without the row draining reddens `/push`, which is the intended ratchet.

## Design Decisions

1. **Row unit and universe (item 1).** A row is `(spec file, anchor heading, construct)`. The **universe** is every
   `*.md` under `docs/language_specification/` except `README.md`, enumerated at test time (a new spec file with no
   row is red; a row for a deleted file is red). A row's **anchor** is a `## ` heading in the file, or the `# ` title
   for the 15 heading-less files; a missing anchor is red (stale). A file that defines no typing construct
   (`introduction.md`, `conventions.md`, `rejected_proposals.md`, `feature_roadmap.md`, `deferred_features.md`,
   `lexer_implementation.md`, `naming_conventions.md`, `comments.md`, …) carries exactly one `NoConstruct(reason)` row.
   *Why not one row per `##`:* 15 files have none and most headings ("See Also", "C# Emission") define nothing — the
   owner's "one row per section that defines a construct" is honoured as the row's anchor (every construct row names
   its defining section); "a spec file with no row is red" is the universe check. Record at `/verify-plan` if the
   owner wants the stricter per-heading universe.
2. **Verdict vocabulary.** `Generated ∈ { Y(member, AstPredicate), N(issue), ByDesign(reason) }`,
   `Injected ∈ { Y(member, code, severity), N(issue), ByDesign("no typing surface") }`. Both lanes may be
   `KnownRed(issue, observed)` — the lane is wired but the compiler does not yet hold (a pin, gated).
3. **Issue-cite rule for N rows.** (a) *N-generated because no generator emits the construct:* cite **#2287**
   (P62's grammar-driven sentence generator "replaces hand-maintained syntactic coverage" — the planned cure; a new
   generator is out of P63's scope by R-FZ). P62 reads these rows as its worklist. **Owner ruled (a) on 2026-10-15
   at plan creation** ("sure a"); rejected (b) one new "typed-generator reach" class tracker (adds an open issue for
   work P62 already owns). Filing only, two-way. (b) *N-injected because the construct is generated but not refused:* the injection is
   written as a `KnownRed` pin citing the class tracker the ledger resolves to (#2212 constraints, #2143 override /
   abstract, …), so it drains on fix. (c) *N-injected because no injection exists yet though the construct is
   generated and a refusal exists:* drained **in this plan** (Phase 4) — no cite survives it.
4. **Positive control for Y-generated (RAW, not filtered).** `Sample` the named member 40× at a fixed seed with the
   unfiltered generator; for `Gen<string>` members, parse with the real `Parser`; the row's `AstPredicate` must hit ≥ 1
   node in the corpus, and no sample may throw in generator, unparser or parser. Rows for members whose output is a
   tuple of sources (`GenImports`) predicate over the entry source.
5. **The generated lane counts what it discards (Phase 3).** `SemanticFilter.WellTypedProgram`/`CompilableProgram`
   gain a `RejectionSink` (per-call record of `(source, codes | exception)`); every soundness property asserts the
   sink is empty **or** every rejected code is a `KnownRedRefusal` the property declares. Y-generated is sustained by
   this property; the roster's control proves presence, the property proves acceptance.
6. **One judging seam for every refusal lane (Phase 2).** `ErrorInjector.Judge` keeps its contract; a new
   `JudgeSource(string source, string? expectedCode, DiagnosticSeverity severity, KnownRedRefusal? pin, string label)`
   is the core and `Judge(InjectionResult)` delegates to it after unparsing. The severity arm admits the warning code
   SPY0463 (match-statement exhaustiveness) — "exactly one diagnostic of the expected severity with the expected code,
   and no error" — without widening the error arm. Every refusal property calls one of the two; thresholds go;
   `Assert.True(tested > 0)` stays (an inapplicable injection is a red, item 2's rule).
7. **Source-scan backstop.** `PropertyOracleConformanceTests` scans `Properties/Semantic/*.cs` text for the banned
   spellings — `StartsWith("SPY`, `> total /`, `> tested /`, `!result.Success ||`, `+ outsideDiagnosed > 0`-style
   two-bucket sums, and a `catch` whose body increments a pass counter — with a literal allowlist `(file, spelling)`
   that this plan drains to **0** and a positive control (an in-test literal containing each spelling is detected).
   It cannot prove an oracle strong; it stops the five weak shapes from being re-typed.
8. **The constraint lane is repaired, not deleted.** `GenGenerics.GenericConstraintProgram` emits a *method-based*
   user interface constraint (`interface Ordered[T]: def less(self, other: T) -> bool`, the measured valid shape) —
   the spec's own `__lt__` example is SPY0413 (allowlisted #1952) and `collections.Comparable` does not exist. Two
   injections: inferred-call violation → **SPY0237**; explicit-instantiation violation → `KnownRedRefusal("#2212",
   "analyses clean; run is SPY0908 CS0311")`.
9. **New injections only where the construct is generated** (item 3, R-FZ), each with the measured code of the table
   above: Optional store (SPY0220) and Optional operand (SPY0222) over `TypeEnv.WithOptionals`; failed-branch use
   (SPY0220) over `GenTyped.NarrowedBlock` (wired into `TypedProgram`'s statements when the env has optionals — today
   `NarrowedBlock` has no caller, which Phase 0 confirms with `grep -rc`); `isinstance` else-branch member (SPY0203)
   over `GenClasses.ClassHierarchy` using a derived-only member; const reassignment (SPY0225) once `GenTyped` emits a
   `const` binding (`VariableDeclaration.IsConst`, unparsed as `const`); protocol misuse (SPY0320) over
   `GenInterfaces.ModuleWithProtocolDunder` by renaming the dunder; interface member missing (SPY0325) replacing the
   prefix; match statement (SPY0463, warning) and match **expression** (SPY0416) over `GenMatchPatterns`; variance
   (SPY0417/0418/0419) routed through the seam. **Same-scope re-`let` is `ByDesign`** (spec `variable_scoping.md`
   §Variable Shadowing [CORRECTED: the `##` heading that holds the sentence; §Sibling-Block Redeclaration is a `###`]; control: it runs, prints `2` @ f8c37f986). **CLR interop is N-generated → #2287** (no generator emits a CLR
   import; the SPY0220 probe is recorded on the row for the day P62 generates one).
10. **Totality anchors are literals.** `ExpectedRows`, `ExpectedYGenerated`, `ExpectedYInjected`, `ExpectedKnownRed`
    are `const int`s counted at landing and bumped in the same commit as the row that moves them; the test prints
    `rows total/Y-gen/Y-inj/N/ByDesign/KnownRed @ sha (measured)` — the coverage record item 4 asks for.
11. **Gated cites.** N and KnownRed rows are constructed as `new KnownRedRow("#NNNN", …)` so `KNOWNRED_CELL_RE` in
    `build_tools/allowlist_issue_state.py` sees them; a row citing a closed issue reddens `/push` until drained.
    [CORRECTED: the regex is `new\s+\w*KnownRed\w*\s*\(\s*"#?(\d+)"` and the scanner walks `src/*.Tests/**/*.cs`, so the
    `N(issue)` record of Design Decision 2 must ALSO carry `KnownRed` in its type name (e.g. `KnownRedN`) or be the same
    `KnownRedRow` record with an `Observed` of `"no generator emits the construct"` — a record named `N`/`NotGenerated`
    is invisible to the gate. Mutation R2 (Phase 3) covers it: a misspelled issue in an N row must redden
    `build_tools/check_allowlist_issue_state.sh`.]

## Implementation

### Phase 0: Measure the instrument (no production or test change lands)

**Goal:** replace "by reading" with "measured": what every generator member produces raw, what every existing refusal
lane draws, and which `Gen` members have no caller.

**Acceptance (class-level, measurable):**
- A table `member × {samples, parse-ok, analyse-clean, first-code buckets}` at 200 samples, fixed seed, for all 78
  members, written to `<scratchpad>/p63/raw-census.md` with the sha.
- For each of the 11 weak refusal oracles: the code distribution its "invalid" generator draws (as item 2 did at 600).
- `grep -rc` caller counts for every public `Gen` member in `Properties/` (dead members become `N` or `ByDesign` rows,
  never `Y`).
- Every raw-rejected bucket classified: *generator defect* (spec says the program is wrong → Phase 3/4 fixes the
  generator) or *compiler cell* (spec says valid → findings-ledger row, lane false-refusal, candidate class).

#### Tasks

1. **Raw census script** — a throwaway xunit `[Fact(Skip=…)]`-free console harness is not available; write the census
   as a temporary test class under `<scratchpad>` compiled into a **worktree** copy of `Sharpy.Compiler.Tests` (never
   the shared tree), run with `.claude/scripts/dotnet-serialized test --filter "FullyQualifiedName~P63RawCensus"`
   (`dangerouslyDisableSandbox: true`), output via `ITestOutputHelper`. Record `@ sha`. No commit.
2. **Drawn-code table for the weak lanes** — same harness; 600 samples each for `GenVariance.*`,
   `GenMatchPatterns.MatchNonExhaustive`, `GenInterfaces.ModuleWithInterface(_, false)`,
   `GenIterators.ModuleWithIterProtocol(false)`, `GenExceptions.ModuleWithBareRaise(false)`,
   `GenContextManagers.MissingEnterOrExitProgram`, `GenOperators.InvalidDunderProgram`,
   `GenAsync.ModuleWithAwaitOutsideAsync`, `GenGenerics.GenericConstraintProgram/WrongTypeArgCountProgram/
   TypeMismatchOnGenericFieldProgram`. A lane drawing two codes is a bad generator (item 2's rule) and is repaired in
   Phase 2 before it is judged.
3. **Ledger seed** — open `<scratchpad>/findings-ledger.md` with the cells planning already found outside the
   contract: (i) `TypeInferenceService.cs:549` and `OperatorValidator.cs:209` key constraint handling on
   `Name.Contains("Comparable")` — a rung-4 name-keyed alias (meta-class #1933), candidate tracker **#2212**;
   (ii) four spec files unlinked from `README.md` (docs, tooling); (iii) any compiler cell Task 1 classifies.

### Phase 1: The roster census test (item 1)

**Goal:** `TypedGeneratorRosterTests` — the literal roster, the directory-scan universe, anchor checks, the RAW
positive control, literal totality anchors, and the coverage line.

**Acceptance (class-level, measurable):**
- Matrix that goes green: every file in the universe has ≥ 1 row; every row's anchor exists; every Y-generated row's
  predicate hits in 40 raw samples; every row's member/code strings resolve (member by reflection on the generator
  types — a renamed member is red).
- Guard mutations (each recorded broken → red / restored → green in the commit body): **M1** delete a file's rows →
  red (universe); **M2** point a Y row at a member that does not emit the construct (`GenTyped.TypedProgram` for the
  `match` row) → red (positive control); **M3** misspell an anchor heading → red; **M4** in a worktree add
  `zz_probe.md` with no row → red; **M5** bump `ExpectedRows` by one → red (anchor); **M6** set a `ByDesign` reason to
  `""` → red; **M7** [ADDED: missing step — the `NoConstruct` exemption must not be its own subject] a `NoConstruct` row may name only a
  file whose fenced Sharpy blocks are all `error`/`fragment`-marked or absent (read with `build_tools/spec_blocks.py`'s block scan or a
  fence regex); mark `match_statement.md` `NoConstruct` → red; positive control: `comments.md` stays green.
- Allowlist entries that drain: none (the roster's N/KnownRed rows ARE the ratchet; counts in the coverage line).
- Execution evidence: `rows total/Y-gen/Y-inj/N/ByDesign/KnownRed @ sha (measured)` printed and quoted in the commit.

#### Tasks

1. **Row model and universe** — `src/Sharpy.Compiler.Tests/Properties/Semantic/TypedGeneratorRosterTests.cs`
   - `record RosterRow(string File, string Anchor, string Construct, Lane Generated, Lane Injected)`; `Lane` is an
     abstract record with `Y(string Member, Func<Node,bool> Predicate)` / `YInjected(string Member, string Code,
     DiagnosticSeverity Severity)` / `KnownRedRow(string Issue, string Observed)` / `ByDesign(string Reason)`;
     `NoConstruct(string Reason)` rows for the non-construct files.
   - Universe: `Directory.EnumerateFiles(Path.Combine(FixtureRoots.RepositoryRoot, "docs", "language_specification"), "*.md")`
     minus `README.md`. Fail loud if the count is < 100 (a degraded path would pass vacuously).
   - Anchor check: `## <Anchor>` or `# <Anchor>` line present (exact text match after trimming backticks is NOT done —
     the row carries the heading verbatim).
   - Commit: `test(property): TypedGeneratorRosterTests — the spec-file universe, literal rows with anchors, and the N/ByDesign vocabulary; every file has a row or is red (refs #2288)`
2. **Seed the rows by reading the generator @ sha** — one row per construct the 78 members emit (classes, inheritance,
   constructors, functions, overloads, generics, constraints, variance, interfaces, protocol dunders, iterators,
   generators, async, exceptions, decorators, dataclass, context managers, f-strings, imports, match patterns ×7,
   operators, Optional, narrowing, `let`, lists, `range` loops, if/else, conditional expressions, truthiness) and one
   row per remaining spec file (N → #2287 per Design Decision 3a, or `NoConstruct`). Expect roughly 125 files →
   ~150 rows; the exact count is the literal anchor.
   - Commit: `test(property): roster rows seeded by reading the typed generator @ <sha> — Y rows name their member and AST predicate, N rows cite #2287 (no generator emits the construct), non-construct files carry one NoConstruct row (refs #2288)`
3. **RAW positive control** — `Sample(iter: 40, seed: fixed)` over the member resolved by reflection
   (`typeof(GenTyped).Assembly` types in the `Generators.Typed` namespace; `Gen<Module>` → `Unparser.Unparse` → parse
   back; `Gen<string>` → parse; tuple → entry source); walk the AST with a reflective child enumerator (lift one of the existing private `Descendants(Node)` helpers —
   `Semantic/NestedGenericReferenceFactTests.cs:42`, `Semantic/UnionCasePatternVectorTests.cs:121`, `CodeGen/RoslynEmitterOperatorLoweringTests.cs:391` — into
   `Infrastructure/`; or subclass `Parser/Ast/AstVisitor` [CORRECTED: `SymbolPositionPropertyTests` walks the symbol table, not the AST]) and evaluate the predicate;
   resolve `Gen<T>` for T : `Node` (`Unparser.Unparse(Node)` → parse), T = `string`, and `ValueTuple`s whose items are `Node` or `string`
   (`GenClasses.ClassHierarchy` is `Gen<(ClassDef, ClassDef)>`; `GenTyped` has 7 `Gen<Expression>`/`Gen<Statement>` members beside its 2 `Gen<Module>`) [ADDED: missing step — the 78 members are not all `Gen<Module>`/`Gen<string>`]; a throw anywhere is red
   with the sample's source.
   - Commit: `test(property): every Y-generated roster row is proven by a raw 40-sample corpus containing its construct — a Y whose generator never emits it is red (refs #2288)`
4. **Totality anchors + coverage line** — `const int ExpectedRows/ExpectedYGenerated/ExpectedYInjected/ExpectedKnownRed/ExpectedN`;
   print `rows … @ sha (measured)`; record M1–M6 in the commit body.
   - Commit: `test(property): roster totality anchored to literals and the coverage line printed per run; mutations M1–M6 recorded (refs #2288)`

### Phase 2: One judging seam; every refusal lane exact (items 2's class, 3's precondition)

**Goal:** every refusal property draws exactly one code through `ErrorInjector.JudgeSource`/`Judge`; the weak shapes
become unwritable.

**Acceptance (class-level, measurable):**
- Matrix that goes green: the 11 weak refusal oracles in the Current State table re-judged at every applicable sample
  with the code Phase 0 measured; `PropertyOracleConformanceTests` scan with allowlist **0 rows**.
- Guard mutations: for each migrated lane **N1** (change the expected code → red) and **N5** (`Analyze` throws →
  red); for the scan **S1** (add `StartsWith("SPY0")` to a property → red) and its positive control (literal
  spellings detected = count of spellings).
- Allowlist entries that drain: the scan's literal allowlist, seeded with the 11 `(file, spelling)` rows at Task 2 and
  drained to 0 by Task 3 within this phase (never committed non-empty at the phase's end).
- Execution evidence: per lane `k/k drew <code>` lines @ sha.

#### Tasks

1. **`JudgeSource` core + severity** — `Properties/Generators/Typed/ErrorInjector.cs`
   - `public static string? JudgeSource(string source, string? expectedCode, CompilerDiagnosticSeverity severity, KnownRedRefusal? pin, string label)`;
     `Judge(InjectionResult, label)` unparses and delegates with `Error`. Warning arm: exactly one diagnostic with the
     code at `Warning` severity and **zero errors**. `InjectionResult` gains an optional `Severity` (default Error).
   - Mutation: the SPY0463 lane under `Error` severity → red ("drew nothing"); restored → green.
   - Commit: `test(property): ErrorInjector.JudgeSource is the one refusal verdict for source-text generators too, with a warning arm for SPY0463 — Judge(InjectionResult) delegates to it (refs #2288)`
2. **`PropertyOracleConformanceTests` with the seeded allowlist** — `Properties/Semantic/PropertyOracleConformanceTests.cs`
   - Regex scan over `Properties/Semantic/*.cs` text (read from `FixtureRoots.RepositoryRoot`); literal allowlist of
     every current hit each citing `#2288` (38 lines in 15 files @ f8c37f986 by the verifier's grep — `ImportResolutionPropertyTests` 6, `GenericTypePropertyTests` 7, `FunctionSemanticsPropertyTests` 4, `IteratorPropertyTests` 4, `CombinedFeaturePropertyTests` 4, `VariancePropertyTests` 3, `InterfacePropertyTests` 2, `ExceptionPropertyTests` 2, and one each in `Async`, `ContextManager`, `Operator`, `PatternMatch`, `TypedAnalyze`, `ClassInheritance` (`passed > 0`), plus the `catch`-increments the regex must also model) [CORRECTED: "11 rows" — the plan's table missed four classes]; positive-control fact feeds a string with every banned spelling and
     asserts each is found; S1 recorded.
   - Commit: `test(property): PropertyOracleConformanceTests bans prefix, any-failure, threshold and two-bucket refusal oracles in Properties/Semantic — eleven rows allowlisted against #2288 to drain this plan (refs #2288)`
3. **Migrate the eleven lanes** (one commit per file, in this order so a red attributes to one lane):
   `VariancePropertyTests` (0417/0418/0419) → `AsyncPropertyTests` (0273) → `PatternMatchPropertyTests` (0463 warning;
   add the match-expression variant → 0416) → `InterfacePropertyTests` (0325) → `ContextManagerPropertyTests`,
   `OperatorOverloadPropertyTests`, `IteratorPropertyTests.IterProtocol`, `ExceptionPropertyTests.BareRaise`
   (codes from Phase 0 Task 2) → `GenericTypePropertyTests` (constraint lane: see Phase 4 Task 1; `WrongTypeArgCount`
   and `TypeMismatchOnGenericField` gain their measured codes — a `NeverCrashes` that asserts no refusal is an
   accepted-program lane and moves to Phase 3's census) → `ImportResolutionPropertyTests` (the `StartsWith("SPY0")` lane at line 127 and the
   warning threshold at line 189 — the warning arm of `JudgeSource`) [CORRECTED: a twelfth refusal lane the table missed].
   **`GenMatchPatterns.MatchNonExhaustive` is repaired in the `PatternMatchPropertyTests` commit** [CORRECTED: measured @ f8c37f986 —
   every variant draws SPY0266 beside SPY0463 because the match sits in a `-> str` function with no trailing return; the
   statement form becomes a `-> None` body (prints per arm) so the warning arm's "zero errors" holds, and the expression
   variant (Phase 4 Task 6) keeps `-> str` with `return match …`, which draws SPY0416 alone]. Each: thresholds removed, `tested > 0`, drop its allowlist
   row, N1/N5 recorded. A lane whose generator draws two codes is split into one-fact programs first (item 2's rule)
   — the generator change is in the same commit.
   - Commit (pattern): `test(property): <Class> judges every sample through JudgeSource — <code> exactly, thresholds gone, allowlist row drained (refs #2288)`
4. **Soundness thresholds** — the eleven `passed > tested / 2` lanes: every raw sample must analyse clean or be a
   declared `KnownRedRefusal`; this is Phase 3's sink, so these flip in Phase 3 Task 2 (listed here for the scan's
   `> tested /` spelling, allowlisted until then).

### Phase 3: The generated lane counts its rejections

**Goal:** `SemanticFilter` records what it discards; every soundness property asserts zero unexplained rejections.

**Acceptance:**
- Matrix: `TypeSoundnessPropertyTests` ×4, `ErrorCompletenessPropertyTests`' base generator, and the eleven
  threshold soundness lanes assert `sink.Rejections` is empty or fully pinned; the roster's Y-generated rows all
  correspond to a property that does so.
- Guard mutation **R1**: make `GenTyped.LeafOfType("int")` emit a string literal in a worktree → every soundness
  property red (rejections SPY0220); restored → green. **R2**: a pin whose issue is spelled wrong → red at
  `allowlist_issue_state`.
- Allowlist entries that drain: the `> tested /` rows of the conformance scan (to 0).
- Execution evidence: per property `rejections 0/N @ sha (measured)` or the pinned codes.

#### Tasks

1. **`RejectionSink`** — `Properties/Generators/Typed/SemanticFilter.cs`: `WellTypedProgram(Gen<Module>, RejectionSink)` /
   `CompilableProgram(…, RejectionSink)`; the sink records `(source, codes)` or `(source, exception)`; the old
   overloads are deleted (not kept — a kept overload is the inert path §2). All 24 call sites in 10 classes migrate in this
   commit (list in the Current State table) [CORRECTED: the plan named 5]; a caller that passes a sink and never asserts on it
   is the inert path too — Task 2 asserts on every one.
   - Commit: `test(property): SemanticFilter records every sample it discards in a RejectionSink — the filter can no longer hide a refused generated program (refs #2288)`
2. **Assert on the sink** — `TypeSoundnessPropertyTests`, `ErrorCompletenessPropertyTests`, every other `SemanticFilter`
   caller (`CsClean`, `DiagnosticSpan`, `SymbolPosition`, `Determinism`, `TypedAnalyze`, `SemanticInfoConsistency`, `ValidatorOrdering`),
   and every threshold lane the scan lists (`GenericType` ×5, `FunctionSemantics` ×4, `Iterator` ×3, `Exception` ×1, `ImportResolution` ×5,
   `CombinedFeature` ×4, `TypedAnalyze` ×1, `ClassInheritance.SuperCall`) [CORRECTED: "the eleven" undercounted; the scan's allowlist cannot reach 0 otherwise]: `Assert.True(sink.Unexplained.Count == 0, …)` with declared pins; Phase 0's classification decides which
   rejections are generator defects (fixed here in the generator) and which are compiler cells (pinned, ledger).
   - Commit: `test(property): every soundness lane asserts zero unexplained rejections — generator defects fixed, compiler cells pinned KnownRed with their issue; the > tested / 2 thresholds are gone (refs #2288)`

### Phase 4: New injections where the construct is generated (item 3)

**Goal:** the eight lanes #2288 names, each an `ErrorInjector` member that breaks ONE fact, judged exactly, mutation-tested
against the compiler.

**Acceptance:**
- Matrix: `ErrorCompletenessPropertyTests` gains one `[Fact]` per injection; each `k/k drew <code>` or
  `KnownRed <issue> k/k still unrefused`; the roster's Injected column flips N → Y (or KnownRed) for each.
- Guard mutations (**in a worktree build**, restore by `cp`): for each refusing injection, comment out the compiler's
  report site → the property red ("drew nothing"); restore → green. Report sites @ f8c37f986 [ADDED: missing step — the tasks
  did not name them]: SPY0220 Optional store → `TypeChecker.StoreConversion.cs` (the "narrow it" arm); SPY0222 → `TypeChecker.Expressions.Operators.cs`;
  SPY0203 → `TypeChecker.Expressions.Access.cs`; SPY0237 → `GenericTypeInferenceService.cs:746`; SPY0225 → `TypeChecker.Statements.cs:1012`;
  SPY0320 → `Validation/ProtocolValidator.cs` (and `TypeChecker.Expressions.Literals.cs` for `len()`); SPY0325 → `Validation/InterfaceImplementationValidator.cs`;
  SPY0416 / SPY0463 → `Validation/ExhaustivenessValidator.cs`. Each mutation names the one arm it disabled.
  For each KnownRed pin, item 2's N3/N4 shape (drop the pin → red; an injection that is refused today under the pin →
  red).
- Allowlist entries that drain: none added; roster N rows for these constructs are deleted in the same commit.
- Execution evidence: per injection 600-sample drawn-code line @ sha, as item 2 recorded.

#### Tasks

1. **Constraint lane repaired + two injections** — `GenGenerics.GenericConstraintProgram` emits the method-based
   `Ordered[T]` interface (Design Decision 8) and a satisfying class; `ErrorInjector.InjectConstraintViolationAtInferredCall`
   → SPY0237; `InjectConstraintViolationAtExplicitInstantiation` → `KnownRedRefusal("#2212", "analyses clean; run is SPY0908 CS0311 — a violated constraint at an explicit instantiation is refused by Roslyn, not Sharpy")`.
   `GenericTypePropertyTests.GenericTypeConstraint_IsEnforced` becomes the positive control (valid program runs, prints
   the expected value — contract §4, recorded ≠ applied).
   - Commit: `test(property): the constraint lane is live — GenericConstraintProgram emits a user interface constraint (collections.Comparable never existed: every sample was SPY0301), inferred-call violations draw SPY0237, explicit-instantiation violations pin #2212 (refs #2288, refs #2212)`
2. **Optional / `maybe`** — `InjectOptionalStoreWithoutNarrowing` (SPY0220) and `InjectOptionalOperandWithoutNarrowing`
   (SPY0222) over `TypeEnv.WithOptionals` programs.
   - Commit: `test(property): Optional injections — an Optional stored to its payload type draws SPY0220, an Optional operand SPY0222, every sample (refs #2288)`
3. **Narrowing** — wire `GenTyped.NarrowedBlock` into `TypedProgram(withStatements: true)` when the env has optionals
   (roster Y-generated for `type_narrowing.md`); `InjectUseInFailedNarrowingBranch` → SPY0220;
   `InjectIsinstanceElseBranchMember` over `GenClasses.ClassHierarchy` → SPY0203 (the derived class gets a member the
   base lacks; the injection adds `if isinstance(b, Derived): … else: b.<member>`).
   - Commit: `test(property): narrowing injections — the failed branch of an is-None test draws SPY0220, the else branch of isinstance SPY0203; NarrowedBlock gains its first caller (refs #2288)`
4. **`let`/`const`** — `GenTyped` emits a `const` binding in `main` when fuel allows (`VariableDeclaration.IsConst`);
   `InjectConstReassignment` → SPY0225. Same-scope re-`let`: a `ByDesign` roster row citing `variable_scoping.md`
   §Sibling-Block Redeclaration with the running control in the row comment.
   - Commit: `test(property): const reassignment draws SPY0225 in every sample; same-scope re-let is by design (variable_scoping.md) and recorded as such (refs #2288)`
5. **Protocol synthesis + interface member** — `InjectProtocolDunderRemoved` over `ModuleWithProtocolDunder("__len__")`
   → SPY0320; `InjectInterfaceMemberRemoved` over `ModuleWithInterface(n, complete: true)` → SPY0325 (replaces the
   `complete: false` generator path, which stays as the roster's positive control that `Judge` sees the same code).
   - Commit: `test(property): protocol and interface injections — len() on a class whose __len__ was removed draws SPY0320, a dropped interface member SPY0325 (refs #2288)`
6. **Match exhaustiveness (expression form)** — `GenMatchPatterns.MatchExpressionNonExhaustive` → SPY0416 beside the
   statement form's SPY0463 (Phase 2 Task 3); the roster's `match_statement.md` §Exhaustiveness Checking row flips Y.
   - Commit: `test(property): non-exhaustive match expressions draw SPY0416, statements the SPY0463 warning — both judged exactly through JudgeSource (refs #2288)`
7. **Variance / override / abstract / CLR** — variance is Phase 2 Task 3 (already exact); override and abstract keep
   their #2143 pins; CLR interop is an N-generated row → #2287 with the SPY0220 probe recorded. No task; the roster
   rows are the deliverable (Phase 1 Task 2).

### Phase 5: Coverage record, standing-harness registration, close-out

**Goal:** the instrument is documented where plans cite harnesses; #2288 closes on measured counts.

**Acceptance:**
- `docs/design/gap-discovery-contracts.md` gains one row: class "typed-generator reach and refusal-oracle strength
  (#2288)", contract (this plan's), harnesses `TypedGeneratorRosterTests` + `PropertyOracleConformanceTests` +
  `ErrorInjector.JudgeSource`/`RejectionSink`, umbrella none (N rows cite #2287/#2212/#2143 individually).
- Coverage line `rows total/Y-gen/Y-inj/N/ByDesign/KnownRed @ sha (measured)` posted on #2288 with the per-lane
  drawn-code lines and the mutation records (M1–M6, N1/N5 per lane, S1, R1/R2, Phase 4 production mutations).
- Whole-solution gate `passed/failed/skipped @ sha (measured)`; CI shard timing delta for the compiler shard that holds
  `PropertySerial` recorded.

#### Tasks

1. **Contracts doc row** — `docs/design/gap-discovery-contracts.md`.
   - Commit: `docs(design): gap-discovery roster gains the typed-generator census and refusal-oracle seam (#2288) (refs #2288)`
2. **Close-out** — post the record on #2288; `/close-issues 2288` after the gate; the ledger rows filed by the lead per
   §10 (comments on #2212 and the docs note; a new tracker only if a class has none).

## Testing Strategy

- **Generator first (§11):** the census universe is a directory scan (reflection-style, no hand list of files); the
  per-row positive control samples the RAW generator; the lanes' judging is one seam. The hand axis is the **row list
  itself** (which constructs exist and which member emits each) — spec prose has no enumerator; every row is checked
  against the file system, the generator's reflection surface and a sampled corpus, so a hand row cannot be stale
  silently.
- **New fixtures:** none (`.spy` fixtures are not this instrument). New test classes: `TypedGeneratorRosterTests`,
  `PropertyOracleConformanceTests`; new members on `ErrorInjector`, `SemanticFilter`, `GenTyped`, `GenGenerics`,
  `GenMatchPatterns`. For every negative (refusal) lane the **positive control** is the same generator's valid program
  analysing clean (and, for the constraint lane, running and printing the value).
- **Edge cells beyond the issue's list:** the warning-severity lane (SPY0463); `Gen<string>` members parsed back
  (the parser is the AST source for the predicate — a generator printing unparsable text is red); heading-less spec
  files anchored to their title; the explicit-instantiation constraint cell (#2212) beside the inferred one; the
  same-scope re-`let` control (by design, runs).
- **Outputs that discriminate:** every judged lane prints `k/k drew <code>`; the constraint control prints `2`; the
  roster prints the coverage line; a mutation record lists the counter before/after.
- **Spec deliverable:** none changed — the spec is the oracle. The `generics.md` `__lt__` example being SPY0413
  (allowlisted #1952) is recorded in the roster row's comment, not touched.
- **Blast radius and gate:** `Sharpy.Compiler.Tests` only; edit loop `--filter "FullyQualifiedName~Properties.Semantic"`
  plus `~PropertyOracleConformance|~TypedGeneratorRoster`; the gate is the whole solution
  (`.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`,
  private log path, `dangerouslyDisableSandbox: true`); regen: none ("regen: no diff @ sha" in the first commit body).
  Pre-push: `build_tools/check_allowlist_issue_state.sh` (new KnownRed cites), `check_spec_blocks.sh` unaffected.
- **Agent prompts** carry the §9 shared-tree block verbatim; mutations run in `sharpy.worktrees/` builds, restored by
  `cp` (never `copy2`; `touch` after restore).

## Issues to Close

- #2288 — property tests: the typed program generator has no feature roster … — closed by Phase 5 Task 2;
  **close criterion:** (1) `TypedGeneratorRosterTests` green with every spec file rowed and the coverage line
  `rows total/Y-gen/Y-inj/N/ByDesign/KnownRed @ sha (measured)` posted; (2) `PropertyOracleConformanceTests` allowlist
  at 0 rows; (3) every refusal property in `Properties/Semantic` judged through `JudgeSource`/`Judge` with its
  600-sample drawn-code line; (4) `RejectionSink` asserted empty-or-pinned on every soundness lane; (5) the Phase 4
  injections landed with their production mutations recorded (broken → red, restored → green); (6) whole-solution
  gate `passed/failed/skipped @ sha (measured)`. Items 1, 3, 4 of the issue map to (1)+(2), (5), (1)'s line.

## Class trackers this plan may post cells to

- #2288 — typed-generator roster (this group's tracker) — roster rows, oracle cells, coverage lines.
- #2212 — type-parameter constraints (uncalled constraint resolution; violated constraint at a call behind SPY0908) —
  the explicit-instantiation KnownRed pin; the `Contains("Comparable")` name-keyed arm (ledger row (i)).
- #2143 — concrete class leaving an abstract/override contract to Roslyn (CS0508/CS0534) — the existing pins.
- #2287 — grammar-driven generator (P62) — every N-generated row whose construct no generator emits (Design Decision 3a,
  owner-ruled (a) 2026-10-15).
- #1952 — spec-block refusals allowlisted (the `generics.md` `__lt__` constraint example) — read-only reference.
- #2240 — match guard/member scoping — if a match generator cell lands there.
- A compiler cell Phase 0 finds whose class has no open tracker → the lead files ONE class issue with a cell table,
  labelled `lane:*` + `reach:*`; no implementer runs `gh issue create`.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-10-15 @ f8c37f986
**Plan file:** `.claude/plans/plan-f4fb34.md`

Mechanical check: `python3 build_tools/check_plan_refs.py --issues` → paths 4 ok / 1 missing (the proposed `TypedGeneratorRosterTests.cs`),
`file:line` 3 ok + 2 "ambiguous" (worktree copies; both `Contains("Comparable")` arms confirmed at `TypeInferenceService.cs:549`
and `OperatorValidator.cs:209`), 15 SPY codes ok, 29 PascalCase names not found = all proposed members, 7 issues (#1933 CLOSED —
cited only as the meta-class name, not as a tracker). Probes: 14 programs through the built `sharpyc` @ f8c37f986 (binary 15:32 >
commit 15:31), `run` for refusals and `emit diagnostics` for the warning cell.

### Corrections Made
- **Match-statement lane draws two codes** (Current State probe row; Phase 2 Task 3): `GenMatchPatterns.MatchNonExhaustive`'s three
  variants all sit in a `-> str` function with no trailing return → SPY0266 (error) beside SPY0463 (warning) on every sample. Under the
  planned warning arm ("one warning, zero errors") the migrated lane is red by construction. Added the generator repair to the same
  commit (statement form → `-> None` body; expression form keeps `return match …`, which drew SPY0416 alone).
- **Oracle matrix undercounted** (Current State table; Phase 2 Task 2/3; Phase 3 Task 1/2): 38 weak-spelling lines in 15 files under
  `Properties/Semantic` (not "eleven"), four classes missing from the table — `ImportResolutionPropertyTests` (6, incl. a
  `StartsWith("SPY0")` refusal lane and a warning threshold), `CombinedFeaturePropertyTests` (4), `TypedAnalyzePropertyTests` (1),
  `ClassInheritancePropertyTests.SuperCall` (`passed > 0`); `GenericType` ×5 not ×4, `Iterator` ×3 not ×2. The allowlist seed and the
  Phase 3 flip list now cover every hit, or the scan's "0 rows" acceptance is unreachable.
- **`SemanticFilter` has 24 call sites in 10 classes**, not 5 (`CsClean`, `DiagnosticSpan`, `SymbolPosition` ×5, `Determinism`,
  `TypedAnalyze`, `SemanticInfoConsistency` ×7, `ValidatorOrdering`): deleting the old overloads touches all of them; Phase 3 Task 2 now
  asserts every sink (an unasserted sink is the inert path).
- **`VariableDeclaration.IsLet` does not exist** — `IsConst` is on `VariableDeclaration`; `IsLet` is on `Assignment` (Adversarial Review).
- **Spec anchor for same-scope re-`let`**: the sentence (`variable_scoping.md:334`) is under `## Variable Shadowing`;
  `### Sibling-Block Redeclaration` is a third-level heading under §Assignment Statement and is not a roster anchor (DD9, Phase 4 Task 4
  reference, probe row). Probe: `def main(): let x = 1; let x = 2; print(x)` → prints `2`; module-level `let` is SPY0340.
- **`N(issue)` rows must be visible to `KNOWNRED_CELL_RE`** (`new\s+\w*KnownRed\w*\s*\(\s*"#?(\d+)"` over `src/*.Tests/**/*.cs`): DD11
  now requires the N record's type name to contain `KnownRed` (or reuse `KnownRedRow`), else #2287 closing never reddens `/push`.
- **AST walker**: `SymbolPositionPropertyTests` walks the symbol table, not the AST; named the three existing private `Descendants(Node)`
  helpers and `AstVisitor` instead; added the `Gen<Expression>`/`Gen<Statement>`/`Gen<(ClassDef, ClassDef)>` resolution the RAW control
  needs (7 of `GenTyped`'s 9 members are fragments; `GenClasses.ClassHierarchy` is an AST tuple).
- Paths: `FixtureRoots` is `src/Sharpy.TestInfrastructure/Integration/FixtureRoots.cs`; `StdlibModuleGlobalRosterTests` is in
  `Sharpy.Stdlib.Tests/Conformance/`; `DifferentialExecutionTests` lacks `PropertySerial` (23 of 24 consumers share it).

### Adequacy
- **Contract:** named — every spec construct has a roster row whose lane verdicts are measured (raw corpus for Y-generated, one exact
  code for Y-injected); a missing row, a stale anchor, and a Y whose generator never emits the construct are red.
- **Matrix:** larger than repro lists — construct (126 spec files, directory-scanned) × lane × verdict, and consumer class (24) × oracle
  kind (6). The verifier found four consumer classes and 13 threshold lanes the table omitted (corrected above); the oracle axis is
  now complete by grep @ f8c37f986. Sibling cells the verifier found that are *outside* the contract: none new (the `Contains("Comparable")`
  rung-4 arm is already ledger row (i) → #2212).
- **Cure:** seam + scan + sink — `JudgeSource` as the one refusal verdict, `PropertyOracleConformanceTests` making the weak shapes
  unwritable, `RejectionSink` making the filter count — meta-class *Instruments that pass vacuously* (§1 table).
- **Harness:** none existed; Phase 1 adds `TypedGeneratorRosterTests`, Phase 2 adds `PropertyOracleConformanceTests`, Phase 5 registers
  both in `gap-discovery-contracts.md`. Allowlist delta: the scan's seeded rows drain to 0 inside the plan; KnownRed pins cite #2212/#2143
  and drain on fix.
- **Layer:** tests only — no ladder rung; the roster anchors to spec headings and AST kinds, never to `DiagnosticCodes` constants (the
  N2 renumbering mutation stays live).

### Scope & convergence
- **Group:** one (P63, charter §3.6b row 6b) · **Alone:** no — required? no (tests only, no printed output or layout) · **Gates:** 1
- **Generator:** existing — the universe is a directory scan of `docs/language_specification/*.md` (126 files, 15 heading-less, 4
  unlinked from README — all measured) and the per-row control samples the RAW generator by reflection; the hand axis is the row list
  itself, stated in Testing Strategy.
- **Trackers named:** #2288 (this group), #2212, #2143, #2287 (N-generated, owner ruled (a) at plan creation), #1952, #2240 · **Fidelity
  rows:** n/a (no Python-parity surface).
- **Filing budget:** was missing; added to `## Scope` (oracle matrix + ≤ 2 axis changes).

### Falsifiability
- `TypedGeneratorRosterTests`: mutations M1–M6 named with expected reds; exemption (`NoConstruct`/`ByDesign`) was its own subject for
  file-level `NoConstruct` → **M7 added** (a `NoConstruct` file must have no compiled Sharpy block; `match_statement.md` → red,
  `comments.md` → green); positive control = RAW 40-sample corpus (not the filtered generator); by-direction n/a (tests only).
- `PropertyOracleConformanceTests`: mutation S1 named; positive control = literal string containing each banned spelling; exemption =
  the literal allowlist, drained to 0 in-plan — not the subject.
- `ErrorInjector.JudgeSource` + warning arm: N1 (wrong code) / N5 (throw) per migrated lane; warning-arm mutation (SPY0463 under
  `Error` → red); inert-fix check: `Judge(InjectionResult)` must delegate, so a second core would be a conformance-scan row — add
  `new Compiler().Analyze(` outside `JudgeSource`/`SemanticFilter` to the banned spellings (warning below).
- `RejectionSink`: R1 (`LeafOfType("int")` emits a string literal → every soundness lane red with SPY0220 — `LeafOfType` is private
  static at `GenTyped.cs:19`, mutate in a worktree) and R2 (misspelled issue → `check_allowlist_issue_state.sh` red); positive control:
  the sink's count is printed per property.
- Phase 4 injections: production mutation per code at the sites now listed in Phase 4 acceptance; KnownRed pins get N3/N4;
  discriminating output: `k/k drew <code>`; the constraint control prints `2` (verified @ f8c37f986) — note it discriminates the
  *valid-program* lane, not the refusal, which is discriminated by the code.
- Refusals by direction: no program's verdict changes (tests only); every KnownRed pin records the prior behaviour verbatim.

### Warnings
- **Phase 2 Task 3 ordering vs the warning arm:** `PatternMatchPropertyTests` migrates in the third commit; the generator repair
  (SPY0266 → gone) must be in that same commit or the lane is red between commits. Phase 0 Task 2's 600-sample drawn-code table will
  show `SPY0266, SPY0463` on all three variants — treat it as the generator-defect bucket, not a compiler cell.
- **`ImportResolutionPropertyTests` line 189 is a warning-threshold lane** (`warned > tested / 2`): it migrates through the warning
  arm of `JudgeSource`, which must therefore accept a `Gen<(Main, Lib)>` two-file input or the lane is split out — say which in the
  Phase 2 Task 3 commit.
- **Inert-seam hazard:** a property that calls `new Sharpy.Compiler.Compiler().Analyze(` directly and inspects codes is a second judging
  core; add that call (outside `ErrorInjector`, `SemanticFilter` and the soundness lanes' accepted-program path) to the conformance
  scan's banned spellings, with its positive control, or the "one seam" claim is unenforced.
- **SPY0237 is spelled `CannotInferGenericType`** in `DiagnosticCodes.cs:313` but reports "Inferred type 'Plain' does not satisfy
  constraint" (`GenericTypeInferenceService.cs:746`): the roster records the literal code; a message/name mismatch is a `lane:message`
  comment on #2212, not an issue.
- **Scan scope:** the conformance scan reads `Properties/Semantic/*.cs` only; `Properties/CodeGen/CsCleanPropertyTests` (a
  `SemanticFilter` caller) and `Properties/Metamorphic`/`Parser` (one weak spelling each, different generators) are outside it — state
  in Phase 2 Task 2 that the scan covers `Properties/**` minus the GapDiscovery sweeps, or record why not.
- **`Judge`'s pin semantics on the N3/N4 shape:** `Judge` returns a failure when a pinned injection IS refused — so "an injection that
  is refused today under the pin → red" (Phase 4 acceptance) is already the shipped behaviour; the mutation to record is dropping the
  pin (→ "must draw exactly … drew [nothing]").
- CI shard 5 (`Properties.` minus `Properties.Parser.`) holds every class this plan touches; its wall-clock delta is the one to
  record in Phase 5.

### Missing Steps Added
- `## Scope` → filing budget line (§10/§11).
- Phase 1 acceptance → M7 (`NoConstruct` positive control).
- Phase 1 Task 3 → fragment/tuple `Gen<T>` resolution; a named AST walker.
- Phase 2 Task 2/3 → the four omitted classes and the match-generator repair.
- Phase 3 Tasks 1/2 → all 24 `SemanticFilter` call sites and the full threshold list.
- Phase 4 acceptance → the report site per injection code.
- DD11 → `KnownRed` in the N record's type name.

### Unchecked Claims
- Phase 0's raw census numbers (member × bucket at 200 samples) and the 600-sample drawn-code table for the weak lanes — the plan
  defers them to Phase 0 by design; the verifier re-measured the single-program cells only (14 probes @ f8c37f986).
- `SemanticFilter` rejection rates per caller (what fraction of each generator the filter discards today) — Phase 3's sink is the
  instrument; unmeasured by the verifier.
- The owner's "sure a" ruling on Design Decision 3a is recorded in the plan text and memory only (no issue-comment link); Phase 5's
  #2288 post should quote it so the ruling is on the tracker.
- CI shard 5 timing baseline @ f8c37f986 — read it from the latest green `dotnet10.yml` run before Phase 5 rather than deriving it.

## Implementation Checklist

Base sha **f8c37f986** (agent worktrees branch from `8e0791f85`, the mainline merge — `git diff` empty, same tree) · baseline gate **80351/0/4 @ f8c37f986 (measured)** (`.claude/tmp/dotnet-serialized-0.log`, 15:32–16:11, P54 verify round — same sha, reused) · board = this section (no team tools in this harness) · parallel agents work in isolated worktrees and the lead cherry-picks onto `dev` in plan order.

- [x] P0 — raw census (78 members × 200) + drawn-code table (weak lanes × 600) + caller counts + bucket classification → `<scratchpad>/p63/` — census agent (worktree, no commit)
  - P0 measured @ 8e0791f85 (src tree = f8c37f986), report `<scratchpad>/p63/census-report.md`: 78 members / 94 shapes × 200, 0 throws, 145 s. Generator defects: **G1** slot-less empty `[]` (SPY0227; `ExpressionOfType(WithCollections)`, `TypedProgram` stmts, `IfElseStatement`, `ListOfType`); **G2** `ModuleWithFunctions(WithFunctions)` calls undefined `greet`/`add`/`is_positive` (SPY0200 134/200, `GenFunctions.cs:92`); **G3** `ModuleWithIterProtocol(true)` 0/200 clean (undeclared `self.index` SPY0203 + `__iter__` without `-> Self` SPY0260). **U** = the #2169 Unparser cell (12 rows, 2–17 samples). Drawn codes ×600: MatchNonExhaustive SPY0266+SPY0463 (repair); AwaitOutsideAsync SPY0273+SPY0274 (`await process()` of a sync fn — repair to one fact); IterProtocol(false) SPY0203 for the WRONG reason (undeclared field) — the protocol refusal is SPY0320 only when iterated; InvalidDunder splits SPY0321×423 / SPY0414×177 by variant (split the lane); GenericConstraint SPY0300 in-process (not SPY0301); single-code: VarianceOnClass 0417, CovIn 0418, ContraOut 0419, Interface(false) 0325, BareRaise(false) 0242, MissingEnterOrExit 0324, WrongTypeArgCount 0224, TypeMismatchOnGenericField 0220, DecoratorStack(false) 0430, NameCollision 0204, CircularImport 0302, UnusedImport warns SPY0452 ×600 clean. 14 members have no caller (NarrowedBlock, ListOfType, ListSubscript, ClassDef, ConstructorCall, FunctionDef, ImplementingClass, ModuleWithNestedTry, six MatchWith*). No new compiler cell.
- [x] P0.T3 — findings ledger seeded (`<scratchpad>/findings-ledger.md`) — lead — done at start
- [x] P1 T1–T4 — `TypedGeneratorRosterTests` — roster agent → 61e619e04 (TypeEnv ordinal-sorted: seeds reproduce across processes — finding in #2288's class, cured in-plan), 837c0d8ee (T1+T2 folded), ec65a7e68 (RAW control + `AstDescendants`, #2169 parse pin), 21386583e (anchors, M1–M9). Coverage: rows 202 / Y-gen 87 / Y-inj 14 / N-lanes 272 / ByDesign 21 / KnownRed 2 + 4 NoConstruct over 125 files (measured). 32 lanes cite #2288 and must drain in-plan (`<scratchpad>/p63/roster/rows2288.md`: 13 dead-member → P3 T3; 8 Phase-2 → flip after P2 T3; 9 Phase-4; constraint generator; circular import)
- [x] P2 T1 — `ErrorInjector.JudgeSource` + warning arm — seam agent → 103be325c
- [x] P2 T2 — `PropertyOracleConformanceTests` + seeded allowlist + positive control + S1 — seam agent → db7647b56 (34 rows / 64 hits); lead widened the universe to all of Properties/Semantic → 58d3bc0e4 (+CombinedFeature ×4)
- [x] Recovery note 2026-10-15 ~23:00: the Mac rebooted; /private/tmp (scratchpad: census report, ledger, briefs, agent reports) was wiped. Rebuilt durably under `.claude/tmp/p63/` (gitignored): common-brief.md, sink-brief.md, census-summary.md, findings-ledger.md, rows2288-dead-member.md. Agents now write reports under their own worktree's `.claude/tmp/p63/`.
- [x] P2 T3 — twelve refusal lanes exact: lanes-A 3829bc4cc..2155c8d2c, lead roster flips eb0c9ef16; lanes-B aee4a04ee 24dab9552 cea71d4c5 d7817d22a(P4 T1: constraint lane, SPY0237, #2212 pin) 94a9af126(JudgeDiagnostics core + JudgeProject multi-file) 26cf9d47f, lead roster flips 181420cfa. Allowlist 34 → 12 rows (all Phase-3 soundness). Edit loop 17964/0/0 @ 181420cfa (measured). Lanes-B finding (user-iterator formatting consumes it) → #2057 comment 6207613364
- [x] (running, worktree, brief `.claude/tmp/p63/sink-brief.md`) P3 T1–T2 — `RejectionSink`, 24 call sites, every soundness lane asserted, R1/R2 — sink agent — blocked by P2 T3
- [x] P3 T3 (lead addition, roster finding) — a soundness fact through the sink for every DEAD valid member whose construct the roster rows (six `GenMatchPatterns.MatchWith*`, `GenExceptions.ModuleWithNestedTry`, …); their Generated rows cite `#2288` ("no property samples the member — wired in-plan") and drain here — sink agent — blocked by P3 T2
- [x] P3 note (roster finding, filed as a cell on #2169 → https://github.com/antonsynd/sharpy/issues/2169#issuecomment-6205340014): `GenTyped`'s nested conditional in test position unparses without parens → SPY0104; the sink pins it `KnownRedRow("#2169", …)` keyed structurally (Test is a ConditionalExpression), never by working around it in the generator
- [x] (running in parallel with P3, brief `.claude/tmp/p63/injections-brief.md`; T1 done by lanes-B) P4 T1–T6 — new injections + production mutations; roster Injected flips — injections agent — blocked by P1, P3
- [x] Lead filing (roster finding) → **#2324** filed 2026-10-15 (62 rows; roster re-cites them in its seeding commit): ONE class tracker "typed-generator injection reach beyond #2288's eight lanes" (residue of item 3; cell table = the roster's rows `KnownRedRow("#2288", "generated; no injection breaks it — outside Phase 4's eight lanes")`), then re-cite those rows to it in one commit — lead — blocked by P1
- [x] P3 + P4 merged: sink 5cab708f2..a229fc374 (allowlist → 0 rows; G1/G2/G4 generator defects fixed; dead members wired), injections 4073187fb..86ea7a6a7 (8 injections, 600-sample lines, 8 production mutations); lead anchor merges summed per commit; combined edit loop 18002/0/0 @ 86ea7a6a7 (measured); lead R1 re-run → 36/315 red, restored. Lead extras: c285511dc (issue-state gate: only an OPEN issue is a cite), dd4ca1ec3 (FilteredTypedProgram run-failure skip → recorded failure). Roster final: 202 / Y-gen 103 / Y-inj 32 / N 238 (#2287 ×176, #2324 ×62) / ByDesign 21 / KnownRed 2 / NoConstruct 4; zero rows cite #2288.
- [x] Refuting verifier @ 26fb59868 (`.claude/tmp/p63/verify-report.md`): F1 line-based scan + ValidatorOrdering multi-line skip (live), F2 swallowed throws / Differential generated arm drops, F3 roster↔property link unguarded, F4 #2169 pin explains any SPY0104, F5 #2212 pin target-typed (ungated), F6 warning arm counts codes not diagnostics → hardening agent (brief `.claude/tmp/p63/harden-brief.md`, H1–H8; H5 moot per R-HP) → cherry-picked onto dev after the owner's mainline merge 56151bb56: 0fb0aefc6 (H1) 84f4b9f97 (H2) 5c6a89f6c (H6) 0a0af5b05 (H7) 31c783e0f (H4) 9691845cd (H3); report `.claude/tmp/p63/harden-report.md`; H8 600-sample lines in the report. Edit loop 18011/0/0 @ 9691845cd (measured). Lead: hardening ledger's CFG floor → the lane checked 0 CFGs from 50 programs (vacuous) → rewired through the sink, f10b8381f (M-skip/M-prod red, prior lane green under M-prod); lead re-ran M7 on dev → 12/12 red, restored green; close-out 600-sample lines for the 7 post-census generators (`.claude/tmp/p63/h8b-lines.log`) all single-code; build_tools pytest 1150/0/1; issue-state gate exit 0
- [x] P5 T1 — `gap-discovery-contracts.md` row → 26fb59868 — lead — blocked by P4
- [x] Final — whole-solution gate **80410/0/4 @ f10b8381f (measured)** (`.claude/tmp/p63/gate-closeout.log`; Core 3989, Stdlib 4273, Lsp 898+1, Cli 21944, Compiler 49306+3); `dotnet format whitespace sharpy.sln --verify-no-changes` exit 0; allowlist issue-state gate exit 0 (before and after close); build_tools pytest 1150/0/1; refuting verifier = the 26fb59868 round + hardening; dev pushed 56151bb56..f10b8381f
- [x] P5 T2 — record posted https://github.com/antonsynd/sharpy/issues/2288#issuecomment-6307566428; ledger filed (#2324, #2169/#2057/#2212 comments; README unlinked-files + out-of-universe weak oracles noted in the record); #2288 CLOSED 2026-10-20

<!-- Issues closed by /close-issues on 2026-10-20: #2288 -->

## Implementation Verification (/verify-implementation, 2026-10-21)

Scope `f8c37f986..27bf064bb` (44 plan commits + 4 remediation; 14 non-plan mainline merges in range out of scope). Auditors: completeness NOT REFUTED; regression NOT REFUTED (0 red @ HEAD, regen no diff, sweeps green, determinism 375/0/0 ×2); class-cure REFUTED 5 guard-strength claims; sibling-cell prober REFUTED M-B (2 filtered/no-throw lanes) + M-A roster rows. Reports: `.claude/tmp/p63-verify/`.
- Fixed (each mutation-tested): 13c4ec04b TypeSoundness ×2 run-failure name filter + OverloadOrder caught-throw-as-signature (MB print throws → 3 red; MC Compile throws → vacuous at f10b8381f, red after; restored 22/22); 7f22fcf9c JudgeSource error-arm "exactly one code" cell (Contains-mutant → red); e8933d3ff issue-state PR-url mapping pytest (revert → 3 red, restored 29); 27bf064bb cleanup.
- Filed: #2335 (declaration-site rules left to Roslyn: entry-point CS1558, class access decorators CS0107/CS1527); comments #2212 (constraint method/builtin cells), #2021 (@dataclass arm), #2324 (instrument-strength residue: 43 scan evasions + exemption-as-subject, H4 link name-only, ~7 false roster rows, CFG straight-line only, opaque all-refused red).
- Gate **80411/0/4 @ 27bf064bb (measured)**; format exit 0; build_tools/tests pytest 1149/0/0; issue-state gate exit 0.
