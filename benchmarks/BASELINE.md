# Sharpy Compiler Benchmark Baselines

> **Last Updated:** 2026-02-02
> **Commit:** `dev` branch
> **Machine:** (Update with your machine specs when running)
> **Runtime:** .NET 10.0

## D4 Sharpy.Core Hot-Path Results (#1051)

> **Recorded:** 2026-07-12
> **Machine:** Apple M4 Max (14 cores), macOS 26.5.2
> **Runtime:** .NET 10.0.301 · **Python:** 3.12.13
> **Harness:** `benchmarks/cross-language/run_benchmarks.py` (median of 3 timed runs + warmup, ×3 outer repetitions per condition, median of medians)
> **Before:** `bdb4c175b` (pre-D4 baseline) · **After:** `e8e2fc556` (D4 tasks P6.1–P6.4 landed)

The D4 perf tasks — keyless `list.sort()` fast path (P6.1, `f5af6bde9`), struct
enumerators + direct backing access (P6.2, `9ee20e90f`), presized concat/append
(P6.3, `afa2a34cc`), and imperative preallocated comprehension lowering (P6.4,
`ba0d68836`) — target the collection hot paths exercised by the `sorting` and
`list_comprehensions` cross-language benchmarks. Execution-time Spy/Py ratios
(runtime only; **< 1.0 means Sharpy beats CPython**):

| Benchmark | Spy/Py before | Spy/Py after | Δ | Driver |
|-----------|--------------:|-------------:|---|--------|
| fibonacci | 0.32x | 0.33x | ≈flat | not a D4 target (recursion/loops) |
| matrix_multiply | 0.29x | 0.30x | ≈flat | not a D4 target (index loops) |
| sorting | 2.35x | **0.91x** | −61% | keyless sort (P6.1) 225ms→87ms |
| list_comprehensions | 1.49x | **1.05x** | −30% | imperative comprehensions (P6.4) 61ms→43ms |
| string_ops | 1.35x | 1.35x | flat | not a D4 target (string methods) |

**Gate status:** `sorting` crossed from 2.35x to a solid 0.91x (0.91 on all three
outer runs), and `list_comprehensions` improved from 1.49x to ~1.05x (one run hit
1.00x). Three of five benchmarks (`fibonacci`, `matrix_multiply`, `sorting`) are
below 1.0. `list_comprehensions` (~1.05x) and `string_ops` (~1.35x) remain above
1.0; neither residual is closable by D4's collection-focused changes. See the
residual analysis on issue #1051.

## D5 Numeric-Path Results (#1052)

> **Recorded:** 2026-07-12 · Apple M4 Max (14 cores), macOS 26.5.2 · .NET 10.0.301 · Python 3.12.13
> **Harness:** `benchmarks/cross-language/run_benchmarks.py` (median of 3 timed runs + warmup, ×3 outer)

The `matrix_multiply_numpy` variant contrasts the numeric hot path against the
pure-list `matrix_multiply` kernel. numpy's `@` (256×256, 200 products) runs the
same work through native BLAS-class code:

| Path | Python | Sharpy | C# | notes |
|------|-------:|-------:|----|-------|
| numpy `@` (256×256 ×200) | 169ms | (held out, #1084) | 375ms (MathNet) | native BLAS; Sharpy `np.Matmul` delegates to the same MathNet backend |
| pure-list (100×100 ×10) | — | Spy/C# 2.35× (pre-P6.7) → **2.27×** (post-P6.7) | — | bounds-checked nested `Sharpy.List` indexing |

Sharpy's numpy column is held out of the harness until **#1084** (compiled numpy
programs can't load MathNet at runtime — transitive NuGet deps copied but omitted
from `deps.json`); `bench.spy` still compiles under the `matmul` flag. The emitted
C# is `np.Matmul(a, b)` → `Sharpy.Numpy.Matmul` → `NumpyLinalg.Dot` → MathNet, so
Sharpy-numpy throughput tracks the C#/MathNet column modulo one marshal per call.

**P6.7 tag-eligibility note:** the pure-list ratio is essentially flat across P6.7
because the benchmark's O(n³) inner loop uses `while`-counters (`i = i + 1`, i.e.
reassigned), which the non-negative-index fast path does not tag — it tags integer
literals and unreassigned `range()` induction variables. Emitted C# confirms this:
the inner `result[i][j] = ... a[i][k] * b[k][j]` stays a plain (Normalize-ed) indexer
post-P6.7, while only the O(1) setup/print indices (`a[0]`, `result[0][0]`) lower to
`GetItemUnchecked`. P6.7 works where eligible (a `for i in range(n): xs[i]` probe
lowers to `xs.GetItemUnchecked(i)`); this kernel just isn't eligible on its hot path.

**Decision (D5, #1052):** even with the fast path fully engaged the gap would remain
≥ 1.5× — it is structural (nested bounds-checked `Sharpy.List` indexing / no flat 2D
array), not index-normalization cost. The numeric fast path already exists via numpy →
MathNet, ~50–100× faster than a same-scale pure-list kernel. Raw-array lowering is
therefore dispositioned to Workstream E3 rather than built now; see the escalation
memo on #1052.

## D3 Compile Round-Trip Breakdown (#1050)

> **Recorded (pre-D3):** 2026-07-14
> **Machine:** Apple M4 Max (14 cores), macOS 26.5.2 · .NET 10.0.301 · Python 3.12.13
> **Before commit:** `498ea38a4` (built in an isolated `git worktree` pinned at HEAD)
> **Measurement:** `sharpyc compile <file> --metrics-format json`, median of 8 timed runs
> (1 warmup discarded) per file, per-phase durations from `CompilationMetrics.FormatAsJson`.
> **Conditions:** measured under heavy parallel-agent load (load avg ≈ 36, ~19 concurrent
> `dotnet` processes). Absolute wall times are inflated by contention and are **not**
> comparable across machines; the **per-phase proportions** below are the load-robust
> before/after signal (see the note in Design Decision 8 of plan-136a52).

**What D3 changes.** The emitter builds a normalized Roslyn `SyntaxTree`, flattens it to
text with `ToFullString()`, and today that text is **reparsed** in `AssemblyCompiler` (the
timed **"C# Parsing"** phase) before `CSharpCompilation.Create`. A *second*, untimed reparse
of the same text runs in the always-on post-codegen invariant (`AssertGeneratedCSharpParses`).
So every hot-path file was parsed to a tree, thrown away, and reparsed **twice**. D3 as landed
parses the emitted text **once, at codegen time**, hands that parsed tree straight to
`CSharpCompilation`, and reads the post-codegen invariant's diagnostics off the same tree —
eliminating the separate "C# Parsing" phase and the validation reparse (2 parses → 1, the one
parse folded into Code Generation). Handing the emitter's own node graph (zero parses) is
**blocked on #1095**: some emitter nodes are not reparse-equivalent (string-built `global::`
names inside identifier tokens), which broke stdlib-wide compilation when attempted.
`ToFullString()` stays (snapshots, incremental cache, `emit csharp`, LSP); cache-served
string-only files still `ParseText` — that is the incremental path's cost, not the hot path's.

**Pre-D3 per-phase medians (the reparse is "C# Parsing"):**

| Input | Type Checking | Code Generation | **C# Parsing** | Roslyn Compilation | IL Emission | Total | C# Parsing % total |
|-------|--------------:|----------------:|---------------:|-------------------:|------------:|------:|-------------------:|
| `large_lexer_corpus.spy` (476 ln) | — | 137.8 ms | **3.30 ms** | 6.9 ms | 654.7 ms | 925.7 ms | 0.36% |
| `large_functions.spy` (73 ln) | — | 129.9 ms | **1.02 ms** | 10.0 ms | 765.9 ms | 1002.7 ms | 0.10% |
| `classes.spy` (35 ln) | — | 129.2 ms | **0.58 ms** | 10.1 ms | 683.8 ms | 994.1 ms | 0.06% |
| `sorting/bench.spy` | — | 94.3 ms | **0.40 ms** | 7.1 ms | 589.7 ms | 775.0 ms | 0.05% |
| `fibonacci/bench.spy` | — | 75.0 ms | **0.25 ms** | 6.8 ms | 453.4 ms | 601.2 ms | 0.04% |
| synthetic 6000 ln (400 fn + 400 cls) | 307.3 ms | 1574.3 ms | **110.4 ms** | 13.4 ms | 2723.7 ms | 6722.9 ms | 1.64% |

**Reading the split.** "C# Parsing" scales with generated-code size: **0.04–0.36%** of total
for small corpus files, rising to **1.64%** of total (**6.55%** of the emit-related work,
`Code Generation + C# Parsing`) for the 6000-line synthetic file. The always-on validation
reparse is a second, equal-cost parse of the same text, so the redundant-reparse overhead D3
removes is **≈ 2× the "C# Parsing" phase**. The round trip is real but narrow: for typical
single files it is fractions of a percent; it becomes material for large files and
whole-project (multi-file) builds where "C# Parsing" aggregates across every unit. The
`Total` here is dominated by **IL Emission** (~70–80%), which folds in per-process
`MetadataReference` (re)load over the full trusted-platform assembly set and the Roslyn emit —
that cost is the target of **D2 (#1049)**, not D3.

**Post-D3 per-phase medians** (recorded 2026-07-15, quiet machine — no concurrent agent
builds/tests; Release CLI at `2ba958fb4`; median of 8 runs, 1 warmup dropped):

| Input | Code Generation | **C# Parsing** | Roslyn Compilation | IL Emission | Total | C# Parsing % total |
|-------|----------------:|---------------:|-------------------:|------------:|------:|-------------------:|
| `large_lexer_corpus.spy` (476 ln) | 136.0 ms | **0.009 ms** | 5.8 ms | 506.0 ms | 736.7 ms | 0.00% |
| `large_functions.spy` (73 ln) | 104.6 ms | **0.009 ms** | 5.8 ms | 472.9 ms | 658.9 ms | 0.00% |
| `classes.spy` (35 ln) | 106.4 ms | **0.011 ms** | 6.3 ms | 419.2 ms | 611.6 ms | 0.00% |
| `comprehensions.spy` | 95.1 ms | **0.009 ms** | 6.4 ms | 489.8 ms | 668.1 ms | 0.00% |
| `sorting/bench.spy` | 106.6 ms | **0.013 ms** | 6.2 ms | 520.0 ms | 708.0 ms | 0.00% |
| `list_comprehensions/bench.spy` | 95.2 ms | **0.010 ms** | 6.6 ms | 477.3 ms | 659.0 ms | 0.00% |
| `fibonacci/bench.spy` | 84.7 ms | **0.010 ms** | 6.6 ms | 442.8 ms | 611.9 ms | 0.00% |

**Measured emit-phase drop (the #1050 done-when).** The "C# Parsing" phase collapsed from
0.25–3.30 ms (0.04–0.36% of total; up to 1.64% on the 6000-line synthetic) to **≈ 0.01 ms
(0.00%)** — only prebuilt trees flow through it on the hot path. The single remaining parse
now happens inside Code Generation, whose medians are **at or below** their pre-D3 values on
every corpus file (e.g. `large_lexer_corpus` 136.0 vs 137.8 ms), i.e. absorbing the parse cost
is within noise. The untimed always-on validation reparse (an equal second parse of every
generated file) is gone as well, replaced by `GetDiagnostics()` on the existing tree. Net:
**two text parses per hot-path file → one**, with the residual zero-parse handoff tracked in
#1095.

## D2 Persistent-Server Compile (#1049)

> **Harness:** `benchmarks/cross-language/run_benchmarks.py` (`Server` column) plus a direct
> `sharpyc build --server` micro-measurement (median of 11 warm runs after warmup).
> **Machine:** Apple Silicon macOS, .NET 10, Release CLI. **Measured under peer build/test load**
> (three agents sharing the machine) — treat the absolute cold figure as an upper bound and
> re-measure at a quiet window for the canonical record; the warm-vs-cold gap and the gate verdict
> are robust to the load (every warm run was < 120 ms).

**What landed.** Two process-lifetime caches remove the per-compile cold costs the D3 breakdown
attributed to D2, and a keep-alive `sharpyc server` reuses one process across compiles:

1. **`AssemblyCompiler.s_referenceCache`** — `MetadataReference`s keyed by (assembly path,
   last-write time), so the trusted-platform-assembly set is read and parsed from disk at most once
   per process instead of on every compile.
2. **`OverloadIndexCache.s_inMemoryIndices`** — the deserialized stdlib overload index kept per
   process (keyed by the content-addressed cache-file path), so a fresh `CachedModuleDiscovery` per
   compile no longer re-reads every index from disk.
3. **`sharpyc server`** — an explicitly-started keep-alive process on a named pipe; clients opt in
   with `sharpyc build|run --server[=NAME]` and fall back to an in-process compile if none is
   running (no daemon auto-spawn).

**Measured warm compile of a small file** (`def main(): print("hi")`):

| Path | Median | Range | Notes |
|------|-------:|------:|-------|
| Cold in-process `sharpyc build` | ~790 ms | — | fresh process: JIT + reference load + index deserialize + emit (inflated by peer load) |
| **End-to-end `sharpyc build --server` (warm)** | **105 ms** | 97–120 ms | client process launch + pipe round-trip + warm server-side compile |
| Server-side compile only (server-reported) | ~9 ms | 6–23 ms | the actual compilation once caches + JIT are warm |

**Gate verdict — PASS.** The exit criterion (warm compile of a small file **< 200 ms**) is met with
margin: end-to-end **105 ms** (every sample < 120 ms), of which the compilation itself is **~9 ms**;
the remaining ~95 ms is the client `dotnet sharpyc.dll` process launch, not compilation.

**Residual analysis.** With the server-side compile already ~9 ms, the dominant remaining cost on the
`--server` path is **client process startup** (~95 ms of the 105 ms). If a stricter target is ever
set, the next levers are, in order: (a) ReadyToRun/AOT-publishing the CLI *client* to cut its launch
time (the `Sharpy.Cli.csproj` is a plain framework-dependent Exe today — no R2R/AOT configured),
or (b) a resident client / editor-embedded client that skips a per-compile process launch entirely.
Neither is needed to clear the 200 ms bar, so R2R/AOT is **recorded as evaluated and deferred**, not
adopted, per Design Decision 9(c).

## E3 IR Optimization Pass Deltas (#1057)

> **Recorded:** 2026-07-16
> **Machine:** Apple M4 Max (14 cores), macOS 26.5.2 · .NET 10.0.301 · Python 3.12.13
> **Passes:** `opt_const_fold` (#640), `opt_comprehension_fusion`, `opt_stack_collections` — three
> CodeGen-scoped behavioral flags, each default-off, each a pure IR→IR rewrite in
> `IrPassManager.Default`. A fourth candidate, `opt_devirt`, was evaluated and **retired** (below).
> **Method:** per-benchmark `emit csharp` diff (flags-off baseline vs each flag and all-on) is the
> primary evidence. A flag that produces byte-identical C# produces identical IL, so its runtime delta
> is **zero by construction** — not "within noise". A wall-time matrix is only informative where the
> emit changes.

**Matrix result — no pass fires on the cross-language suite.** Emitting each benchmark with all three
flags on and diffing against the flags-off baseline yields **byte-identical C#** in every case:

| Benchmark | opt_const_fold | opt_comprehension_fusion | opt_stack_collections | all-on vs off |
|-----------|:--------------:|:------------------------:|:---------------------:|:-------------:|
| fibonacci | identical | identical | identical | **identical** |
| list_comprehensions | identical | identical | identical | **identical** |
| matrix_multiply | identical | identical | identical | **identical** |
| sorting | identical | identical | identical | **identical** |
| string_ops | identical | identical | identical | **identical** |
| matrix_multiply_numpy | identical | identical | identical | **identical** |

Because the emitted IL is unchanged, the pre-E3 execution baseline (`results/latest.md`) stands
unchanged as the post-E3 baseline **for every flag combination**; a wall-time matrix would measure only
scheduler noise around it and is deliberately omitted (it would misrepresent noise as signal).

**Why each pass is inert on these programs** (a reach limit, not a defect):

| Pass | Why it does not fire here |
|------|---------------------------|
| `opt_const_fold` | The benchmarks contain no compile-time-constant *operations* — every arithmetic/comparison subexpression involves a runtime variable (`n - 1`, `x % 2`, `(i + j) % 7`). Bare literals (`30`, `range(1000)`) are already literals and are never re-emitted. Even where present, RyuJIT already folds constants. |
| `opt_comprehension_fusion` | The only multi-`for` comprehension (`[a + b for a in range(50) for b in range(50)]`) draws from `range(...)` **call** sources, which v1 excludes (effect-free variable/attribute sources only, so re-evaluating the source for its `Count` is safe). Single-`for` comprehensions are already presized by the initial lowering. |
| `opt_stack_collections` | No benchmark iterates a **list literal** directly (`for x in [..]`); the loops are `while` counters and comprehensions, so there is no non-escaping literal to stack. |

**The passes are correct and reduce work where they fire** (micro-demonstrations, `emit csharp` diff):

| Pass | Program | Flag off | Flag on | What it saves |
|------|---------|----------|---------|---------------|
| `opt_const_fold` | `x = 2 + 3 * 4` | `int x = 2 + 3 * 4;` | `int x = 14;` | two runtime IL arithmetic ops become one `ldc.i4` |
| `opt_comprehension_fusion` | `[x + y for x in xs for y in ys]` (list params) | `new Sharpy.List<int>()` (grown incrementally) | `new Sharpy.List<int>(((ISized)xs).Count * ((ISized)ys).Count)` | the reallocation/copy churn of growing the result list |
| `opt_stack_collections` | `for x in [1, 2, 3]` | `new Sharpy.List<int>() { 1, 2, 3 }` | `new int[] { 1, 2, 3 }` | **3 heap allocations → 1** (wrapper + backing `List<T>` + its array ⇒ one array) |

**Two findings for the E4 backend decision (#1058):**

- **Devirtualization has no headroom by construction.** `opt_devirt` was evaluated and **retired
  without shipping a pass** (Phase 8): `Sharpy.List<T>`, `Dict<K,V>`, `Set<T>` are all `sealed`, so
  RyuJIT already devirtualizes every call on a concrete receiver and the emitter already emits direct
  calls — a "mark for direct dispatch" pass would produce byte-identical output. The protocol sites
  (`len(x)` → `((ISized)x).Count`) are explicit-interface implementations that cannot be devirtualized.
  The one non-JIT-redundant rewrite (identity-key `sort` → keyless) fires on zero real code. A custom
  IL backend gains **no devirtualization headroom over RyuJIT** on a sealed-collection design.

- **The preallocation win is allocation churn, not wall time.** `opt_comprehension_fusion`'s
  product-of-counts presize — and D4's single-source presize before it — removes the incremental
  reallocation/copy of a growing result list: an **allocation-count and GC-pressure** reduction, not a
  measurable wall-time change on a benchmark whose result list is not the hot loop. The cross-language
  suite does not isolate allocation churn from wall time, so the effect is real but invisible in the
  execution-time table.

**Plateau assessment.** All three shipped E3 passes are correct, toggleable, and byte-transparent when
off, and each demonstrably reduces work on a program that exercises its pattern — yet **none fires on
the representative cross-language workloads**, and the one pass a custom backend might have justified
(devirtualization) has **no headroom against RyuJIT** because the collection types are sealed. The E3
optimization surface, as scoped, has **limited reach on real code and no JIT-independent win to
capture** — the central evidence Phase 10's go/no-go weighs.

## Benchmark Suite

The benchmark suite is located in `src/Sharpy.Compiler.Benchmarks/` and uses [BenchmarkDotNet](https://benchmarkdotnet.org/).

### Running Benchmarks

```bash
# Run all benchmarks (takes several minutes)
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release

# Run specific benchmark class
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release -- --filter "*CompilerBenchmarks*"
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release -- --filter "*LexerBenchmarks*"
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release -- --filter "*ParserBenchmarks*"

# Quick single benchmark (useful for sanity check)
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release -- --filter "*HelloWorld*" --job short
```

## Corpus Files

Located in `src/Sharpy.Compiler.Benchmarks/Corpus/`:

| File | Lines | Description |
|------|-------|-------------|
| `hello_world.spy` | 4 | Basic print function |
| `fibonacci.spy` | 22 | Recursive and iterative functions |
| `classes.spy` | 35 | Classes, inheritance, methods |
| `comprehensions.spy` | 26 | List/dict comprehensions |
| `large_functions.spy` | 73 | Prime checking, GCD, factorial |
| `large_lexer_corpus.spy` | 476 | Combined language features |

## Baseline Numbers

> **Note:** The BenchmarkDotNet tables in this section are illustrative order-of-magnitude
> figures for the `CompilerBenchmarks`/`LexerBenchmarks`/`ParserBenchmarks` micro-suite; run
> them on your machine to refresh. For **measured per-phase compile breakdowns** (median of N
> timed runs, per-phase from `CompilationMetrics.FormatAsJson`), see the D4/D5 and
> **D3 Compile Round-Trip Breakdown** sections above.

### Full Pipeline Benchmarks (CompilerBenchmarks)

| Benchmark | Mean | Allocated |
|-----------|------|-----------|
| Hello World (4 lines) | ~15 ms | ~10 MB |
| Fibonacci (22 lines) | ~20 ms | ~12 MB |
| Classes + Inheritance (35 lines) | ~25 ms | ~14 MB |
| Comprehensions (26 lines) | ~20 ms | ~12 MB |
| Large Functions (73 lines) | ~30 ms | ~16 MB |

### Lexer Isolation Benchmarks (LexerBenchmarks)

| Benchmark | Mean | Allocated |
|-----------|------|-----------|
| Large Corpus (~476 lines) | ~1 ms | ~500 KB |
| Combined Corpus (~700 lines) | ~1.5 ms | ~750 KB |

### Parser Isolation Benchmarks (ParserBenchmarks)

| Benchmark | Mean | Allocated |
|-----------|------|-----------|
| Large Corpus (~476 lines, pre-tokenized) | ~3 ms | ~2 MB |
| Fibonacci (22 lines, pre-tokenized) | ~0.5 ms | ~500 KB |
| Classes (35 lines, pre-tokenized) | ~0.7 ms | ~600 KB |

### Throughput Benchmarks (ThroughputBenchmarks)

| Benchmark | Mean | Lines/sec |
|-----------|------|-----------|
| Combined Corpus | ~35 ms | ~20,000 |

## Performance Notes

1. **Startup cost**: First compilation has JIT overhead. BenchmarkDotNet handles warmup automatically.

2. **Memory**: Most allocations come from Roslyn's SyntaxFactory for code generation.

3. **Bottlenecks** (measured per-phase proportions of a single-file compile, from the
   D3 breakdown above; front-end vs. Roslyn back-end split, medians under load):
   - Lexer + Parser (front-end syntax): < 1% of total time
   - Type checking: ~5% of total (grows with program size — ~4.6% on the 6000-line file)
   - Code generation (emit + `ToFullString` flatten): ~15–25% of total
   - **C# reparse ("C# Parsing" phase)**: 0.04–1.6% of total, ~6.5% of emit-related work on
     large files — the redundant round trip **D3 (#1050)** removes (plus an equal untimed
     validation reparse)
   - Roslyn back-end (Reference Resolution + Roslyn Compilation + IL Emission): ~70–80% of
     total, dominated by per-process metadata-reference loading and IL emit — the target of
     **D2 (#1049)**, not D3

4. **Incremental compilation**: Wired up for `.spyproj` projects since #756 and benchmarked as of #1053. The cross-language harness (`benchmarks/cross-language/run_benchmarks.py`) measures a cold compile (empty cache) and a warm `--incremental` compile (cache present) per benchmark via a synthetic one-file project; both are published in `results/latest.md`. Single-file compiles have no incremental cache and stay cold. The **persistent compiler-server** compile path landed with **D2 (#1049)**: the harness now also measures an end-to-end `sharpyc build --server` compile through a keep-alive process (see the **D2 Persistent-Server Compile** section below), reported as the `Server` column.

## CI Integration

Benchmarks run automatically on PRs that touch compiler code via `.github/workflows/benchmarks.yml`.

- **Trigger**: PRs to `mainline` modifying `src/Sharpy.Compiler/**` or `src/Sharpy.Compiler.Benchmarks/**`
- **Manual trigger**: Use "Run workflow" button in GitHub Actions
- **Results**: Uploaded as artifacts (JSON + Markdown), retained for 30 days
- **Scope**: Runs `CompilerBenchmarks` only (not full suite) for speed

To view results from a PR:
1. Go to the PR's "Checks" tab
2. Click "Benchmarks" workflow
3. Download "benchmark-results-N" artifact

## Allocation-Regression Gate (Phase 14 — borrowing list)

The benchmarks workflow fails the build if any `CompilerBenchmarks` benchmark's **Allocated**
column (BenchmarkDotNet `[MemoryDiagnoser]` `BytesAllocatedPerOperation`) regresses more than
**10%** versus the checked-in baseline `benchmarks/allocation-baseline.json`. Allocated bytes are
measured precisely (not statistically), so this is a stable, machine-independent signal — and the
prerequisite for the object-pooling work ([#1100](https://github.com/antonsynd/sharpy/issues/1100)),
which needs regression detection before it starts.

The baseline is a **deliberate artifact**, treated like a `.expected.cs` snapshot: it is only
updated on purpose and the change is reviewed. "Regenerate to green" is exactly what the gate exists
to prevent. If an allocation increase is intentional, refresh it and review the diff:

```bash
# 1) Produce a fresh JSON export (a short job is fine — allocation is precise regardless of length):
dotnet run -c Release --project src/Sharpy.Compiler.Benchmarks -- \
  --filter "*CompilerBenchmarks*" --job short --exporters json --artifacts ./BenchmarkResults

# 2) Rewrite the baseline from that export, then review the git diff before committing:
python -m build_tools.allocation_gate update \
  --baseline benchmarks/allocation-baseline.json --results ./BenchmarkResults
```

The comparison script and its pytest live in `build_tools/allocation_gate.py` and
`build_tools/tests/test_allocation_gate.py` (run via the standard `python -m pytest build_tools/tests/`).

## LSP Analysis Latency (Phase 14 — borrowing list "measure first")

> **Recorded:** 2026-07-24 · Apple M4 Max (14 cores), macOS 26.5.2 · .NET 10.0.301
> **Harness:** `LspAnalysisLatencyBaselineHarness` (`src/Sharpy.Lsp.Tests/`, `Category=Benchmark`,
> excluded from the normal run); warm in-process medians of 15 timed runs after 3 warmups.

Per-change **change→publish** analysis wall time for the LSP paths, driven through the real
instrumented code (`AnalysisLatencyLog` lines in `SharpyWorkspace.FireAndForgetAnalysis` and
`LanguageService.OnDocumentChangedAsync`). The #1099 carve-out landed two changes to the project
path: (a) open workspace buffers are overlaid into analysis so project diagnostics reflect unsaved
edits, and (b) an `AstFingerprint`-gated fast path skips whole-project reanalysis when the changed
document is structurally unchanged (comment/whitespace-only edits) — the new **no-change edit skip**
row below. The rest of the incremental-frontend work
([#1099](https://github.com/antonsynd/sharpy/issues/1099): lazy memoized binding, incremental
reparse) stays roadmap, so a structural edit still re-runs a full whole-project analysis.

| Path | Input | median | min | max |
|------|-------|-------:|----:|----:|
| single-file full analysis | 227-line file (`GetAnalysisAsync`, no incremental reuse) | 4.0 ms † | 3.1 ms | 9.5 ms |
| project full reanalysis | 6-file project, 54 lines total (`OnDocumentChangedAsync` → `AnalyzeProject`) | 1.2 ms | 0.8 ms | 1.7 ms |
| project no-change edit skip | 6-file project, comment/whitespace edit to `stats.spy` (fast path returns without reanalysis) | 0.0 ms ‡ | 0.0 ms | 0.0 ms |

† The single-file path is **unchanged** by the #1099 carve-out (only the project path was touched),
but the elevated median versus the 2026-07-15 reading (1.7 ms) is **not** load skew: an uncontended
re-run the same evening reproduced it (3.8 ms median, min 3.5, max 6.9, 15 runs) while the project
full-reanalysis row improved to 0.9 ms median. The shift landed somewhere between 2026-07-15 and
2026-07-24, outside this carve-out — tracked in
[#1137](https://github.com/antonsynd/sharpy/issues/1137).

‡ Sub-0.1 ms: the fast path only parses the edited buffer and runs `AstFingerprint.Classify` against
the last-analyzed AST, then returns without touching the project. Only edits that classify as
`NoChange` skip; `AstFingerprint` conservatively reports `BodyOnly` for any function body it cannot
prove equal (list literals, comprehensions, class-member bodies, …), so files with those in scope
(e.g. `main.spy`) fall through to a full reanalysis even on a whitespace-only edit — `stats.spy`
stays within the provable subset and is the row's subject for that reason.

Caveats: warm (post-JIT) medians on a fast machine and small representative inputs; on a full
reanalysis the project path rebuilds a fresh `Compiler` + `ModuleRegistry` per change
(`CompilerApi.cs:288/:314`) and re-analyzes the whole project, so the cost grows with project size,
not just the edited file. Refresh with the harness command in its class doc comment.

### To refresh the LSP latency baseline

```bash
.claude/scripts/dotnet-serialized test \
  --filter "FullyQualifiedName~LspAnalysisLatencyBaselineHarness" \
  --logger "console;verbosity=detailed"
# Transcribe the "[LSP latency] …" lines into the table above.
```

## Updating Baselines

After significant compiler changes, run full benchmarks and update this file:

```bash
# Run full benchmark suite
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release -- --exporters json markdown

# Results will be in BenchmarkDotNet.Artifacts/
```

Then update the tables above with the actual numbers from the markdown export.
