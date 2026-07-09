# Strategic Review — Issue Classes, Feature Gating, Architecture, and the Roslyn Question

> **Date:** 2026-07-09
> **Scope:** Open issue backlog (32 issues), ~200 recently closed issues, cross-language
> benchmark results (2026-07-06), compiler benchmark baselines, and the compilation
> pipeline (`AssemblyCompiler`, `RoslynEmitter`, `Discovery/`).
> **Companion:** [Roadmap 2026-07](../design/roadmap-2026-07.md) breaks these
> recommendations into phased tasks.

This review answers four questions:

1. Which classes of bugs would be better solved by an overarching mechanism or process
   than by continued point fixes?
2. Which backlog items are new features that should sit behind toggles?
3. Where are the simplification, maintainability, and observability opportunities?
4. Given the benchmark results, where (if anywhere) does "handroll our own compiler,
   bypass Roslyn" belong on the roadmap — and what should we keep borrowing from Roslyn?

---

## 1. Issue classes that want an overarching mechanism

Reading the last ~200 closed issues, five clusters account for the large majority of
bugs. Each cluster is a symptom of a missing mechanism, not a run of unlucky one-offs.

### 1.1 CLR interop gaps (~40% of closed bugs)

**Evidence:** #880–#891, #912–#922, #940–#959, #965–#977, #824–#833.

All variations of one theme: some CLR shape — arrays, `byte[]`, `object?[]`, nested
types, internal constructors, generic interfaces, snake_case parameters, module-qualified
types — isn't bindable/indexable/callable/matchable from `.spy`. Each was discovered by a
painful dogfood session and fixed case by case.

**Mechanism: a generated interop conformance suite.** Walk every public member of every
stdlib assembly via reflection, generate a `.spy` snippet exercising it (call it, index
it, use it in a type annotation, pattern-match it, subclass it where applicable), and
compile the lot in CI. This turns "gap found by dogfooding module N" into "gap found by
CI the day the bridge regresses." The `/gap-analysis` skill is the seed of this; it
should become exhaustive and machine-generated rather than curated.

### 1.2 Semantic-clean-but-invalid-C# (CS-error leaks)

**Evidence:** #980, #917, #912, #902, #900, #873, #867, #866, #960, #961, #949, #921,
#846, #816.

The type checker approves the program; Roslyn rejects the emitted C# with a CS error.

**Mechanism: an enforced invariant, not more fixtures.** Any program that passes
semantic analysis must produce C# that Roslyn accepts. Enforce three ways:

- A property test that fuzzes semantic-clean programs and asserts zero CS diagnostics.
- A last-chance handler that converts any escaped CS error into a single SPY
  internal-error code carrying AST provenance, so users never see raw CS codes.
- Every historical leak becomes a permanent corpus entry.

### 1.3 Type narrowing as syntactic special cases

**Evidence:** #979, #978, #854, #817, #882, #848.

Every new syntactic form (`assert`, early return, `or`-conditions, `self._field`
access) is a fresh narrowing bug because narrowing pattern-matches on if-statement
shapes.

**Mechanism: dataflow over the CFG.** A control-flow graph already exists
(`Analysis/ControlFlow/`) and `ControlFlowValidator` proves the plumbing works.
Reimplementing narrowing as facts propagated over the CFG retires the entire class at
once instead of adding the next syntactic case.

### 1.4 Overload resolution order-sensitivity

**Evidence:** #975 (states it outright: "heuristic is order-sensitive"), plus #1002,
#1003, #965, #966, #954, #833, #828, #890, #810–#814.

**Mechanism: a formal specificity spec.** Write down the betterness rules (crib C#'s
better-function-member rules from the C# spec), make the implementation table-driven,
and add property tests asserting candidate-order independence and determinism.

### 1.5 Non-determinism and flakiness

**Evidence:** #1032 (diagnostics depend on source file order), #895 (static ID counter
ordering), #792 (`Console.SetOut` race), #1017, #1031, #1033, #630.

**Mechanism: a determinism harness.** Compile every multi-file fixture twice with
shuffled file order and diff diagnostics + emitted C# byte-for-byte — the same
contractual guarantee Roslyn makes with `/deterministic`. Ban static mutable state in
the compiler via an analyzer (warnings are already errors solution-wide, so an analyzer
is enforceable for free).

### 1.6 Endorsement: the CPython golden oracle (#1030)

#1030 (port a curated CPython test suite as a golden oracle for Pythonic behavior) is
exactly this kind of overarching mechanism, aimed at the behavioral-parity bug class
(#896, #905, #906). It ranks among the highest-leverage open issues.

### 1.7 Parser ambiguity hotspots

**Evidence:** #1015, #1011, #899, #888, #872, #847, #870, #1001 — lambdas, subscripts,
and tuples in argument position are a recurring ambiguity zone.

**Mechanism:** the unparser round-trip property test already exists; extend it with
grammar-directed fuzzing focused on the known hotspots, and differential-parse the
shared subset against CPython's `ast` module.

---

## 2. What belongs behind toggles

The open backlog is now dominated by speculative language features — #1020 (nominal
aliases), #1021 (refinement types), #1023 (`defer`), #1026 (structured parallelism),
#1027 (placement decorators), #1028 (units of measure), #1029 (`as?`/`as!`), #992 (free
unions), #993 (lazy imports), #989 (`@` matmul), #995–#997 (typing PEPs), #640 (const
eval), #637 (macros), #416 (property observers). Five are literally titled "Evaluate…".

The missing infrastructure is a **language feature gate system**, not any individual
feature:

- A `FeatureFlags` record threaded through `CompilerServices`, settable via
  `--enable-feature=<name>` on the CLI and a `[features]` section in `.spyproj`. A
  Pythonic `from __future__ import defer` spelling would fit the language's character.
- The parser **always** recognizes gated syntax, so the error is "requires experimental
  feature `defer`" rather than a confusing SPY0104. Semantic analysis and codegen gate
  on the flag.
- Test fixtures declare required features, so gated features get full coverage before
  graduating.
- An explicit lifecycle: features either graduate to stable or get deleted cheaply.
  Merging an experiment no longer implies committing to it.

**Good gate candidates** (purely additive syntax): `defer` (#1023), `@` matmul (#989),
`as?`/`as!` (#1029), free unions (#992), refinement types (#1021), units of measure
(#1028), lazy imports (#993), nominal aliases (#1020), placement decorators (#1027),
structured parallelism (#1026).

**Caveats:**

- #1029 *replaces* the `to` operator, so it needs a transition story: gate plus
  `TransitionWarningValidator`, which exists for exactly this purpose.
- #1025 (persistent collections) doesn't need a gate at all — it's a stdlib module;
  importable or not is already the toggle.

---

## 3. Simplification, maintainability, and observability

### 3.1 Unify run mode and project mode

Bugs like #940 (`os.Sep` works in one mode, `os.sep` in the other), #814, and #862
exist only because there are two compilation paths that drift. Make `run` a degenerate
project-of-one-file so there is one pipeline to test.

### 3.2 Make the emitter a dumb translator

#973 (type-inference logic duplicated between `TypeInferenceService` and
`RoslynEmitter`), #974 (reflection calls inside the emitter), and #972 (`IsNdArrayType`
hardcoded in codegen) all point the same way: the emitter still *reasons about types*.
Every decision it makes should instead be materialized into `SemanticInfo` /
`CodeGenInfo` during semantic analysis — the materialization-points architecture already
exists for this. This is the single highest-leverage architectural simplification, and
(see §4) it is also the precondition for ever swapping backends.

### 3.3 One bidirectional name-mapping authority

The mangling bug family (#942, #863, #861, #820, #823, #891, #897, #898) suggests
casing/mangling logic is applied at multiple sites. `NameMangler` should be the single
authority, bidirectional, with exhaustive round-trip property tests
(`mangle(demangle(x)) == x` and vice versa).

### 3.4 Consolidate the type bridge

`ClrTypeMapper` (CLR → `SemanticType`), `TypeSyntaxMapper` (`SemanticType` → C#), and
`OverloadIndex` are three views of one boundary — the boundary where ~40% of closed
bugs live (§1.1). Making them one `Discovery`/bridge component with the conformance
suite attached gives that boundary an owner.

### 3.5 Observability

`CompilationMetrics` and `StructuredLogger` exist, but nobody can currently say what
fraction of a 2-second compile is startup vs. discovery vs. frontend vs. Roslyn Emit
(§4 shows why this matters). Opportunities:

- Surface per-phase wall time and allocations in `--verbose` output and in the
  benchmark JSON, so "compilation is slow" decomposes into actionable parts.
- **Diagnostic provenance:** record which pass/validator emitted each diagnostic. It
  would have shortened #1013-style "right rejection, wrong diagnostic" investigations
  and is nearly free given the `ValidationPipeline` structure.
- An internal-error policy for SPY0907-class crashes (#912): assert-heavy semantic
  phase with a crash reporter that dumps a minimal repro.
- #638 (post-hoc code analysis tooling) fits naturally in this bucket.

---

## 4. Benchmarks, Roslyn, and when (not) to handroll a compiler

### 4.1 The numbers don't say what they appear to say

From the 2026-07-06 cross-language run:

| Benchmark | Python | Sharpy | C# | Spy/Py | Spy/C# |
|-----------|--------|--------|-----|--------|--------|
| fibonacci | 206ms | 39ms | 30ms | 0.19x | 1.31x |
| list_comprehensions | 60ms | 92ms | 77ms | 1.54x | 1.19x |
| matrix_multiply | 787ms | 158ms | 40ms | 0.20x | 4.00x |
| sorting | 181ms | 279ms | 180ms | 1.54x | 1.55x |
| string_ops | 168ms | 201ms | 142ms | 1.20x | 1.42x |

Compilation: Sharpy 1.77–2.25s vs. C# ~1.15s per benchmark.

**Compilation "slowness" is cold-start, not Roslyn.** The benchmark times
`dotnet sharpyc.dll compile file.spy` as one process invocation: .NET host startup,
JIT'ing the compiler *and* Roslyn, reflection-based stdlib discovery, then the actual
frontend — which BASELINE.md puts at ~15–30ms per small file. The compile path is
already in-process Roslyn (`AssemblyCompiler` does `CSharpCompilation.Create` + `Emit`;
it does not shell out to `dotnet build`). The 1.77–2.25s is ~95% amortizable overhead.
A handrolled backend would not move this number.

**Execution slowness is the runtime library and lowering shape, not codegen quality.**
Roslyn + RyuJIT compile whatever C# is emitted exactly as well as hand-written C#; a
custom backend would generate *worse* code for years. The specific losses are
diagnosable:

- Losing to *Python* on list_comprehensions (92 vs 60ms) and sorting (279 vs 181ms)
  points at `Sharpy.List` wrapper indirection, comparer delegates, and allocation in
  comprehension lowering.
- The 4× gap on matrix_multiply is the absence of flat 2D arrays (the deferred NdArray
  work, #955–#959) forcing bounds-checked nested `Sharpy.List` indexing.

These are fixed by optimizing `Sharpy.Core` (struct enumerators, devirtualization,
`Span<T>` fast paths) and by smarter lowering (preallocated loops for comprehensions,
raw arrays for numeric hot paths) — not by replacing the backend.

### 4.2 What to do instead, in order of leverage

1. **A compiler server** (the VBCSCompiler model — keep-alive process, warm JIT, cached
   `MetadataReference`s and `OverloadIndex`), and/or ReadyToRun / Native AOT for the
   CLI. This alone should take warm compiles well under 200ms.
2. **Kill any text round-trip.** Syntax trees are built with `SyntaxFactory`; if the
   compile path normalizes to text and reparses before `CSharpCompilation.Create`,
   that is likely the bulk of the "codegen is ~60% of compile time" figure in
   BASELINE.md. Feed trees straight to the compilation.
3. **Cache discovery across runs** — metadata references and the overload index
   (`Discovery/Caching/` already exists).
4. **Wire `--incremental` into the benchmark story**, so published numbers reflect the
   warm path users actually live in.

### 4.3 Where a Roslyn bypass belongs on the roadmap

**Behind a milestone worth scheduling instead: an explicit lowering IR.** The genuinely
useful intermediate step is an IR between semantic analysis and C# emission — a
Sharpy-level middle-end where const folding (#640), comprehension fusion, and
devirtualization decisions live. That is where the optimization headroom is, it forces
the "emitter stops reasoning" cleanup (§3.2), and it happens to be the exact seam a
future non-Roslyn backend (direct IL via `System.Reflection.Metadata.Ecma335.MetadataBuilder`)
would plug into. It buys optionality without betting on it.

Direct IL emission only earns its cost if, after the IR work plateaus, we still need:

- **(a)** semantics C# can't express efficiently (tagged-pointer big ints, custom
  metadata for refinement types / units of measure), or
- **(b)** sub-100ms cold compiles that even a warm compiler server can't hit, or
- **(c)** self-hosting as a goal in itself.

Realistically that is v1.0+ territory, plausibly never.

**One argument weighted heavily: Roslyn is currently the test oracle, not just the
backend.** The entire "invalid C# leak" bug class (§1.2) was *caught* because Roslyn
rejected bad output with a CS error. With handrolled IL emission, every one of those
bugs becomes a silent `InvalidProgramException` or memory corruption at runtime. Don't
give up that safety net until the emitter's defect rate says we can. Bypassing Roslyn
also forfeits `#line`-based debugging (#609), the analyzer ecosystem, and free .NET
interop verification.

### 4.4 Lessons to keep borrowing from Roslyn, continuously

- **Lazy, memoized semantic binding** — `SemanticModel` binds on demand; Sharpy
  type-checks eagerly. Matters most for LSP latency.
- **Red/green trees** for incremental reparse — the immutable record AST is the right
  foundation.
- **Object pooling** (`ObjectPool`, `ArrayBuilder`) and allocation-regression gates in
  benchmarks.
- **`/deterministic` as a contractual guarantee** — directly retires the #1032 bug
  class.
- **The incremental-generator pull-based memoization model** for the `.sharpy-symbols`
  cache.
- **`IOperation`** as the design precedent for the lowering IR.
- **The compiler-server architecture** (VBCSCompiler keep-alive).
- **Diagnostics as immutable descriptors** paired with code-fix providers (LSP quick
  fixes).

### 4.5 Suggested sequencing

- **Near term (v0.2.x):** per-phase perf observability, compiler server, `Sharpy.Core`
  hot-path optimization, determinism harness, interop conformance suite.
- **Mid term (v0.3):** lowering IR + first optimization passes, NdArray/2D arrays,
  comprehension lowering rework, feature-gate infrastructure to drain the "Evaluate"
  backlog.
- **Long term (v0.4+):** revisit direct IL only against measured targets the IR-era
  compiler fails to meet.

See the [Roadmap 2026-07](../design/roadmap-2026-07.md) for the task-level breakdown.
