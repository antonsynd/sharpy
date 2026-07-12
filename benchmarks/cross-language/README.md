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
| `matrix_multiply_numpy` | Numeric path: numpy `@` / MathNet (native BLAS) vs the pure-list kernel |

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

### Optional sidecars

| File | Effect |
|------|--------|
| `bench.features` | Experimental feature flags (one per line) passed to the Sharpy compiler as `--enable-feature <name>` and into the warm-compile project's `<Features>`. Needed for gated syntax such as the `@` matmul operator. |
| `bench.languages` | Languages the benchmark opts into (one per line: `Python`, `Sharpy`, `C#`); absent = all three. Use to hold a language out of the harness. |

`#` starts a comment at line start or after whitespace (so the `C#` token survives).
The C# project also gains a `MathNet.Numerics` package reference automatically when
`bench.cs` contains `using MathNet`.

> `matrix_multiply_numpy` holds Sharpy out via `bench.languages` until **#1084**
> lands: numpy delegates to MathNet.Numerics, but the emitted `deps.json` omits
> transitive NuGet deps, so a compiled numpy program can't load MathNet at runtime.
> `bench.spy` still compiles (proving the `@`/matmul path); its numeric-path numbers
> come from Python (native BLAS) and C# (MathNet).

## Design Principles

- Programs must be **semantically equivalent** across all three languages
- Use only features Sharpy supports (no Python-only tricks)
- Each benchmark should take 1-10 seconds in Python (enough to measure, not too slow for CI)
- The `.cs` version represents "what a developer would write by hand" (not hyper-optimized)
