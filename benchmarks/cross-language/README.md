# Cross-Language Benchmarks

Compares runtime performance of equivalent programs in **Sharpy** (compiled to .NET), **raw C#**, and **CPython**.

## Benchmarks

| Name | What it tests |
|------|---------------|
| `fibonacci` | Recursive + iterative compute (CPU-bound, function call overhead) |
| `sorting` | Quicksort with list comprehensions (allocation-heavy, recursion) |
| `string_ops` | Concatenation, case conversion, splitting (string interning, GC) |
| `list_comprehensions` | Filtered + nested comprehensions (collection construction) |
| `matrix_multiply` | Nested loops, array indexing (tight numeric loops) |

## Running

```bash
# All benchmarks
python3 benchmarks/cross-language/run_benchmarks.py

# Specific ones
python3 benchmarks/cross-language/run_benchmarks.py fibonacci sorting

# JSON output (for CI/tooling)
python3 benchmarks/cross-language/run_benchmarks.py --json
```

## Output

```
Benchmark              Python       Sharpy       C#           Spy/Py   Spy/C#
--------------------------------------------------------------------------
fibonacci              4.21s        0.08s        0.06s        0.02x    1.33x
sorting                1.83s        0.12s        0.09s        0.07x    1.33x
...

Spy/Py < 1.0 = Sharpy faster than Python
Spy/C# ~ 1.0 = Sharpy matches raw C# (minimal overhead)
```

## Adding a Benchmark

1. Create a directory: `benchmarks/cross-language/<name>/`
2. Add three files: `bench.spy`, `bench.py`, `bench.cs`
3. Each must produce identical output and do the same work
4. Use `def main():` as entry point in `.spy` and `.py`

## Design Principles

- Programs must be **semantically equivalent** across all three languages
- Use only features Sharpy supports (no Python-only tricks)
- Each benchmark should take 1-10 seconds in Python (enough to measure, not too slow for CI)
- The `.cs` version represents "what a developer would write by hand" (not hyper-optimized)
