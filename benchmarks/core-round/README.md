# core-round — the W1.2 (#2460) `round()` kernel

`Sharpy.Builtins.Round` follows CPython's `float.__round__` / `int.__round__` since W1.2 (PR #2465):
half to even, any `ndigits` (negative, zero, past the precision), correctly rounded through
decimal/BigInteger arithmetic instead of `System.Math.Round`'s binary scaling. The W1.2 report left
the cost unmeasured; the owner asked for it on 2026-11-07. This program is the measurement: one call
per element over the same 4096 finite doubles for each variant, median nanoseconds per call. It is
outside `sharpy.sln`, like `core-element-access`, and never part of the gate.

| Variant | Call | Note |
|---|---|---|
| `builtins-int` | `Sharpy.Builtins.Round(double)` → `int` | CPython contract (exact, NaN/inf raise) |
| `math-int` | `(int)Math.Round(x, MidpointRounding.ToEven)` | the route W1.2 replaced (NaN → 0, inf → int.MaxValue) |
| `builtins-2` | `Sharpy.Builtins.Round(double, 2)` | correctly rounded (`round(2.675, 2) == 2.67`) |
| `math-2` | `Math.Round(x, 2, MidpointRounding.ToEven)` | the route W1.2 replaced (`2.68`; digits 0..15 only) |
| `builtins-neg2` | `Sharpy.Builtins.Round(double, -2)` | no `Math.Round` counterpart (it throws for n < 0) |
| `builtins-int-neg1` | `Sharpy.Builtins.Round(int, -1)` | the overload W1.2 added |

## Timing

```bash
.claude/scripts/dotnet-serialized build benchmarks/core-round -c Release
DOTNET_TieredCompilation=0 dotnet benchmarks/core-round/bin/Release/net10.0/CoreRound.dll builtins-2 30
```

Every run prints `IsJITOptimizerDisabled=` for the loaded `Sharpy.Core`; a reading with `True` is a
Debug Core and is discarded. The second argument is the number of timed runs after 5 warm-ups; the
program prints the median. The checksums of a `builtins-*` variant and its `math-*` twin agree on this
input set (no tie lands on a cell where the two contracts differ), which is what makes the pair a cost
comparison and not a correctness one — `RoundDifferentialTests` owns correctness.

**No-difference control** (`benchmarks/BASELINE.md`): run the six variants twice, interleaved, and
read a difference only where both passes agree and the gap is outside the 15 % single-sequence floor.

## Reading, 2026-11-07 @ dev 3f103e098 (W1.2 landed)

Cloud container, 4 cores, Linux x64, .NET 10, Release Core, `DOTNET_TieredCompilation=0`, 30 runs,
two interleaved passes:

| Variant | pass 1 median ns/call | pass 2 | verdict |
|---|---:|---:|---|
| `builtins-int` | 2.7 | 3.1 | ≈ 2 ns over `math-int`; both nanoseconds, UNMEASURED at the 15 % floor's scale |
| `math-int` | 1.7 | 0.9 | |
| `builtins-2` | 629.8 | 667.7 | **≈ 400× `math-2`** — both passes agree |
| `math-2` | 1.8 | 1.2 | |
| `builtins-neg2` | 533.9 | 507.3 | ≈ 0.5 µs/call; no baseline route |
| `builtins-int-neg1` | 101.0 | 139.2 | ≈ 0.1 µs/call; no baseline route |

What it says: `round(x)` with no digits is as cheap as before. `round(x, n)` costs about half a
microsecond per call — the decimal/BigInteger correctly-rounded path — where `Math.Round` cost a
nanosecond. No benchmark corpus (`benchmarks/cross-language/*/*.spy`, `src/Sharpy.Compiler.Benchmarks/Corpus/*.spy`)
calls `round(x, n)`, so the cross-language suite does not see it. A fast path (for `0 ≤ n ≤ 15` and
`|x| < 2^52`, take `Math.Round`'s answer when the exact decimal expansion is not within one ulp of a
tie, fall back otherwise) is the obvious cell if a program ever shows `round(x, n)` on a hot path;
it is not chartered — CPython's own `round(x, n)` goes through dtoa at roughly 100–200 ns/call, so
the contract was never a nanosecond one.
