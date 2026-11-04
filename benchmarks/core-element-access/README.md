# core-element-access — the P76 (#2404) kernel

One 100×100 `int` matrix multiply, written the way the Sharpy compiler emits
`c[i][j] += a[i][k] * b[k][j]`: eight element accesses per inner iteration. It measures whether
`Sharpy.Core`'s element accessors inline (#2404: before P76 the hot block carried 8 out-of-line
calls to `Sharpy.Index.Normalize` / `GetItemUnchecked`). The standing guard is
`src/Sharpy.Core.Tests/ElementAccessInlinabilityConformanceTests.cs` (a static IL-shape scan);
this program is the JIT-level evidence the scan cannot see. It is outside `sharpy.sln` and never
part of the gate.

| Variant | Accessor route |
|---|---|
| `indexer` | `Sharpy.List<T>.this[int]` get/set (the plain `xs[i]` route) |
| `unchecked` | `Sharpy.List<T>.GetItemUnchecked` reads, indexer write (the `range`-loop route) |
| `bcl` | `System.Collections.Generic.List<T>` (the floor R-KA's 1.3× trigger is read against) |
| `arrayhelpers` | `int[][]` through `Sharpy.ArrayHelpers.GetItem`/`SetItem` (the `array[T]` route) |
| `raw` | `int[][]` indexed directly (the lower bound) |

## Timing

```bash
.claude/scripts/dotnet-serialized build benchmarks/core-element-access -c Release
DOTNET_TieredCompilation=0 dotnet benchmarks/core-element-access/bin/Release/net10.0/CoreElementAccess.dll indexer 30
```

Every run prints `IsJITOptimizerDisabled=` for the loaded `Sharpy.Core`; a reading with `True` is a
Debug Core and is discarded. The second argument is the number of timed runs after 5 warm-ups; the
program prints the median.

**No-difference control** (`benchmarks/BASELINE.md`): before reading any before/after or A/B, run
the same variant twice, interleaved with the other arm, and check the two medians land inside the
15 % single-sequence floor. If they do not, the machine is noisy and the reading is discarded.

## Disassembly

```bash
DOTNET_TieredCompilation=0 DOTNET_JitDisasm='MatMul*' DOTNET_JitStdOutFile=/path/to/disasm.txt \
  dotnet benchmarks/core-element-access/bin/Release/net10.0/CoreElementAccess.dll indexer 1
```

Start at the innermost loop head of `MatMulIndexer` / `MatMulUnchecked`, follow the in-range path
(the bounds branch — `bhs` on arm64, `jae` on x64 — not taken) back to the loop's back-edge, and
count the call instructions on it — `call` on x64, `bl`/`blr` on arm64. Before P76: 8 per variant.
After P76: 0. The JIT may lay the `[NoInlining]` `Index.WrapOrThrow` calls out *inside* the loop's
address range (it does for `indexer` on arm64: 8 slow blocks, each reached only by a taken `bhs`),
so count the in-range path, not the address span.
