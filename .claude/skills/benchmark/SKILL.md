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

## Baseline Management

```bash
# Save current results as baseline
cp -r .claude/tmp/benchmark-results .claude/tmp/benchmark-baseline

# Compare next run against baseline (cross-language)
python3 benchmarks/cross-language/run_benchmarks.py --json > .claude/tmp/benchmark-current.json
```

## Notes

- Cross-language benchmarks require: Python 3.9+, .NET 10 SDK
- Compiler benchmarks must use Release mode (Debug is meaningless)
- Benchmark corpus: `src/Sharpy.Compiler.Benchmarks/Corpus/`
- Cross-language programs: `benchmarks/cross-language/*/`
