# Remediation round plan — 2026-08-13

Scope: all 90 open issues (excluding #1020/#1021, parked by owner). Every issue read in full;
each entry below states what the issue is and the recommended remediation, with the batch chosen
so that fixing the batch **retires the defect class**, not just the member cells.

---

## 1. The convergence question

The owner's standing question: is this an endless loop of fixing, or does it converge?

**Where the 90 issues actually came from** (by provenance stated in each issue body):

| Source | ~Count | Meaning |
|---|---|---|
| New conformance sweeps/mirrors built in the last 3 rounds (SymbolFactMirror, QualifiedBareSpelling, JsonLoadsAgreement, value-position sweep, FrontEndParity's new entry point) | ~15 | **Latent gaps that always existed** — the instruments finally see them |
| The two 2026-08-13 `/verify-implementation` rounds (#1491–#1509) | ~19 | Architectural review of recent commits: mostly latent adjacents + a few exposure regressions |
| Found adjacently while implementing something else, then deliberately split/filed | ~45 | Latent gaps + honest scope-splitting (the discipline working) |
| Genuine regressions where a fix made something worse or reachable | ~5 | #1496 (honest registration exposed a throwing stub), #1354 (#1251 made a latent bridge lie observable), #1450 (exposed by #1408), #1500 (mechanism unification widened exposure), #1508 (fix pair reopened a hazard) |
| Roadmap/perf placeholders | 6 | #1099–#1103, #1140 — not defects |

So the inflow is **not** primarily "fixes creating bugs." It is a compiler that launched with a
large latent-gap surface, plus a discovery apparatus that got dramatically better at seeing it.
That is the healthy shape: the count looks scary precisely because the instruments are good.

**The convergence model.** Issue inflow = (new instruments × latent gaps) + regressions.
The regression term is already controlled by ratchets (allowlists that drain on fix, snapshots,
differential/metamorphic sweeps). The latent-gap term decays as the *agreement axes* saturate —
and the axes are finite:

| Agreement axis | Instrument | Status |
|---|---|---|
| qualified vs bare spelling | QualifiedBareSpellingConformanceTests | exists, allowlist draining |
| front doors (run/emit/analyze/REPL/LSP) | FrontEndParityTests | exists, allowlist draining |
| declared vs from-import vs qualified vs extraction | SymbolFactMirrorConformanceTests | exists, allowlist draining |
| cold vs warm build (cache) | SymbolSerializer round-trip conformance | exists (#1444/#1474 drain it) |
| typed vs untyped stdlib doors | JsonLoadsAgreementTests | exists |
| compiled output vs CPython | DifferentialExecutionTests | exists |
| semantics-preserving transforms | MetamorphicCorpusSweepTests | exists |
| front-end accepts ⇒ Roslyn compiles | ILCompiles/CsClean + generic-reference sweep | exists (#1146's verifiers) |
| **annotation path vs expression path** (same CLR type, two producers) | **none yet** | #1496/#1497 class — build it |
| **emitter rewrite × scope shadowing** (lambdas/nested defs/comprehension targets) | **none yet** | #1500/#1498 class — build it |
| **CLR call checking parity** (instance vs static vs operator dunder) | **none yet** | #1451/#1395/#1501 class — build it |

**Definition of done for "confidence in the compiler"** (proposed):

1. **No open wrong-code issue** (silent wrong values — the worst class) and a standing sweep for
   each wrong-code mechanism that has ever produced one.
2. **SPY0908 corpus clean** — #1146's acceptance holds: no reproducible source shape ICEs; new
   SPY0908s triaged as "missing semantic check" by default.
3. **Every ratchet allowlist empty or strictly shrinking** per round; a stale entry fails the suite.
4. **Issue inflow per verify round declining.** Track filed-vs-closed per round. Expect inflow to
   *peak* when a new instrument lands, then fall. If a round with no new instruments still files
   heavily, that's the signal the process is broken — today's data says the opposite.
5. The three missing axes above get instruments; after that, new-instrument discovery is done and
   inflow becomes regression-only.

This is normal for a compiler at this maturity. Production compilers carry thousands of open
bugs; what distinguishes a trustworthy one is (a) no known-wrong-code without a pin, (b)
ratchets that only tighten, (c) a severity mix that shifts toward cosmetic over time. Sharpy is
already unusually disciplined on (a) and (b); this plan's batching is organized to force (c).

## 2. Recurring meta-classes and their standard cures

Nearly every issue below is an instance of one of four shapes. The cure is listed once so batch
implementers apply it rather than point-patching:

| Meta-class | Instances (examples) | Standard cure |
|---|---|---|
| **Two parallel representations of one fact** | import extraction vs declaration (#1440–#1442, #1491), cache vs fresh (#1444, #1474), typed vs untyped door (#1487, #1505), emit vs run (#1490) | Identity sharing / single authority; where a copy is unavoidable, `with`-expression clones + shared helpers; agreement sweep with drain-on-fix allowlist |
| **Spelling-keyed logic where symbol/identity-keyed is needed** | #1500 (rewrite by name), #1474 (decoder arms by name), #1489 (name-keyed type paths), #1395 (`self` by literal name), #1499 (two naming channels) | Key on the symbol or the CLR identity; delete the second naming channel; totality guard enumerated by reflection, never by hand-listing |
| **Check exists for shape A, missing for sibling B** | #1456, #1461, #1438, #1443, #1451, #1452, #1342 | Move the check to the shared seam (one door), then sweep the parallel sites in the same commit; a conformance cell per site |
| **Guards that cannot fail for what they claim to guard** | #1475, #1351, #1508 (uncommitted harness), #1439 | Mutation-test the guard (break the guarded thing, watch it fail); rename guards honestly; commit the harness |

## 3. Batch structure and sequencing

| Batch | Theme | Issues | Size | Priority |
|---|---|---|---|---|
| **J** | Owner decisions | **RESOLVED 2026-08-13** — 16 rulings posted to issues; #1510 filed | done | — |
| **INFRA** | Fleet protection (lock/bench) | #1481, #1508, #1509 | S | **first (parallel with J)** |
| **D** | Wrong code & runtime crashes | 9 | L | 2nd |
| **A** | Import & symbol fidelity | 9 | L | 3rd |
| **C** | SPY0908 drain (semantic-time refusal or lowering) | 15 | L | 4th |
| **B** | Generic substitution, CLR call checking, union patterns — one authority | 6 (+#1510) | M | 5th |
| **E** | Decorator/validator fact stamping | 6 | M | 5th (parallel) |
| **F** | Cache & incremental fidelity | 5 | M | 5th (parallel) |
| **G** | Diagnostic identity & rendering | 5 | M | 6th |
| **H** | LSP correctness | 5 | M | 6th (parallel) |
| **I** | stdlib parity (json/yaml/numpy/complex) | 10 | M | 7th |
| **K** | Guard integrity | 3 | S/M | interleave |
| **P** | Parked (roadmap/perf) | 6 | — | not this round |

Rationale for the order: J is DONE (rulings posted 2026-08-13); INFRA costs almost nothing and
protects everything else (#1481 wedged the lock 2.5 h — it taxes every other batch's test runs).
D is the class users are harmed by most (silent wrong output, process crashes). A is the largest
structural payoff — the identity-sharing anchor (#1491) makes most of its sibling cells
unrepresentable. C drains the ICE backlog under the #1146 contract.

---

## Batch J — Owner decisions — **ALL RESOLVED 2026-08-13**

Every ruling below was made by the owner in the batching discussion and posted as an
owner-decision comment on its issue (16 comments, 2026-08-13). Batch J is CLOSED as a decision
gate; the entries below record what was decided and stay as implementation context. Deltas from
the recommendations as drafted:

- **#1476/#1358 — the ruling went FURTHER than recommended:** #1358's payload-type-pattern
  classification for Optional is REVERTED. Both tagged unions match via constructor-case
  spellings (`case Some(v):`/`case None():`, `case Ok(v):`/`case Err(e):` — all four already
  fixture-pinned); bare payload type patterns are refused on BOTH with a targeted steer. Filed as
  **#1510** (the Optional half + the shared contract); #1476 closes as its Result half. The
  CS8121 ICE must not return — the refusal is semantic-time. → Batch B.
- **#1342 — walker route, NOT the local route:** generalize `GenericInstantiationWalker` to the
  interface channel first; shape 1 reads the walker. Shapes 2 (refuse variant-position Self) and
  3 (base-chain impl walk) proceed independently. → Batch B, sequenced with #1449.
- **#1495 — option 1 with NO interim refusal** ("don't half-bake"): the whole GenerateTestAssert
  family lowers framework-free; the deliverable is the lowering itself.
- Everything else as recommended: #1487 both-refuse; #1505 Err-on-missing (+ `T?` absence → None,
  defaults honored, extras ignored); #1428 experimental gate; #1463 agree-via-PrimitiveCatalog +
  refuse `builtins.None` (CPython-matching — verified SyntaxError); #1347 helper-class route +
  re-measure SPY0336; #1489 refuse type-aliasing at import; #1453 display shows the semantic type
  (runtime formatting arm); #1439 fix the helper; #1475 rename now + allow-list inversion; #1460
  symbol-side `ConstantValue`; #1462 error-recovery binding; #1465 bool+int now / sexagesimal
  declined / timestamp deferred; #1412 pointer comment + park/close.

Three items were already decided before this round and are execution, not rulings: #1354
(Iterator[T] flip, 2026-08-10), #1356 (singleton collapse, 2026-08-10 — re-land after #1474),
#1464 (ranking row, work item defined on-thread).

**#1356 — two double-backed `BuiltinType` singletons (`float`/`float64`).**
Every identity/name comparison is a trap; the same program prints both spellings.
*The decision is ALREADY MADE* (owner comment 2026-08-10): collapse to one singleton per CLR
type, alternate spelling becomes a display alias (`float` canonical), the same rule covering the
integer family's asymmetry (`int` vs catalog `int32`, `long` named `int64` — instance #3 in the
thread cost #1304 twice). The inventory it required is DONE
(`docs/design/builtintype-singleton-inventory.md`, measured at 37baaff2a). A collapse attempt
(6c72b6fcf) was **reverted** (b4d9ffdd2). → **Remaining work, not decision:** re-land the collapse
per the recorded decision — but **sequence #1474 (SymbolSerializer naming channel) first**, since
the serializer's hand-spelled decoder arms are exactly what the last collapse tripped over, and
review diagnostic-text changes against the `.error` corpus before landing (the decision comment
requires this).

**#1428 — C# 14 user-defined compound assignment (retires #1394's rebind divergence).**
*Status change: the feasibility note is DONE* (`docs/design/csharp14-user-defined-compound-assignment.md`);
probe 1 compiles and runs on net10.0 — the mechanism is real. → **Recommend:** gate as an
**experimental feature** per `docs/design/feature-lifecycle.md` (alias-mutating `+=`/`|=` is a
breaking language change vs the documented rebind; the netstandard2.1 story must be resolved
before graduation). Explicitly forbid a silent semantics fork by TFM.

**#1476 — payload type pattern: Optional matches `Some`, Result refuses.**
The divergence is an assignability accident, not a rule — confirmed by code (the classifier keys
on `OptionalType` only; the in-code comment at `Patterns.cs:569-572` already says "not deliberate
design"); the 2026-08-13 thread comment formally requests this ruling. → **Recommend option (b)
now:** keep the refusal but make it deliberate — a targeted diagnostic naming `case Ok(v):` /
`case Err(e):` as the spelling. Cheap, strictly better message, keeps option (a) (extend #1358
classification to Result) open as an enhancement. If (a) is chosen instead, rule the `int !int`
ambiguity (refuse-with-message) first.

**#1464 — overload ranking has no better-conversion rule for §10.2.11 constant conversions.**
*Decision already recorded on the thread (2026-08-13):* the exclusion at the ranking site is the
documented, deliberate interim state, and the work item is defined — add the §10.2.11
better-conversion row to `overload_resolution.md` and the resolver, then flip
`allowConstantConversion` at the ranking site **with the ambiguity cells measured** (two
small-width candidates for one constant is the hazard shape). → Execution, not a new decision;
`stdlib_itertools` stays green as the regression pin.

**#1460 — a `const` binding doesn't satisfy a small-width annotation.**
Needs symbol-level constant storage (where do folded constants live?). → **Recommend:** fold the
initializer at declaration into a `VariableSymbol.ConstantValue` (const-eligible integer cases
only), since that storage also serves future const-eval; keep `static readonly` bindings refused
(correct C# behaviour).

**#1463 + #1347 (+ #1489 direction) — what does a builtin's name denote across positions.**
One ruling covering three issues: (a) `builtins.int` etc. in value position disagrees with bare
for the 8 primitive-named builtins because primitives live in `PrimitiveCatalog`, not
`BuiltinRegistry`; (b) `builtins.None` is now SPY0342 where bare `None` is a literal; (c) the
`bytes` family. For (c) *the mystery is already solved on the thread (2026-08-12):* `int.parse`
works because `int`'s static surface lives on a separate `Sharpy.IntParse` class, so the
qualifier never collides — a Core design asymmetry, not a resolution rule. → **Recommend:**
(a) route the qualified value position through the same constructor-reference tiers as bare via a
`PrimitiveCatalog` membership check; (b) refuse `builtins.None` with a message saying `None` is a
literal, not a module member; (c) **mirror the shipped design** — give `bytes`'s static members
their own helper class exactly as `IntParse` does, then add the `Builtins.Bytes` overload set,
and **re-measure** whether SPY0336 still fires (the thread says re-measure, don't predict).
Interim if (c) stalls: extend the unfamilied SPY0342 refusal to ARGUMENT positions **with
exception-type control cells** (`assert_raises(ValueError, ...)` is the #1170 over-fire hazard) —
this also serves #1490. All land against `BuiltinsQualifiedAgreementTests` (entries drain).

**#1487 — json doors disagree on lone surrogates.**
Untyped accepts (CPython-matching), typed refuses / throws `InvalidOperationException` past the
Result door. → **Recommend:** rule **both refuse** (Axiom 1 — .NET-first; System.Text.Json
validates by design), document as a deviations-ledger entry. Non-negotiable regardless of
direction: `loads[T]` must never throw — the InvalidOperationException escape is fixed under
#1504's door-hardening either way.

**#1505 — yaml `safe_load_typed[T]` silently defaults a missing required dataclass field.**
→ **Recommend:** rule **Err on missing required field** (it's the canonical "expected failure"
per the stdlib contract), decided for BOTH yaml and json doors together, with a missing-field row
added to the agreement corpus. Fields with declared defaults keep their defaults.

**#1495 — a `@test` program is uncompilable under `sharpyc run` (Xunit CS0246 behind SPY0908).**
The thread's boundary enumeration (2026-08-13) names the full residue: the whole
`GenerateTestAssert` family (isinstance, equals, in, is, startswith, endswith, not, approx) is
Xunit-coupled, and explicitly poses the scoping question for this round. → **Recommend option 1:**
lower the whole family framework-free under non-test compilation (the #1413 `assert_raises`
precedent — this is the residue of that half-migrated design), keeping the Xunit lowering only for
test-host compilation. Implemented in Batch C.

Also in J (smaller dispositions):
- **#1462** — variadic accessor parameter binds as element type (latent; declaration now refused).
  → **Recommend:** bind through the error-recovery path (`ErrorRecoveryReason`) so the recovery is
  legible, record the decision on the issue, done. No cascade-masking.
- **#1412** — #1308 walk-fallback vs #912 erasure ordering is unpinned because no program can
  reach it. → **Recommend:** no code (an unreachable guard is worse than the coupling); label
  `parked`, add a pointer comment at `FillTypeArgumentsFromSubject` naming the trigger condition
  (any change that lets a Sharpy class declare an erasable-collection base), close.
- **#1453 display semantics** (also listed in D): what does printing a bare CLR sequence mean.
  The frame is already set by the #1354 owner decision (2026-08-10): CPython-iterator surfaces
  return `Iterator[T]` (with reprs), while **plain** CLR `IEnumerable<T>` keeps #1251's
  materialize-to-`list[T]` semantics — so `"abc".reverse()` IS a `list[str]` by its own semantic
  type, and print showing a LINQ type name is the emitted type contradicting the semantic type.
  → **Recommend:** treat display positions (`print`/`str`/f-string operands) as slots — format the
  value as the semantic type says (a list), which #1251's mutation concern doesn't apply to
  (display is read-only). Small confirm, not a new design.
- **#1342 Self-in-interface bridges — restart-or-defer** (decision formally posted on the thread
  2026-08-13; batch 7 shipped none of the three shapes, no deferral note). → **Recommend:**
  implement now via the **local route** (thread the source base-annotation's type arguments —
  ~40 LOC, the plan's own recommendation, no cross-batch dependency); shape 2 (`list[Self]`) =
  refuse variant-position Self per the plan; shape 3 (inherited implementations, ~10 LOC) is
  ready regardless. Defer only the `GenericInstantiationWalker` interface-channel generalization,
  as a named follow-up.
- **#1439 flaky property test — fix or defer-with-note** (formally asked on the thread).
  → **Recommend fix:** both defects are in the test helper (stable RNG seed derived from the
  CsCheck seed; skip string content on lexer token boundaries instead of hand-scanning quotes).
  Contained, and every flake costs a triage that always concludes "the helper again". Batch K.
- **#1475 emitter-purity guard — rename vs structural** (formally asked on the thread; the round
  leaned on the guard's name twice and had to re-verify by hand both times). → **Recommend both:**
  (a) rename/re-document now (nearly free, removes the false assurance); (b) the allow-list
  inversion (CodeGen may reference only SemanticInfo/CodeGenInfo/Symbol/SemanticType) as the real
  guard, mutation-tested with a `TypeSubstitution.Apply` call from CodeGen. Batch K.
- **#1465 YAML 1.1 families** (also in I): → **Recommend:** implement bool (`yes/no/on/off`) and
  int (hex/octal/underscore) families; **deliberately exclude** sexagesimal (YAML 1.2 dropped it)
  with a doc comment; defer timestamp behind a target-type decision recorded on the issue.

---

## Batch INFRA — fleet protection (do immediately; tiny, protects every other batch)

**#1481 — dotnet-serialized: hung-but-live holder wedges the lock indefinitely.**
A finished-but-not-exited `dotnet test` driver held the lock 2h26m; waiters die at 45 min.
→ Holder-side watchdog keyed on **tee-log mtime staleness** (not wall clock — legit runs are
17+ min); measure the quietest legitimate stretch first; on detection kill the recorded child
PID, release, log loudly. **Hard requirement from the thread: the killed run must never report
success** — `dotnet test` handles SIGTERM and exits 0 (measured: 181 failures, EXIT=0), so the
watchdog must force a distinct non-zero exit code (the EXIT_CODE=3 precedent) and shout on
stderr + the tee log. Also audit other callers that kill a dotnet child and trust its exit code,
and resolve the KNOWN-UNFIXED PID-reuse wording (measure or attribute).

**#1508 — bench `--acquire-lock` reopens the orphaned-child lock steal; harness uncommitted.**
Third fix cycle for the same OOM hazard, zero committed regression harnesses.
→ Record the benchmark child's PID at `$LOCK_DIR/child` (Popen, write before wait), and — the
actual class-killer — **commit the lock-protocol regression harness** (fake-HOME/fake-dotnet,
orphan-steal probe, N-way race probe) and path-filter `.claude/scripts/**` into CI. Acceptance is
the harness in-tree, not the one-line fix.

**#1509 — unbalanced position cell flows through the pooled median invisibly.**
→ Print per-cell sample counts in `describe()`/`report()` (the field already exists), and refuse
comparison when cells are unbalanced, matching the interrupted-round drop policy.

---

## Batch D — wrong code & runtime crashes (highest user harm)

**#1500 — accessor value-parameter rewrite reaches inside shadowing lambdas → silent wrong values**
(Python 106, Sharpy 300). The rewrite is spelling-keyed and pre-resolution.
→ Suspend `_accessorParamRewrite` in any scope that re-binds the name (lambda params, nested
defs, comprehension targets) — or better, make it symbol-keyed. Probe nested `def` too (unprobed).
Then build the **rewrite × scope-shadowing sweep** (new instrument, §1) so every emitter rewrite
(`_eventHandlerParamName` heritage included) is exercised under shadowing — that retires the class.

**#1504 — json `loads[T]` on a multi-word snake_case dataclass field crashes the process.**
Constructor binding fails for every document AND the exception escapes the Result door.
→ Two fixes: (1) align the emitted dataclass ctor-parameter/property naming channels (trace which
pair diverges); (2) **catch-all at the `Loads<T>` boundary → `Err(JSONDecodeError)`** — the door
must be crash-proof by construction, not per-exception-type. Add multi-word field rows to the
agreement corpus (single-word names masked this), and land the never-implemented plan-a5d04a
Phase 3 Task 3 cell (same document through `json.loads[T]` AND `yaml.safe_load_typed[T]` into the
same dataclass, snake_case fields included) — the thread names it as exactly the test that would
have caught this.

**#1505 — yaml silent default** (ruling in J) → implement Err-on-missing for both doors together;
agreement-corpus row.

**#1496 — `DictKeyView | DictKeyView` (annotation path) compiles then crashes NotSupportedException.**
→ Implement view set-algebra returning `set[K]` (Python semantics — the stub's own message admits
the intent) rather than refusing; cover all three views × all set operators, annotation AND
expression paths. Feed the **annotation-vs-expression-path sweep** (new instrument, §1).

**#1497 — `len()` on annotation-typed DictKeyView draws SPY0320 while `len(d.keys())` works.**
→ Trace why the annotation-path symbol doesn't satisfy ISized recognition
(`IReadOnlyCollection<K>.Count`); fix the recognition, not the one type. Same new sweep covers it.

**#1354 — lazy CLR sequences materialized eagerly (remaining scope: itertools + `os.walk`).**
Owner decision made 2026-08-10 (Iterator[T] in the type system, flip everything, cheap reprs);
partial-landing status recorded on the thread 2026-08-13: iglob, reversed(str), reprs, and the
builtins are DONE; the ~24 itertools producers and `os.walk` are blocked by #1470 (22 of 25 need
tuple or type-parameter type arguments). → Unblock #1470 first, then the remaining flips are
mechanical; open acceptance = the `itertools.count` assignment repro + differential-oracle cells.

**#1470 — `iter[T](...)` fails for tuple and type-parameter type arguments; SPY0237 recommends the
spelling that fails; SPY0260 prints identical types on both sides.**
Thread narrows the diagnosis (2026-08-12): generic-argument **unification** handles `BuiltinType`
and `GenericType` but not `TupleType`/`TypeParameterType` — and the direct `return _g(xs)` has no
workaround either, so there is NO `.spy` spelling that produces an `Iterator[T]` from a generator
for these shapes. → Fix unification for both type kinds; **check whether other generic builtins
with explicit type arguments fail the same way for a tuple** (the thread suspects this is broader
than `iter` — if so, the class is "explicit-type-argument unification", not an iter bug); make
SPY0237's suggestion truthful; make SPY0260 structurally unable to print identical types on both
sides. Four-row matrix + the direct-return rows as fixtures.

**#1468 — `iter("abc")` draws SPY0237; `StringHelpers.Iterate` is unreachable.**
→ Route `iter(str)` to `StringHelpers.Iterate` exactly as `reversed` is routed (the asymmetry
between the two helpers is the bug); return `Iterator[str]`.

**#1453 — bare-position CLR sequence prints a LINQ iterator type name.**
→ Per the J ruling: type as `Iterator[T]`, print the address-free repr. Cell lands beside the
#1354 fixtures.

**#1415 — `str(exception)` returns .NET ToString()** — `str(e)`/`print(e)` halves are FIXED in
Core; → close after #1480 lands, and in the close-out either resolve or file the adjacent
`e.message`-doesn't-resolve gap the issue records (currently untracked).

**#1480 — f-string interpolation bypasses `Builtins.Str`, so `f"{e}"` leaks stack trace + path.**
→ Node-keyed `SemanticInfo` fact ("interpolated expression is exception-typed") recorded by the
checker, applied verbatim by the emitter (Critical Rule 2); `!s`/`!r` already correct; pin float/
list/user-class interpolations as byte-identical controls.

---

## Batch A — import & symbol fidelity (one symbol, one representation)

The class: `ModuleLoader` extraction and `ImportResolver` re-export clones are hand-built second
representations of symbols; every fact not restated is silently dropped, and every future fact
must be remembered at two seams or it diverges. The structural fix is **identity sharing** — after
it, most sibling cells become unrepresentable. Do #1491 first.

**#1491 — from-imported functions never get identity sharing (the a804d4f5e types fix never
covered functions).** → Extend `ResolveOwnExportedSymbol` to the overload channel: in-source-set
modules bind the project's own `FunctionSymbol`s (overload lists included). Flip the
`NotBeSameAs` pin to `BeSameAs`. This retires fact-threading-by-hand for in-source-set functions.

**#1440 — `CreateReExportedTypeSymbol` is a hand-built clone dropping NestedTypes, Documentation,
DeprecationMessage, IsMustUse, DeclarationSpan, Events (+10 more unlisted).**
→ Convert to a `with` expression restating only what a re-export changes — inverts the default
from "drop unless listed" to "carry unless listed", which makes the bug unrepresentable. Same for
the `VariableSymbol` arm. Preserve the #1403 aliasing comment. Mirror cells drain.

**#1441 — extraction drops per-member facts (DeclarationSpan, IsNameBacktickEscaped,
ExplicitAccessLevel; `self` typed Unknown; and — per the thread's fifth member —
`DeclaringFilePath`/`DefiningFilePath` are NEVER set on anything extracted, so go-to-definition
on any imported symbol resolves to nothing outside the source set).**
→ Member extractors call the same helpers `NameResolver` uses (the #1365 `SignatureKey`
precedent) instead of restating formulas; stamp both file paths from `CurrentModulePath` (the
extractor already has it — its own diagnostics use it). Mirror cells drain.

**#1442 — extraction never computes IsStringEnum/IsDataclass** (imported string enum treated as
int enum; imported @dataclass loses synthesized members).
→ Compute both from the AST the extractor already walks (like `@must_use`/`@deprecated` in
#1365); synthesize dataclass members the way the declaring pass does or share that pass's helper.

**#1448 — extraction keeps the DOTTED name in an imported module's own annotations**
(`inner.Box[int]` vs `Box[int]`). → Normalize to the resolved symbol's name exactly as
`TypeResolver.ResolveGenericType` does; qualified/bare allowlist entries drain.

**#1436 — module-qualified type alias (`lib.Handle`) does not resolve.**
→ Alias-aware seam in the qualified path (sibling `ResolveQualifiedAlias` or widen the resolver
return to `Symbol?`); expand through the existing alias-expansion helpers; plain + generic cells.

**#1445 — module-qualified class pattern is a parse error (`case lib.Circle():`).**
→ Accept a dotted name where a class pattern's type is parsed; route through the same qualified
lookup the annotation position uses. Four allowlist entries drain. (The qualifier changing
whether the program *parses* is the coarsest possible Design-Decision-3 violation.)

**#1447 — closed module-qualified generic in type-test position (`isinstance(x, lib.Box[int])`)
leaks CS0305; type arguments dropped between resolution and emission.**
→ Carry the resolved type arguments through the qualified spelling (find where the qualified path
loses them vs the bare path); semantic-time check per #1146 if any shape remains unloweable.

**#1435 — imported nested type emits without its declaring chain (CS0426).**
→ Run the declaring-chain walk in `TypeSyntaxMapper`'s qualified branches too (the computation
exists in the current-file branch); check `GetMappedTypeName` and the `UserDefinedType` arm for
the same drop. Multi-file fixture: class/struct/enum cells.

---

## Batch B — generic substitution & CLR call checking: one authority

**#1449 — members inherited from a source-declared generic base are typed at the OPEN `T`.**
`Direct(Root[int]).get()` → SPY0220 "Cannot assign 'T' to 'int'". Thread confirms: the **write
path fails too** (`g.second = "direct"` → inverted message), a fully-written base fails
identically (not an inference gap), and the direct-instance control passes.
→ Type the found member under the composed base-chain substitution
`GenericInstantiationWalker`/`InstantiateBaseType` already produces (the #1287/#1409 authority) —
in the `UserDefinedType` member-lookup arm AND the field/property/event walks beside it, checked
in BOTH directions (read and write). No second reader of `BaseTypeReference`. **Acceptance trap
from the thread:** fixtures must annotate the destination — `print(g.second)` passes green
without the fix (`print` is never a type probe).

**#1342 — Self-in-interface bridges skip generic declaring interfaces, nested Self, and
inherited implementations.** → Per the J ruling (walker route): FIRST generalize
`GenericInstantiationWalker` to the interface channel (`InterfaceReference` counterpart to
`BaseTypeReference`), then shape 1's bridge reads the composed substitution from the walker — no
bridge-local reader. Shape 2 = **refuse `Self` in variant positions** (new SPY049x code +
DiagnosticExplanations entry); shape 3 = walk the base chain at the `impl` lookup. Sequence the
generalization with #1449 in this batch — same authority, two consumers.

**#1510 — bare payload type patterns over Optional/Result → constructor-case steer (reverts
#1358's classification; the #1476 ruling's wider half).**
→ Remove the #1358 payload-classification arm (`Patterns.cs:548-585`); semantic-time refusal for
payload type patterns over `OptionalType` AND `ResultType` scrutinees, each naming its
constructor-case spellings (`case Some(v):`/`case None():`, `case Ok(v):`/`case Err(e):`). The
CS8121 ICE must not return (regression cell: emit shows the refusal, run never ICEs); #1358's
classification fixtures flip to refusal pins; the narrowed form stays working; spec page updated
with executed examples. Closes #1476 as its Result half.

**#1395 — multi-candidate operator dunders can never select a CLR-discovered overload.**
**Status correction from the thread: this was attempted (9bb773217) and REVERTED (f5e7eb75f),
and the issue's premise is falsified** — the CLR fallback is load-bearing, not redundant: it is
the only thing that substitutes type arguments for generic CLR operators, and every target cell
(`dict|dict`, `set|set`, …) is on a generic type. The broken arity filter was accidentally
protecting an unfinished path (fixing it alone ICEs numpy with `NdArray<object>` constraint
violations). → **Re-scoped:** "bring type-argument substitution to the operator-dunder resolution
path" (where do the receiver's type arguments come from at that point?), with the shape-based
self-skip as a prerequisite applied to BOTH copies (`GetSelfOffset` ~:64 for arity,
`IsMoreSpecificOverload`'s `SelfOffset` ~:251 for betterness — the 2026-08-10 parallel-site
warning). Recover the five-cell offset matrix + three-cell mutation matrix from
`git show 9bb773217`; the retry MUST include a cell running a real generic CLR receiver through
the resolver (`ndarray_operators`) — synthesized-symbol tests structurally could not catch this.

**#1501 — #1290's FunctionType argument skip was to narrow when #1393 landed; it didn't.**
→ Narrow the skip to `ft.HasUnresolvedTypes()` (the stated residual case), re-point the stale
comment, add the missing #1393 LSP hover pin; `calendar_module.spy` stays the regression pin.

**#1502 — ControlFlowGraphBuilder never models match EXPRESSIONS (no MatchSubject).**
→ Teach the builder to record subjects for match expressions (its first expression-walking);
measure `NarrowingFlowAnalysis` blast radius; then give `CheckMatchExpression` the facts copy and
retire the mutation-proven trap comment in the same change.

Batch-level instrument: the **CLR call-checking parity sweep** (instance/static/operator ×
single/multi candidate) — #1451 (Batch C) and #1395/#1501 all cite the same absent axis.

---

## Batch C — SPY0908 drain (the #1146 contract: refuse at semantic time or lower correctly)

Every fix names the semantic-time check or lowering added. #1146 itself stays open as the
standing contract and closes when the corpus + sweeps are clean.

**#1490 — bare type name in ordinary call-argument position → CS0119; emit is clean.**
→ Needs a rule that distinguishes an ordinary call argument from a special form's type-argument
position (widening the existing gate broke `with assert_raises(ValueError):` — the regression
control). Semantic-time diagnostic visible from `emit diagnostics`; FrontEndParity covers the pair.

**#1489 — builtin TYPE imported under an alias: call form → CS0103; annotation form gets a clean
SPY0202.** → Per the J ruling: either full alias support (name-keyed type paths follow
`BuiltinAliasOf` in `TypeResolver` AND `TypeSyntaxMapper`) or refuse at the import with a
deliberate diagnostic. **Recommend refuse-at-import** (cheap, closes the ICE, `int` is already
reachable three ways); upgrade to full support only if users ask.

**#1495 — @test under run** → per the J ruling: the WHOLE `GenerateTestAssert` family lowers
framework-free under non-test compilation (option 1, no interim refusal — "don't half-bake");
Xunit lowering kept only for test-host compilation.

**#1478 — bare `None` argument to a value-type `T?` parameter → CS1503 ICE.**
→ Give the argument-conversion path the same `None`→`Optional<T>` conversion the assignment path
already has (find the assignment path's mechanism, share it — one door). Cells: value-type +
reference-type payloads, argument + default-value positions.

**#1461 — @abstract member in a NESTED non-abstract class ICEs (SPY0493 never fires).**
**Priority raised by the thread (2026-08-13): this is a refusal→ICE REGRESSION** — the shape was
a clean SPY0247 refusal until `8fac96434` dropped the TypeChecker arm (whose `_currentClass`
save/restore reached nested classes) in favor of a validator whose `LookupType(bareName)` returns
null for nested types (the #1371 gap).
→ Fix the validator via the enclosing symbol's `NestedTypes` (the #1371 fix's pattern) AND —
class-killer — **sweep every `LookupType(<nested>.Name)` caller** for the same defect in the same
commit; the issue explicitly warns against patching one caller.

**#1456 — observer properties synthesize a backing field outside SPY0407's check → CS0102.**
→ Widen `ValidateBackingFieldCollision`'s guard: auto-property with non-empty `Observers` also
synthesizes the field; same message with observer wording.

**#1499 — indexer occupies `Item` but the SPY0522 walk sees `GetItem` → CS0102.**
→ Reserve the indexer protocol's emitted surface (`Item` when `__getitem__`/`__setitem__`
present) exactly like `IteratorProtocolReservedNames` — and audit the other protocol surfaces
(events, operators) for unreserved emitted names while there.

**#1498 — dict-spread comprehension loop target registered unscoped → CS0136 + version corruption
on the outer local (also wrong-code).**
→ Mirror `GenerateImperativeComprehension`'s save/restore scoping in the spread path; fixture for
the enclosing-collision cell; note `LocalSlotRegistrationConformanceTests` structurally cannot see
scoping (it checks WHO writes, not WHETHER scoped) — extend the guard or note the limit honestly.

**#1450 — CLR `IEnumerable<T>` constructor parameter emitted as `Sharpy.List<T>` → CS1503.**
→ Preserve `IEnumerable<T>` in parameter position when re-expressing CLR signatures (it's the
mapping, not the forwarder); expect and cover the same narrowing on ordinary CLR methods.

**#1451 — static CLR call through a type name has no arity or argument check at all.**
→ Apply the instance seam's shape to the static receiver (#1243's rule: exactly one arity match
decides; two keep silence) + `CheckClrCallArgumentTypes`. Feeds the call-parity sweep (Batch B).

**#1452 — `set(<CLR sequence>)` lowered against the `int` (capacity) constructor → CS1503.**
→ Fix `set()`'s builtin-conversion constructor selection for sequence arguments; add the
`frozenset`/`dict` siblings as cells while there (same selection code).

**#1430 — `@[dll_import]` on a bodied function → CS0601; spec example has never compiled.**
→ Refuse with a deliberate diagnostic (no extern form exists in the language today); fix or
remove the spec example (Rule 7 + the docs-need-execution-evidence rule); revisit a P/Invoke
declaration form only as a deliberate feature.

**#1431 — source-generator trigger `@[MyGen]` emitted as ordinary attribute → CS0616.**
→ Fix `NameResolver.IsSourceGeneratorType` for the imported-generator shape so the partition
consumes the trigger; end-to-end trigger test asserts success (the existing test deliberately
doesn't — flip it with the fix).

**#1483 — `--self-contained` publish emits unmangled entry-type name; every publish fails.**
→ Mangle the entry identifier at `SelfContainedPublisher.cs:82` (the other three uses are file
names, fine); add an execution test (StandaloneDeploymentTests shape) — parse-level coverage is
why every input being broken went unnoticed.

**#1146 — umbrella.** Stays open; closes on its own acceptance (clean corpus + sweeps, allowlists
empty). **Untracked residue recorded only in its thread (2026-08-11 comment) — verify each has a
tracker or file one:** (1) imported CLR generic receivers (`system.collections.generic.List[int]`)
still leak CS1061 — the #1344 gate is name-listed, not structural, and widening it requires
enumerating that receiver's name-resolution routes first ("teach the proof, then move the gate");
(2) the registry `typeof(object)` placeholders — partially resolved by #1346 for views/iterators,
confirm nothing remains; (3) `SemanticType.UnmappedClr` is a disambiguation, not a fix — an
unmapped CLR type still presents as `object`.

---

## Batch E — decorator/validator fact stamping (check-missing-for-sibling)

**#1438 — @deprecated never warns on an overloaded call (six SetCallTarget sites, one checker).**
→ Fold `CheckDeprecatedUsage` into a `RecordResolvedCallTarget(call, symbol)` helper all seven
sites call — ends the class, not one route. Cells: overloaded same-file/imported/method/ctor.

**#1443 — @must_use on a method never warns same-file (`ResolveMethodDeclaration` doesn't stamp
`IsMustUse`).** → Stamp it; executing fixture discarding a @must_use method call (its absence is
why this survived); mirror entry drains. Audit `ResolveFunctionDeclaration` vs
`ResolveMethodDeclaration` for OTHER facts stamped in one and not the other (same class).

**#1459 — assigning a module-level property reported as unused local (SPY0451).**
→ `UnusedVariableValidator` asks whether the name resolves to `VariableSymbol{IsModuleProperty}`
before classifying a bare `name = value` as a local declaration. Warning-file pin empties.

**#1429 — an import used only by a bracket attribute is reported unused (SPY0452).**
→ Surface which namespace the bracket-attribute resolver resolved through and count it as a use
(the resolver already knows); the cheap approximation (any namespace import + any bracket
attribute = used) only as a stopgap.

**#1455 — ParameterSymbol drops IsNameBacktickEscaped; forwarders can't emit verbatim.**
→ Carry the flag on `ParameterSymbol` from construction (semantic phase); fixture per forwarder
shape. Same blind spot exists for `PropertySymbol`/`EventSymbol` — cover all three standalone
records in one pass (the #1385 lesson).

**#1462 — variadic accessor binding** → per J ruling: error-recovery binding, decision recorded.

---

## Batch F — cache & incremental fidelity (cold build == warm build)

**#1444 — SymbolSerializer drops ten facts (SignatureKey, IsMustUse, DeprecationMessage, IsFinal,
IsStatic, ExplicitAccessLevel, Properties, Events, IsDataclass…).**
→ Add fields to `CachedSymbol`, thread through, **one schema bump shared with #1474**. Acceptance
is the conformance table flipping `Dropped`→`RoundTrips` (the test fails on a stale note).

**#1474 — symbol cache silently loses nine builtin types (decoder arms spelled in a different
naming channel than the encoder).**
→ Delete the hand-written switch; resolve through `PrimitiveCatalog.GetByName` (one naming
authority); totality guard enumerating singletons by reflection, red-first, mutation-tested.
Same schema bump as #1444.

**#1473 — `TypeSymbol.Constructors` means two things depending on which path built the symbol.**
→ Populate `Constructors` on the discovery path (`LoadBuiltinTypes`) as `ModuleRegistry` already
does, so empty means empty everywhere; first check for readers relying on the unpopulated state.

**#1492 — SPY0495 falsely refuses a bracket attribute whose type lives only in a .spyproj
Reference.** → Teach the absence proof the project's reference closure (MetadataLoadContext probe,
no execution-context load), or a "references not probed" bit that downgrades to pass-through;
References-only fixture is the acceptance cell.

**#1493 — ClrAttributeResolver's static process-lifetime caches serve stale absence verdicts
(LSP order-dependence).** → Scope the cache per compilation (match `_bclMemberAbsenceMemo`, the
pattern from the same batch) or invalidate on AppDomain loaded-assembly count.

---

## Batch G — diagnostic identity & rendering (#1144 front-door parity)

**#1437 — run/build render a sibling file's parser diagnostic at a bogus location in the entry
file (offset re-derived against the wrong buffer).**
→ Both halves: stamp parse-phase diagnostics with their unit's path (and stop the `"<source>"`
placeholder for on-disk units), and route run/build through `RenderDiagnosticsFromFiles` (what
`project` already uses). "No FilePath" must never mean "same file". Regression test includes the
padding control (a wrong-but-plausible location passes message-only assertions).

**#1433 — path-blind analyze baseline hides file-identity diagnostics (SPY0302 self-import,
SPY0523 collision) from the LSP.**
→ Split the conflated axes: thread the document path into LSP analysis ("what is this file
called") while keeping `nullifyEntryFilePath` symbol-identity semantics ("whose symbols are
these"). Two frontend-parity allowlist entries drain.

**#1434 — SPY0302's cycle description starts at a nondeterministic node.**
→ Rotate each detected cycle to a canonical start (ordinally smallest normalized path) before
rendering; add a fixture pinning the full rendered cycle (current `.error` files match only the
prefix, which is why the suite is green either way).

**#1494 — a position-less SPY0908 renders with no file name (documented degradation
unimplemented).** → Thread the entry/compiling file path into `RenderDiagnosticFromFile` as
last-resort context; CLI-level test for the position-less shape.

**#1482 — GetMetadataReferences degrades silently; a dropped framework reference is reported as
a Sharpy compiler bug (181 test failures, hours of misdirection).**
Thread measurement: the trigger is **transient and rare** (identical re-run: 0 failures; handle
exhaustion and incomplete build output both eliminated) — which makes the misreporting the
defect, exactly as filed.
→ Post-condition: the reference set must contain the assembly defining `System.Object`, else fail
with an accurate reference-acquisition diagnostic (TPA seen/skipped counts); raise the swallowed
catch to a warning; distinguish "TPA empty" from "nothing survived". The next occurrence then
names itself on first contact.

---

## Batch H — LSP correctness

**#1359 — renaming a parameter produces no edits; a rebinding occurrence is left behind.**
**Two verification rounds with zero commits and no deferral note** (thread record, 2026-08-13).
A concrete plan already exists — plan-261869 Phase 1 (populate parameter symbol positions,
name-token position on `Parameter`, a node-keyed `Parameter → VariableSymbol` map in
`SemanticInfo` — which MUST join `MergeFrom` — and a `Parameter` arm in `ResolveSymbol`) and
Phase 2 (binding-chain walk). → Execute plan-261869; option 2 (refuse multi-binding renames)
remains the acceptable descope for Phase 2 ONLY, with the refusal message naming why. This is the
batch-9 straggler — it needs the work or an explicit recorded deferral, not a third silent round.

**#1360 — completion: stdlib modules expose no members; a mid-edit buffer returns nothing.**
**Half 2 LANDED** (79d8c8787 — member completion served from the last good analysis with a
byte-identical-prefix drift guard, mutation-checked; stale scope dumps deliberately refused).
→ Remaining scope is half 1 only: give the LSP a path to stdlib module members — materialize onto
`ModuleSymbol` on first resolution (the Batch-A one-representation direction) — and land it for
completion AND hover at once (both handlers share the hole, per the 2026-08-10 note).

**#1454 — AST nodes carry no name-extent END; LSP re-derives from `Name.Length` + a backtick
constant.** → Add `NameColumnEnd` (populated from `Token.Length` at parse time) to nodes carrying
`NameColumnStart`; retire `BacktickPairLength` from `RenameHandler`; symbol-side field or move
callers to the AST. **Closes the remaining half of #1380** — close both together.

**#1380 — span math backtick residue.** Sweep landed (105 sites); → close by retiring the
handler-side +2 compensation once #1454 lands, with the cursor-on-closing-backtick LSP test.

**#1503 — member semantic token span assumes zero whitespace around the dot.**
→ Interim: detect the gap and bail (the multi-line precedent); proper fix arrives with #1454's
name extents. Add the padded cells to `SemanticTokensMemberTests` either way.

---

## Batch I — stdlib parity (after the J rulings)

**#1504 / #1505 / #1487** — see Batch D / J; the door-hardening (never throw past Result) and the
missing-field ruling are the two class-level pieces; agreement corpus gains rows for both.

**#1488 — bare non-finite quoting pass makes `NaN` readable as a str through `loads[T]`.**
→ Move the concession into the reader (custom converter for non-finite tokens) instead of
pre-editing the document text — deletes the ambiguity at the source. Keep the numeric round-trip
pin green.

**#1465 — yaml resolver strict subset of YAML 1.1** → per J ruling: bool + int families with
PyYAML-verified cell tables; sexagesimal deliberately excluded (doc comment); timestamp deferred
behind a type decision. Each family added fixes dump-quoting and marker interaction for free
(the shared-resolver design working as intended).

**#1467 — safe_dump emits `---` for empty/null scalars and spells None as nothing.**
→ Fix both cells (the null value-token omission is the serious half — a conforming parser reads
an EMPTY document); both pinned assertions flip; controls unchanged.

**#1471 — safe_dump_all: no stream-end marker; separator on its own line.**
→ Rework `SafeDumpAll` concatenation to PyYAML's stream shape (`1.0\n--- hello\n...\n`); the pin
in `YamlDumpAllStreamShapeTests` is updated, not deleted; differential cells compare clean.

**#1472 — roundtrip_dump double-quotes what safe_dump and PyYAML single-quote.**
→ Single-quote unless escaping is required (the predicate is already shared; only rendering
diverges); the narrowed cross-surface test regains its quoted cells (stated acceptance).

**#1469 — `np.arange(stop)` single-argument form unsupported.**
→ Add `Arange(double stop)` delegating overload; numpy-verified cells (empty, negative,
fractional); spy test beside the existing forms.

**#1507 — complex has no numeric-tower mixing.**
→ Mixed `Complex ⊕ {int, long, float}` for the four operators + reflected forms + `==`, promoting
per CPython (verify each cell with python3 first). Imaginary literals stay a separate feature.

---

## Batch K — guard integrity

**#1475 — EmitterPurityConformanceTests is a five-substring grep that cannot fail for the
violation Rule 2 describes.** → Do the nearly-free half immediately: rename + narrow the doc so a
green result stops being citable as Rule-2 evidence. Then implement the allow-list inversion
(`CodeGen/` may reference only the materialized-fact carriers: SemanticInfo, CodeGenInfo, Symbol,
SemanticType) — stronger and still cheap compared to an analyzer. Mutation-test: a
`TypeSubstitution.Apply` call from CodeGen must turn it red.

**#1351 — the GenerateExpression re-entry tripwire cannot see re-splicing** (the half that has
actually produced bugs). → Option 1 (count splices) — the thread's 2026-08-13 comment enumerates
the exact six-step remainder (third `IExpressionGenerationRecorder` member, wire
`HoistAugmentedTargetOperand`'s decline path + 3 call sites, single-splice assertion, repeatable-
leaf exemption kept in sync with `IsRepeatableTargetOperand`, the `abc5bf4b0` mutation test that
must go red, doc-comment update). Note: `d2903ad9d` fixed a DIFFERENT defect (skip-list ratchet)
despite citing this issue — do not misread it as progress here.

**#1439 — flaky WhitespaceVariation property test (RNG seeded from randomized GetHashCode; string
skipper confused by nested quotes).** → Derive the RNG from the CsCheck seed (deterministic);
skip on lexer token boundaries instead of hand-scanning quotes. Both defects are in the helper.

---

## Parked this round (roadmap/perf — revisit after the correctness backlog drains)

**#1099** (incremental frontend — the LSP buffer-overlay + NoChange carve-out already landed;
remainder is lazy memoized binding + incremental reparse), **#1100** (object pooling — pass 1 was
a measured NEGATIVE result, −0.03%; no broad pass 2, profile first), **#1101** (pull-based
memoization), **#1102** (comprehension fusion v2), **#1103** (stack-collections escape positions),
**#1140** (LSP per-call overhead — the thread's measurement work is done and the direction is
chosen: cache the per-call invariant analysis context (ModuleRegistry + shared state ≈ 44% of the
14.4 ms real row); the single-file-fast-path direction is measured dead at 6%). None are defects;
when perf work resumes, #1140's option (a) is the shovel-ready item. Keep them out of the
remediation rounds so the correctness burn-down stays legible.

---

## 4. Round mechanics (process guardrails, from this repo's own memory)

- One batch = one plan (`/create-plan` → `/verify-plan` → `/implement-plan` →
  `/verify-implementation`), with the batch's **class-killer named in the plan header** — a batch
  whose plan lists only cells is not accepted.
- Every fix that adds a check must be **mutation-tested** (break the guarded thing, watch it fail).
- Allowlist/ratchet entries cite issues and **drain on fix** — a stale entry fails the suite.
- A closed issue is not a finished issue: closure requires the issue's own acceptance bullets,
  verified against HEAD, with measured evidence (`count @ sha (measured)`).
- Regenerate generated artifacts EARLY in each batch (spy stdlib C#, spy-test C#, snapshots) and
  read the diff as findings — the #1354 masking incident is the standing reason.
- Per-round metric to record in the round memory file: issues filed vs closed, allowlist sizes,
  and the severity mix of new filings. That series is the convergence evidence the owner asked for.
