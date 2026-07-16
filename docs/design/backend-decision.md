# The E4 Backend Decision — Direct IL Emission vs Roslyn

> **Status:** Decision — 2026-07-16 (E4)
> **Issue:** [#1058](https://github.com/antonsynd/sharpy/issues/1058) (Workstream E, Phase 3).
> **Depends on:** E1 lowering-IR design ([#1055](https://github.com/antonsynd/sharpy/issues/1055)),
> E2 emitter port ([#1056](https://github.com/antonsynd/sharpy/issues/1056)), E3 optimization passes
> ([#1057](https://github.com/antonsynd/sharpy/issues/1057)) — all landed.
> **Roadmap:** [roadmap-2026-07.md](roadmap-2026-07.md) (Workstream E, §4.4).
> **Measurements:** [benchmarks/BASELINE.md](../../benchmarks/BASELINE.md) (D2 server compile, D3 round-trip,
> E3 pass deltas) and [benchmarks/cross-language/results/latest.md](../../benchmarks/cross-language/results/latest.md).
> This page is the **written go/no-go** the E4 done-when requires ("a written go/no-go decision with
> measurements attached"). It is a decision, not code; it is reviewed before merge, per the E1 precedent.

## Decision

**No-go.** Sharpy stays on the Roslyn backend. Direct IL emission (`System.Reflection.Metadata.MetadataBuilder`)
is **evaluated and declined** — not deferred pending more work, declined on the measured merits — with the
explicit revisit triggers listed at the end. This is the roadmap's default expectation, and the E3 plateau
plus the three go-criteria evaluation below confirm it rather than merely assume it.

The seam this decision is *about* already exists and stays: the E1 design ([lowering-ir.md §3](lowering-ir.md#3-boundary--the-backend-seam))
names the `IR → backend` boundary as the E4 seam, with `RoslynEmitter` as the first and default backend and a
hypothetical `MetadataBuilder` backend as a *second implementation of the same interface*. Declining E4 keeps
the optionality (the seam is real and measured) without building the second backend.

## What E4 asks

Per [#1058](https://github.com/antonsynd/sharpy/issues/1058), a direct-IL backend is worth building **only if**
one of three criteria holds; otherwise Sharpy stays on Roslyn because Roslyn is the test oracle, the `#line`
debugging path, and free .NET interop verification. E4 is gated on **E3 having plateaued** — which it has.

## The measured picture (post-E3)

**E3 plateaued.** The three shipped IR optimization passes (`opt_const_fold`, `opt_comprehension_fusion`,
`opt_stack_collections`) are correct and reduce work where they fire, but **emit byte-identical C# on all six
cross-language benchmarks** under every flag combination (verified by `emit csharp` diff — see BASELINE.md's
"E3 IR Optimization Pass Deltas"). The one pass a custom backend might have justified — devirtualization — was
**retired without shipping**: `Sharpy.List`/`Dict`/`Set` are `sealed`, so RyuJIT already devirtualizes every
concrete-receiver call and the emitter already emits direct calls. There is **no JIT-independent optimization
headroom** for a backend to capture; the plateau is the precondition E4 required, and it shows the plateau is
real, not a scoping artifact.

**Execution — Sharpy is already competitive** (runtime only; Spy/Py < 1.0 means Sharpy beats CPython):

| Benchmark | Spy/Py | Spy/C# | note |
|-----------|-------:|-------:|------|
| fibonacci | 0.33x | 0.97x | recursion/loops; at hand-written-C# parity |
| list_comprehensions | 0.99x | 0.70x | faster than the hand-written C# baseline |
| matrix_multiply | 0.27x | 2.27x | Spy/C# gap is structural (nested bounds-checked `Sharpy.List` indexing), not a codegen-quality gap a backend fixes — see D5 (#1052) |
| sorting | 0.89x | 0.64x | keyless-sort fast path (D4) |
| string_ops | 1.16x | 1.07x | UTF-16 string methods |

The sixth cross-language benchmark, `matrix_multiply_numpy`, has no Sharpy row: its Sharpy execution fails
(pre-existing, predates this workstream — [#1084](https://github.com/antonsynd/sharpy/issues/1084), standalone
runs of numpy programs missing the transitive MathNet.Numerics dependency in `deps.json`), so the harness
records no result. Its *emit* is included in the byte-identical E3 diff above; only execution is excluded.

The one large Spy/C# ratio (`matrix_multiply` 2.27x) is a **data-structure** cost (no flat 2D array; every
`a[i][k]` is a bounds-checked `Sharpy.List` indexer), diagnosed on #1052 as structural and unclosable by
index-normalization. A direct IL backend emits the *same* indexer calls — it does not change the data structure,
so it does not close this gap. The gap is a runtime-library question, not a backend question.

**Compilation — the sub-100 ms target is already met on the path that matters:**

| Path | Cost | What it is |
|------|-----:|------------|
| cold (single-file, no cache) | ~750–912 ms | dominated ~70–80% by **IL Emission** (per-process `MetadataReference` reload over the full trusted-platform assembly set + Roslyn emit), *not* by the syntax mapping a direct backend would replace (D3, #1050) |
| warm (`--incremental`, symbol cache present) | ~667–773 ms | same IL-emission floor; skips re-parse/re-check of unchanged files |
| **server-warm** (`sharpyc build --server`) | **~94–109 ms end-to-end, ~9 ms server-side** | reuses the process-lifetime `MetadataReference`/overload-index caches; the ~95 ms residual is **client process launch**, not compilation (D2, #1049) |

The D3 round-trip work already removed the redundant reparse (2 parses → 1, folded into codegen; C# Parsing
collapsed to ≈ 0.01 ms). What remains of cold-compile cost is **assembly-metadata load and IL emission/JIT** —
work a `MetadataBuilder` backend still has to do, and which the compiler server already amortizes to ~9 ms
server-side. A direct IL backend would not move the cold number materially, because the cold cost is not in the
Roslyn syntax→IL translation it would replace.

## The three go-criteria, evaluated

**(a) Semantics C# can't express efficiently — NOT met.** The candidates are tagged-pointer big integers and
custom metadata for refinement types (#1021) / units of measure (#1028). Sharpy `int` is fixed-width
(`int32`/`int64`) by Axiom 1; there is no arbitrary-precision-int-by-default on the roadmap, so no tagged-pointer
representation is needed. Refinement types and units are **demand-gated** speculative features (strategic review
§2 backlog; behind feature flags if built at all), and their leading designs lower to *checks and wrapper
structs* expressible in C# — none is a **committed** feature that requires metadata C# attributes cannot carry.
No semantics currently on the roadmap is inexpressible or inefficient in emitted C#.

**(b) Sub-100 ms cold compiles otherwise unreachable — NOT a driver.** Sub-100 ms is **already achieved** end-to-end
on the `--server` path (~94–109 ms, ~9 ms server-side). More to the point, the cold-compile cost that remains is
**IL emission + metadata load + JIT** (D3), which a direct backend must also pay; the Roslyn syntax-mapping step
a `MetadataBuilder` backend would eliminate is a small, already-optimized fraction (C# Parsing ≈ 0.01 ms post-D3).
There is no cold-start SLA on record that the compiler server cannot meet, and no profile showing Roslyn's
syntax→IL mapping as the bottleneck. The criterion is not met, and would not be *addressed* by a direct backend
even if it were.

**(c) Self-hosting — NOT a goal.** Compiling the Sharpy compiler in Sharpy is not a stated project goal (roadmap
or strategic review). Nothing depends on it. Not met.

None of the three go-criteria holds. The evaluation therefore lands on the roadmap's default: **stay on Roslyn.**

## What staying on Roslyn buys (the no-go rationale)

Roslyn is not a neutral translation layer whose removal is free; it provides three things a direct IL backend
would have to **rebuild from scratch**, each carrying correctness or tooling weight the E3-plateau benefit does
not justify sacrificing.

**1. Roslyn is the test oracle for an entire bug class.** The "semantic-clean-but-invalid-C#" class
([strategic review §1.2](../audits/strategic-review-2026-07-09.md)) — the type checker approves a program but
the emitted C# is invalid — was caught **only because Roslyn rejects bad output with a CS error**. That class has
14 recorded instances (#980, #917, #912, #902, #900, #873, #867, #866, #960, #961, #949, #921, #846, #816). A
`MetadataBuilder` backend emits IL directly with **no equivalent free oracle**: `ILVerify`/PEVerify check IL
well-formedness, not the semantic-shape errors (wrong overload, bad conversion, missing member) Roslyn surfaces.
Losing the oracle means this whole class of bugs would ship silently unless a comparably cheap IL-level oracle
were built first — a large, open-ended cost that dwarfs any E3-plateau gain. The B-workstream response to this
class (an enforced "semantic-clean ⇒ Roslyn-accepts" invariant + fuzz property test) is *built on Roslyn being
the backend*; a direct backend removes the very check that retires the class.

**2. Roslyn is the `#line` debugging path.** The emitter emits `#line` directives mapping generated C# back to
`.spy` source, so a debugger steps through Sharpy source. Real-debugger validation of that path is still open
([#609](https://github.com/antonsynd/sharpy/issues/609)), but the **path exists for free** — Roslyn threads the
directives into the PDB. A direct IL backend would have to hand-build PDB sequence points and local-scope records
to reach the same debugging experience, reimplementing what Roslyn gives at no cost.

**3. Roslyn is free .NET interop verification.** Because emission runs through `CSharpCompilation` against real
`MetadataReference`s (the BCL + the whole stdlib), every interop call — method resolution, generic instantiation,
overload binding against actual .NET metadata — is **type-checked at compile time**. A direct IL backend emits
`call`/`callvirt` tokens without that verification, moving interop-mismatch failures from compile-time CS errors
to runtime `MissingMethodException`s or silent misbinds.

The net: E4's upside (a second backend) captures **no measured performance headroom** (E3 plateaued; devirt has
none; the one large Spy/C# gap is structural, not codegen), while its downside removes the oracle, the debugging
path, and the interop check — three things the project actively relies on. That asymmetry is the decision.

## Revisit triggers

This no-go is re-opened if any of these becomes true — each maps to a go-criterion that would then hold:

1. **A *committed* feature requires custom metadata C# cannot express.** If refinement types (#1021), units of
   measure (#1028), or a similar feature graduates from demand-gated speculation to a committed feature *and* its
   chosen representation needs IL/metadata beyond what C# attributes and wrapper structs can carry (criterion a).
2. **A cold-start SLA appears that the compiler server cannot meet, with the bottleneck proven to be Roslyn's
   syntax→IL mapping** — not metadata load / IL emission / JIT, which a direct backend also pays (criterion b).
   Today the server path clears 100 ms and the residual is client launch, addressable by R2R/AOT of the client
   (BASELINE.md D2) long before a backend rewrite.
3. **Self-hosting becomes a stated goal** (criterion c).
4. **An IL-level correctness oracle of comparable cost to Roslyn's** materializes, removing the largest cost of
   leaving Roslyn (the §1.2 bug-class regression). Absent this, triggers 1–3 alone would still weigh the oracle
   loss against the benefit.

Until one of these holds, the `IR → ICodeGenBackend` seam stays a single-backend interface: real, measured, and
uncommitted to a second implementation.
