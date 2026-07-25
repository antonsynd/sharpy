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
| Explicit generic type args per callee kind (#1002–#1004, #1133, #1136, #1138, #1141, #1142, #1147, #1148) | `callee[T,...]` resolves identically for every callee kind, called or uncalled | `GenericReferenceConformanceTests` — 121-cell matrix: 7 callee kinds × 6 usage forms × 4 arities + typo/probe axes, executed subset | #1143 |
| Front-end drift (#1059, #1061, #1097, #1109, #1140) | Same source + options ⇒ same diagnostic multiset from Analyze/Compile/REPL/LSP | `FrontEndParityTests` — fixture corpus × 4 entry points, documented normalization rules, each citing its tracking issue | #1144 |
| Parallel-site replication gaps (#1105, #1106, #1135; #1124, #1125, #1150–#1152; #1065, #1075) | Mirrored facts are structural (one seam) or guarded by a completeness scan | `ModuleExportsMirrorConformanceTests`, `WrapperNodeUnwrapConformanceTests` — Roslyn source scans of the compiler + LSP | #1145 |
| Un-lowerable accepted programs (#1000, #1009, #1067, #1068, #1095, #1110, #1122, #1138, #1139, #1141, #1153-adjacent) | Reproducible SPY0908/CS-leak ⇒ a semantic-time check is missing | The sweeps' csLeak/ice buckets + ILCompiles/CsClean property tests | #1146 |
| CPython semantic divergence (#1063, #1066, #1070–#1073, #1085, #1098, #1153, #1154) | Shared-subset programs produce CPython-identical stdout, or the divergence is in `docs/deviations.yaml` | `DifferentialExecutionTests` — Sharpy binary vs batched `python3` over hand-picked probes + subset fixtures + generated programs | (oracle is the mechanism; #1030 corpus keeps growing) |

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

## Follow-ups

CI/skill wiring for the new harnesses: #1156. Metamorphic transforms at corpus scale: #1157.
