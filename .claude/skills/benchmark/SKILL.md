---
name: benchmark
description: Run compiler or cross-language benchmarks and compare results
disable-model-invocation: true
---

# Benchmark

Run benchmarks for the Sharpy compiler. Two modes:

## Cross-Language Comparison (Sharpy vs C# vs Python vs Boo)

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
# Run full suite (raw `dotnet run` is hook-blocked — it builds; the wrapper is a drop-in)
.claude/scripts/dotnet-serialized run --project src/Sharpy.Compiler.Benchmarks -c Release -- \
  --filter "*CompilerBenchmarks*" --exporters json markdown \
  --artifacts .claude/tmp/benchmark-results

# Specific benchmark
.claude/scripts/dotnet-serialized run --project src/Sharpy.Compiler.Benchmarks -c Release -- \
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
# Compare two refs (--rounds must be EVEN, and defaults to 4)
python3 -m build_tools.bench_ab HEAD~1 HEAD --rounds 4

# One benchmark
python3 -m build_tools.bench_ab main my-branch --filter '*Fibonacci*' --rounds 4

# Null control — must report nothing. Run this if you doubt a result.
python3 -m build_tools.bench_ab HEAD HEAD --rounds 4
```

Reading the output:

- **UNMEASURED — position-dominated**: the two orderings disagree on the sign. There is no
  delta; do not report one.
- **UNMEASURED — unbalanced (arm, position) cells**: a round was dropped as interrupted, or
  the count was odd, so the arms were not measured the same number of times in each position.
  That equality is what makes a position bias cancel, so the comparison is refused rather than
  pooled (#1509). Re-run; every verdict line ends with `[samples a@1:N a@2:N …]` so the
  evidence base behind any number is always visible.
- **UNMEASURED — below the 15% floor**: inside the range the artifact alone produces. Not
  "no regression" — *unmeasured*.
- A reported delta: both positions agreed and the magnitude cleared the floor.

**Allocations are much steadier** than wall clock, but not exact: identical code measures
1–2% apart between runs, and machines differ by more (#2759). So when a change's mechanism
predicts an allocation move, measure both refs on the same machine, more than once, and treat a
move under 2% as unmeasured. The CI gate (`build_tools/allocation_gate.py`) runs only in CI (#2462).

## Notes

- Cross-language benchmarks require: Python 3.9+, .NET 10 SDK (and GitHub access on first Boo use — see the cross-language README)
- Compiler benchmarks must use Release mode (Debug is meaningless)
- Benchmark corpus: `src/Sharpy.Compiler.Benchmarks/Corpus/`
- Cross-language programs: `benchmarks/cross-language/*/`
