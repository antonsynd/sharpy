---
name: benchmark
description: Run compiler or cross-language benchmarks and compare results
disable-model-invocation: true
---

# Benchmark

Run benchmarks for the Sharpy compiler. Two modes:

## Cross-Language Comparison (Sharpy vs C# vs Python)

Runs equivalent programs in all three languages and produces a comparison table.

```bash
# All benchmarks
python3 benchmarks/cross-language/run_benchmarks.py

# Specific benchmarks
python3 benchmarks/cross-language/run_benchmarks.py fibonacci sorting

# JSON output for tooling
python3 benchmarks/cross-language/run_benchmarks.py --json
```

Available: `fibonacci`, `sorting`, `string_ops`, `list_comprehensions`, `matrix_multiply`

## Compiler Throughput (BenchmarkDotNet)

Measures how fast the compiler itself runs (lex/parse/emit).

```bash
# Run full suite
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release -- \
  --filter "*CompilerBenchmarks*" --exporters json markdown \
  --artifacts .claude/tmp/benchmark-results

# Specific benchmark
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release -- \
  --filter "*Fibonacci*" --exporters markdown \
  --artifacts .claude/tmp/benchmark-results
```

## Comparing Two Revisions (A/B)

**Do not measure one revision, then the other, then subtract.** Run position alone swings
wall clock 7-10% on this corpus: whatever runs second looks faster, with non-overlapping
99.9% confidence intervals in *both* directions depending on which arm you ran first
(#1318). A sequential comparison cannot tell that apart from a code effect, and reading
the intervals instead of the means does not rescue it — three round-8 measurements were
corrupted this way.

Use the interleaved orchestrator. It prepares a worktree per ref, builds both in Release
once, alternates A,B,A,B…, pools by position, and reports a delta **only when the two
positions agree in sign**:

```bash
# Compare two refs (an EVEN --rounds balances the positions)
python3 -m build_tools.bench_ab HEAD~1 HEAD --rounds 4

# One benchmark
python3 -m build_tools.bench_ab main my-branch --filter '*Fibonacci*' --rounds 4

# Null control — must report nothing. Run this if you doubt a result.
python3 -m build_tools.bench_ab HEAD HEAD --rounds 4
```

Reading the output:

- **UNMEASURED — position-dominated**: the two orderings disagree on the sign. There is no
  delta; do not report one.
- **UNMEASURED — below the 15% floor**: inside the range the artifact alone produces. Not
  "no regression" — *unmeasured*.
- A reported delta: both positions agreed and the magnitude cleared the floor.

**Allocations are immune** to the position artifact (BenchmarkDotNet measures them
precisely, not statistically), so when a change's mechanism predicts an allocation move,
`build_tools/allocation_gate.py` is the load-bearing signal and wall clock is corroboration.

## Notes

- Cross-language benchmarks require: Python 3.9+, .NET 10 SDK
- Compiler benchmarks must use Release mode (Debug is meaningless)
- Benchmark corpus: `src/Sharpy.Compiler.Benchmarks/Corpus/`
- Cross-language programs: `benchmarks/cross-language/*/`
