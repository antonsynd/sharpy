# Gap-Discovery Contracts

**Status:** Active · **Established:** 2026-07-25 · **Origin:** defect-class audit of issues #1000–#1142

The post-#1000 issue history shows compiler bugs arriving in *classes* — groups sharing one violated
contract — while being discovered *accidentally*, one cell at a time, during unrelated verification
work. This document records the classes, the contract each violates, and the standing harness that
now hunts and pins each class. The companion rule: **a member bug is closed by enforcing its class
contract, not by patching its cell** (each umbrella issue below states the acceptance criterion).

## The pattern: conformance sweep + ratchet ledger

Each discovery harness follows the interop-sweep model (#1034):

1. **Enumerate** an input matrix exhaustively (not sample it).
2. **Classify** every cell: ok / deliberate-diagnostic / failure-bucket.
3. **Report** one aggregated JSON to `.claude/tmp/<name>-report.json`.
4. **Ratchet**: the test fails on any failure not listed in the sweep's allowlist file; every
   allowlist entry cites the GitHub issue that will drain it. Allowlists must trend to empty —
   an empty allowlist is the class contract fully enforced.

Slow sweeps carry `[Trait("Category", "GapDiscovery")]` and run via `/gap-analysis`; fast
source-scan guards run in the regular suite.

## Classes, contracts, harnesses

| Class (issues) | Violated contract | Standing harness | Umbrella |
|---|---|---|---|
| Explicit generic type args per callee kind (#1002–#1004, #1133, #1136, #1138, #1141, #1142, #1147, #1148) | `callee[T,...]` resolves identically for every callee kind, called or uncalled | `GenericReferenceConformanceTests` — 158-cell matrix: 9 callee kinds × 6 usage forms × 4 arities + typo/probe axes, executed subset | #1143 |
| Front-end drift (#1059, #1061, #1097, #1109, #1140) | Same source + options ⇒ same diagnostic multiset from Analyze/Compile/REPL/LSP | `FrontEndParityTests` — fixture corpus × 4 entry points, documented normalization rules, each citing its tracking issue | #1144 |
| Parallel-site replication gaps (#1105, #1106, #1135; #1124, #1125, #1150–#1152; #1065, #1075) | Mirrored facts are structural (one seam) or guarded by a completeness scan | `ModuleExportsMirrorConformanceTests`, `WrapperNodeUnwrapConformanceTests` — Roslyn source scans of the compiler + LSP | #1145 |
| Un-lowerable accepted programs (#1000, #1009, #1067, #1068, #1095, #1110, #1122, #1138, #1139, #1141, #1153-adjacent) | Reproducible SPY0908/CS-leak ⇒ a semantic-time check is missing | The sweeps' csLeak/ice buckets + ILCompiles/CsClean property tests | #1146 → policy: [spy0908-policy.md](spy0908-policy.md) |
| CPython semantic divergence (#1063, #1066, #1070–#1073, #1085, #1098, #1153, #1154, #1202) | Shared-subset programs produce CPython-identical stdout, or the divergence is in `docs/deviations.yaml` | `DifferentialExecutionTests` — Sharpy binary vs batched `python3` over hand-picked probes + **every eligible** subset fixture + generated programs; the fixture arm is full-pool enforced since 2026-07-31 (#1202) — the hand-picked and generated arms are fixed-size by design — and the report states its own coverage | (oracle is the mechanism; #1030 corpus keeps growing) |
| Emit fragility under semantics-preserving syntactic variation (#1147, #1167, #1168, #1169, #1170, #1171) | Rewriting a program into an equivalent form changes neither the diagnostics it produces nor what it prints | `MetamorphicCorpusSweepTests` — every executing fixture × 9 transforms (~14,600 cells, compile-clean + C# bind) and `MetamorphicCorpusInvarianceTests` — sampled execution, stdout vs the fixture's `.expected` | #1157 |
| Precedence inversion in emitted trees (#1712, #1727-A) | The emitter's tree and the text it prints are the same program: every operand whose C# precedence is lower than its parent operator's is a `ParenthesizedExpressionSyntax`, at every seam that wraps a generated expression | `EmittedTreePrecedence.Operand` and its factory wrappers are the one seam; `EmittedTreePrecedence.Violations` is asserted on every `EmitterTestPipeline` emission and over both `ReparseEquivalenceConformanceTests` corpus arms (which also diff binding diagnostics in both directions and capture units whose C# compile failed); in production `CompilerInvariants.AssertEmittedTreePrecedence` names a violation as SPY0524 before the C# compile | none — SPY0908-as-a-net, [spy0908-policy.md](spy0908-policy.md) |
| Dispatch-site vacuity (#1694, #1709, #1715, #1716) | A hand-rolled dispatch over a compiler tree kind (`Node`, `IrNode`) is total or carries a roster that derives from the switch and names the justified-default set, and the standing inventory sees every such dispatch regardless of scrutinee spelling or project | `DispatchSiteInventoryTests` — compilation-level typed-scrutinee census over `src/Sharpy.Compiler` and `src/Sharpy.Lsp`, fail-loud on unresolved scrutinees; the `*TotalityTests` family consuming `SwitchArmScan` pins per-site arm sets against reflection universes | #1715 |

Well-guarded classes needing no new mechanism: lambda-boundary parsing (differential parse oracle,
#1037), unparser fidelity (round-trip property tests), CLR name round-trip (interop sweep + #1040),
deployment closure (`StandaloneDeploymentTests`).

## Policy riders

- **SPY0908 is a net, not an error channel** (#1146): every fix for a CS-leak names the semantic
  check or lowering it adds.
- **Mirrored facts ship with their guard** (#1145): a new SemanticInfo dictionary needs a
  `MergeFrom` entry; a new wrapper node or mirror map needs a conformance-scan entry — in the same PR.
- **Parity normalizations are debt**: every justified entry-point difference in
  `FrontEndParityTests` cites an issue whose acceptance criterion is deleting the rule (#1149).
- **Divergence is never silent**: differential-oracle mismatches become issues or
  `deviations.yaml` entries; the ledger must match shipped behavior (#1155 is the cautionary tale).
- **A sweep states its own coverage** (#1202): the differential-exec allowlist once held a single
  entry for a deviation that manifested in four corpus cells, because the default budget sampled
  22 of 527 eligible fixtures and nothing in the harness or the report said so — the allowlist's
  guarantee was silently 4% of the contract. A sweep now runs its whole eligible pool by default,
  reports pool size / cells run / coverage %, and attributes every fixture it keeps out of the pool
  to a named shape. A cell CPython cannot run is excluded by an attributable AST rule, never by a
  blanket "CPython errored ⇒ skip" — the blanket rule would also hide the case that matters most,
  a program CPython legitimately rejects and Sharpy accepts.
- **Allowlists drain when their bug dies** (#1157): the metamorphic sweep fails on a *stale* entry —
  an allowlisted cell that has started passing — so deleting the lines is part of landing the fix
  rather than a follow-up nobody schedules. The other sweeps' allowlists should acquire the same
  check as they are touched.

### Metamorphic sweep — scope notes (#1157)

- **Only valid programs.** The corpus is fixtures with an `.expected` sidecar. A transform is only
  guaranteed semantics-preserving on a program that compiles; "does a rewrite preserve a *diagnostic*"
  is a different property with a different contract (an error fixture's message, location and code
  would all have to be re-derived under the rewrite), and mixing the two would make every cell's
  verdict ambiguous. `.error`/`.warning`-only fixtures are therefore excluded, not skipped silently —
  the eligibility rule is stated in the report's `scopeNotes`.
- **Multi-file fixtures participate through their entry file only.** A transform has no defined
  meaning across module boundaries; `main.spy` is rewritten and the whole project compiled around it.
- **"No regression" is a per-transform delta contract, not equality.** `MetamorphicTransforms`
  states which diagnostic codes each transform may introduce (dead-code-after-return ⇒ the
  unreachable-code and unused-variable warnings; every other transform ⇒ none). A transform without
  an entry throws rather than inheriting a permissive default.
- **A transform that is not semantics-preserving on a shape must decline it**, returning the source
  byte-identical (which the sweep records as `notApplicable`). Widening the allowlist to cover a
  transform's own bug is the one thing this harness must never do — six such false violations were
  fixed in the transforms during the first triage, not absorbed.

## Follow-ups

CI/skill wiring for the new harnesses: #1156. Metamorphic transforms at corpus scale: #1157 (landed —
harness, ratchet and wiring in place; the bugs its first run found are #1167–#1171).
