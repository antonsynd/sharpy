# Performance Benchmarks

Sharpy compiles to .NET IL via C#, delivering near-native performance while keeping Pythonic syntax.
These benchmarks compare equivalent programs across Sharpy, C#, CPython and [Boo](https://github.com/boo-lang/boo).

## Latest Results

See **[`latest.md`](https://github.com/antonsynd/sharpy/blob/benchmark-results/latest.md)** on the `benchmark-results` branch for execution times, compilation times (cold/warm/server), and Spy/Py, Spy/C# and Spy/Boo ratios. Updated weekly by CI, which commits each run's results to that branch (one commit per run) and also attaches them to the workflow run as an artifact.

Full history (the last 52 weekly data points): [`history.json`](https://github.com/antonsynd/sharpy/blob/benchmark-results/history.json). The copy under `benchmarks/cross-language/results/` on `dev` is a frozen snapshot (2026-11-02) that seeded the branch.

## Performance Trend

![Spy/Py, Spy/C# and Spy/Boo ratios over time](https://raw.githubusercontent.com/antonsynd/sharpy/benchmark-results/trend.svg)

## Methodology

| Benchmark | What it measures |
|-----------|-----------------|
| **fibonacci** | Recursive + iterative compute (function call overhead) |
| **sorting** | Quicksort with comprehensions (allocation, recursion) |
| **string_ops** | Concatenation, case conversion, splitting (GC pressure) |
| **list_comprehensions** | Filtered + nested comprehensions (collection construction) |
| **matrix_multiply** | Nested loops, array indexing (tight numeric loops) |

Each program is semantically equivalent across all languages. The C# version represents
idiomatic hand-written code (not hyper-optimized). Benchmarks run weekly on `ubuntu-latest` via GitHub Actions.

**Spy/Py ratio** — values below 1.0 mean Sharpy is faster than CPython.
**Spy/C# ratio** — values near 1.0 mean Sharpy has minimal overhead vs raw C#.
**Spy/Boo ratio** — values below 1.0 mean Sharpy is faster than Boo, the other Python-flavoured statically typed .NET language.
