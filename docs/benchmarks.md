# Performance Benchmarks

Sharpy compiles to .NET IL via C#, delivering near-native performance while keeping Pythonic syntax.
These benchmarks compare equivalent programs across three runtimes.

## Latest Results

See **[`benchmarks/cross-language/results/latest.md`](../benchmarks/cross-language/results/latest.md)** for execution times, compilation times (cold/warm/server), and Spy/Py and Spy/C# ratios. Updated weekly by CI.

Full history (17 weekly data points): [`history.json`](../benchmarks/cross-language/results/history.json).

## Performance Trend

![Spy/Py and Spy/C# ratio over time](../benchmarks/cross-language/results/trend.svg)

## Methodology

| Benchmark | What it measures |
|-----------|-----------------|
| **fibonacci** | Recursive + iterative compute (function call overhead) |
| **sorting** | Quicksort with comprehensions (allocation, recursion) |
| **string_ops** | Concatenation, case conversion, splitting (GC pressure) |
| **list_comprehensions** | Filtered + nested comprehensions (collection construction) |
| **matrix_multiply** | Nested loops, array indexing (tight numeric loops) |

Each program is semantically equivalent across all three languages. The C# version represents
idiomatic hand-written code (not hyper-optimized). Benchmarks run weekly on `ubuntu-latest` via GitHub Actions.

**Spy/Py ratio** — values below 1.0 mean Sharpy is faster than CPython.
**Spy/C# ratio** — values near 1.0 mean Sharpy has minimal overhead vs raw C#.
