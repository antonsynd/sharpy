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

> **Note:** These are placeholder numbers. Run full benchmarks on your machine and update.

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

3. **Bottlenecks**:
   - Lexer: ~5% of total time
   - Parser: ~10% of total time
   - Semantic analysis: ~25% of total time
   - Code generation: ~60% of total time (dominated by Roslyn)

4. **Incremental compilation**: Wired up for `.spyproj` projects since #756 and benchmarked as of #1053. The cross-language harness (`benchmarks/cross-language/run_benchmarks.py`) measures a cold compile (empty cache) and a warm `--incremental` compile (cache present) per benchmark via a synthetic one-file project; both are published in `results/latest.md`. Single-file compiles have no incremental cache and stay cold. The persistent compiler-server compile path is pending #1049 (D2).

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

## Updating Baselines

After significant compiler changes, run full benchmarks and update this file:

```bash
# Run full benchmark suite
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release -- --exporters json markdown

# Results will be in BenchmarkDotNet.Artifacts/
```

Then update the tables above with the actual numbers from the markdown export.
