# Cross-Language Benchmarks

Compares runtime performance of equivalent programs in **Sharpy** (compiled to .NET), **raw C#**, **CPython**,
and **[Boo](https://github.com/boo-lang/boo)** (the other Python-flavoured, statically typed .NET language).

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

# A subset of languages
python3 benchmarks/cross-language/run_benchmarks.py --languages Sharpy,Boo
```

### Boo toolchain

Boo has no .NET 10 release yet, so on first use the harness fetches the commit pinned in
`BOO_COMMIT` (`run_benchmarks.py`) into `~/.cache/sharpy-bench/boo-<sha>/` and builds
`src/booc` in Release (~20 s, needs network access to GitHub); later runs reuse the build.
Set `BOOC_DLL=/path/to/booc.dll` to use an existing compiler instead. When Boo is only
implied by the all-languages default and the fetch or build fails, the harness warns and
runs the other languages.

`booc` writes an assembly rather than a runnable app, so the harness drops a
`bench.runtimeconfig.json` and `Boo.Lang.dll` beside it (the method Boo's own release
smoke test uses). Benchmarks compile with `-debug-` (comparable to the other languages'
Release builds) and keep Boo's default **checked** arithmetic.

## Output

```
Benchmark              Python       Sharpy       C#           Boo          Spy/Py   Spy/C#   Spy/Boo
------------------------------------------------------------------------------------------------
fibonacci              4.21s        0.08s        0.06s        0.07s        0.02x    1.33x    1.14x
sorting                1.83s        0.12s        0.09s        0.15s        0.07x    1.33x    0.80x
...

Spy/Py < 1.0 = Sharpy faster than Python
Spy/C# ~ 1.0 = Sharpy matches raw C# (minimal overhead)
Spy/Boo < 1.0 = Sharpy faster than Boo
```

## Adding a Benchmark

1. Create a directory: `benchmarks/cross-language/<name>/`
2. Add three files: `bench.spy`, `bench.py`, `bench.cs` — plus `bench.boo` to include Boo
   (optional; a benchmark without one shows `—` in the Boo column)
3. Each must produce identical output and do the same work
4. Use `def main():` as entry point in `.spy` and `.py`; `.boo` defines `main()` and calls it at module level

### Optional sidecars

| File | Effect |
|------|--------|
| `bench.features` | Experimental feature flags (one per line) passed to the Sharpy compiler as `--enable-feature <name>` and into the warm-compile project's `<Features>`. Needed for gated syntax such as the `@` matmul operator. |
| `bench.languages` | Languages the benchmark opts into (one per line: `Python`, `Sharpy`, `C#`, `Boo`); absent = all of them. Use to hold a language out of the harness. |

`#` starts a comment at line start or after whitespace (so the `C#` token survives).
The C# project also gains a `MathNet.Numerics` package reference automatically when
`bench.cs` contains `using MathNet`.

> `matrix_multiply_numpy` holds Sharpy out via `bench.languages` until **#1084**
> lands: numpy delegates to MathNet.Numerics, but the emitted `deps.json` omits
> transitive NuGet deps, so a compiled numpy program can't load MathNet at runtime.
> `bench.spy` still compiles (proving the `@`/matmul path); its numeric-path numbers
> come from Python (native BLAS) and C# (MathNet).

## Design Principles

- Programs must be **semantically equivalent** across all languages
- The `.boo` versions are **typed** (`long`, `List[of long]`) to match Sharpy's static `int`/`list[int]`;
  untyped Boo `[...]` literals are `object` lists that box every element, which would measure something else
- Use only features Sharpy supports (no Python-only tricks)
- Each benchmark should take 1-10 seconds in Python (enough to measure, not too slow for CI)
- The `.cs` version represents "what a developer would write by hand" (not hyper-optimized)
