# Sharpy Compiler Benchmark Baselines

> **Last Updated:** 2026-02-02
> **Commit:** `dev` branch
> **Machine:** (Update with your machine specs when running)
> **Runtime:** .NET 10.0

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

4. **Incremental compilation**: Not yet wired up (see hardening item 5.1). When enabled, expect 2-10x speedup for unchanged files.

## Updating Baselines

After significant compiler changes, run full benchmarks and update this file:

```bash
# Run full benchmark suite
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release -- --exporters json markdown

# Results will be in BenchmarkDotNet.Artifacts/
```

Then update the tables above with the actual numbers from the markdown export.
