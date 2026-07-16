# Cross-Language Benchmark Results (2026-07-16)

**Runner:** local | **Python:** 3.12 | **.NET:** 10.0 | **Runs:** 3 (median) + warmup

## Execution Time (runtime only)

| Benchmark | Python | Sharpy | C# | Spy/Py | Spy/C# |
|-----------|--------|--------|-----|--------|--------|
| fibonacci | 100ms | 33ms | 34ms | 0.33x | 0.97x |
| list_comprehensions | 40ms | 40ms | 57ms | 0.99x | 0.70x |
| matrix_multiply | 356ms | 94ms | 42ms | 0.27x | 2.27x |
| matrix_multiply_numpy | 204ms | FAIL | 373ms | — | — |
| sorting | 87ms | 77ms | 121ms | 0.89x | 0.64x |
| string_ops | 93ms | 108ms | 101ms | 1.16x | 1.07x |

## Compilation Time

| Benchmark | Python (.pyc) | Sharpy (cold) | Sharpy (warm) | Sharpy (server) | C# (dotnet build) | Spy/C# |
|-----------|---------------|---------------|---------------|-----------------|-------------------|--------|
| fibonacci | 523us | 749ms | 667ms | 94ms | 642ms | 1.17x |
| list_comprehensions | 609us | 805ms | 705ms | 100ms | 661ms | 1.22x |
| matrix_multiply | 692us | 840ms | 735ms | 109ms | 669ms | 1.26x |
| matrix_multiply_numpy | 658us | — | — | — | 743ms | — |
| sorting | 565us | 912ms | 773ms | 109ms | 675ms | 1.35x |
| string_ops | 774us | 815ms | 699ms | 94ms | 630ms | 1.29x |

> **Spy/Py < 1.0** = Sharpy execution faster than Python. **Spy/C# ≈ 1.0** = minimal overhead vs hand-written C#.

> **Sharpy (cold)** = single-file compile with no cache; **Sharpy (warm)** = `--incremental` one-file `.spyproj` compile with the symbol cache present; **Sharpy (server)** = end-to-end `sharpyc build --server` compile through a persistent keep-alive process ([#1049](https://github.com/antonsynd/sharpy/issues/1049), D2), which reuses the process-lifetime `MetadataReference`/overload-index caches across compiles.

## E3 Optimization Passes (#1057)

The three E3 IR optimization passes — `opt_const_fold`, `opt_comprehension_fusion`,
`opt_stack_collections` — are all default-off CodeGen-scoped behavioral flags. Emitting each benchmark
above with all three flags on and diffing against the flags-off baseline yields **byte-identical C#**
for every benchmark, so the execution-time and compilation-time numbers above are unchanged by E3 and
stand as both the flags-off and flags-on baseline. The passes do not fire on these workloads (no
constant-folding operations; the multi-`for` comprehension uses `range()` call sources the fusion pass
excludes; no `for`-over-list-literal to stack). Per-pass deltas, micro-demonstrations proving each pass
reduces work where it fires, and the `opt_devirt` retirement finding are recorded in
[`benchmarks/BASELINE.md`](../../BASELINE.md#e3-ir-optimization-pass-deltas-1057).

---
*Baseline table generated 2026-07-16 (pre-E3); confirmed unchanged post-E3 (#1057) by emit-diff — the
E3 passes produce byte-identical IL on this suite.*
