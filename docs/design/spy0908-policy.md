# SPY0908 Policy — un-lowerable shapes are semantic-time diagnostics

**Status:** Policy · **Origin:** #1146 (converted per the 2026-08-18 owner ruling)

The companion rule: **a member bug is closed by enforcing its class contract, not by patching
its cell** — every SPY0908/CS-leak fix names the semantic-time check or the lowering it added
(#1138's SPY0335 is the exemplar). Documenting or pinning the ICE is never a close.

## The contract

A program the emitter cannot lower is rejected during **semantic analysis** with a deliberate,
actionable diagnostic. SPY0908 (`GeneratedCodeCompilationError`,
`src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs:882`) exists only to catch compiler
bugs — it is a net, not an error channel. Any reproducible SPY0908 or raw CSxxxx leak for a
describable source shape is by definition a missing semantic-time check.

## Review rule

Every SPY0908/CS-leak fix names the semantic-time check or the lowering it added. Refusals
are verified **by direction**: before/after against the prior commit distinguishes "replaces an
ICE with a diagnostic" from "restricts working code" (the round-8 Batch B lesson, recorded in
#1146's thread).

## Triage defaults

- A new SPY0908 report is a missing semantic check until proven otherwise.
- **SPY0908 only surfaces under `run` — never refute an ICE report with `emit`.** The
  last-chance CSxxxx→SPY0908 remap lives in `AssemblyCompiler.cs` and only fires during
  assembly compilation, not during `emit csharp`.
- **The untyped-vs-mistyped probe:** `b: bool = expr` — if SPY0220 fires, the expression is
  mistyped (wrong type); if silence, it is Unknown (untyped). The bug is upstream of the
  emitter in both cases; the probe determines which resolution path to take.

## Enforcement-seam inventory

| Seam | Location | Purpose |
|------|----------|---------|
| CSxxxx→SPY0908 remap | `src/Sharpy.Compiler/AssemblyCompiler.cs` | Last-chance catch — wraps raw C# compilation errors as SPY0908 |
| generic-reference sweep | `src/Sharpy.Compiler.Tests/Conformance/` | csLeak/ice buckets in the generic-reference harness |
| interop sweep | `src/Sharpy.Compiler.Tests/Conformance/` | csLeak/ice buckets in the interop harness |
| metamorphic sweep | `src/Sharpy.Compiler.Tests/Conformance/` | csLeak/ice buckets in the metamorphic harness |
| ILCompiles property tests | `src/Sharpy.Compiler.Tests/Properties/CodeGen/` | Random-seed property tests asserting emitted C# compiles |
| CsClean property tests | `src/Sharpy.Compiler.Tests/Properties/CodeGen/` | Random-seed property tests asserting no raw CSxxxx leaks |
| EmitterCarrierOnlyConformanceTests | `src/Sharpy.Compiler.Tests/CodeGen/` | Rule 2 — decisions cannot be taken emitter-side (prevents un-lowerable shapes from being introduced) |
| Ratchet policy | all allowlists | Drain-on-fix, entries cite issues, allowlists trend to empty |

## Starting census (measured @ `8bacf3d34`)

| Sweep | Allowlist entries |
|-------|-------------------|
| generic-reference | 0 |
| interop | 0 |
| metamorphic | 0 |
| path-agreement | 0 |
| rewrite-shadowing | 0 |
| warm-diagnostic-fidelity | 0 |
| qualified-bare | 5 |
| differential-exec | 22 |
| LSP frontend-parity | 32 |
| EmitterCarrierOnly ratchet (per-file-per-type) | 0 |

## Precondition evidence

- **#1139** — verified at HEAD (`3422ea9f3`): `List[int]()` constructs and prints count; no
  SPY0908.
- **#1141** — verified at HEAD (`3422ea9f3`): `lst.no_such_method[str](1)` on a `List[int]`
  produces `Type 'List[int32]' has no member 'no_such_method'` (semantic diagnostic), not
  SPY0908. Pinned by `TestFixtures/generics/bcl_generic_method_not_found_1136.error`.

Members remain individually tracked as issues; the conformance sweeps force that regardless.
