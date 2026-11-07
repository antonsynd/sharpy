<!-- Verified by /verify-plan --adequacy (Adequacy-only: Dimensions 4-design, 6, 7; check_plan_refs clean) on 2026-11-03 @ 89fba5eec -->
<!-- Verification result: PASS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: not-required -->
# P76 — Core element access inlines (the throw-helper fast path at every Core accessor)

## Context

Every `xs[i]` a Sharpy program executes goes through `Sharpy.List<T>.this[int]`, which calls
`Sharpy.Index.Normalize(int, int, bool, bool)`; `for i in range(n): xs[i]` goes through
`List<T>.GetItemUnchecked` (#1052). Both bodies build their `IndexError` message with string
interpolation **on the hot path** (`throw new IndexError($"list index {i} out of range")`), which
puts a `DefaultInterpolatedStringHandler` construction, three `Append*` calls and `ToStringAndClear`
inside a method the JIT is asked to inline — so it does not inline them, and the matrix-multiply
kernel pays an out-of-line call per element access. #2404 (owner-measured, x64 cloud container):
`matrix_multiply` Sharpy 188 ms vs Boo 93 ms vs C# 53 ms; the JIT disassembly shows 8 ×
`call [Sharpy.Index:Normalize]` per inner iteration; `ArrayHelpers.GetItem` (constant message)
inlines. The cure is Core-only (rung 2): the standard throw-helper pattern — a fast path
`if ((uint)i < (uint)count)` with the negative-index wraparound and the exception construction moved
behind a `[MethodImpl(MethodImplOptions.NoInlining)]` helper — at every accessor in the roster, and a
standing conformance guard over that roster so the shape cannot regress.

Charter: `.claude/plans/remediation-round-2026-09-20-batching.md` §3.20g′ (row 6c⁶″); ruling
R-KA at https://github.com/antonsynd/sharpy/issues/2404#issuecomment-6539776539 (the 2026-10-31
batching comment on the issue; §4 row R-KA). Issue: https://github.com/antonsynd/sharpy/issues/2404 (it is the
class tracker). Codegen-side sibling tracker: #2194 (P45, `range` loops / `ArrayHelpers`).

## Current State

Measured @ 89fba5eec (Core sources unchanged since 909b79467; the Release `Sharpy.Core.dll` built
2026-11-01; arm64 Apple Silicon, .NET SDK 10.0.403, `DOTNET_TieredCompilation=0`, median of 30 runs
after 5 warm-ups of one 100×100 `list[list[int]]` multiply, kernel and IL probe in the session
scratchpad `kernel/` and `ilprobe/`). **Kernel:** `Sharpy.List` indexer (A) **13.2–13.3 ms**;
`GetItemUnchecked` reads (B) **13.0 ms**; a local copy of today's `Normalize` shape (X, the
instrument control) **13.3–13.5 ms** — the local wrappers model `Sharpy.List` faithfully; throw-helper
prototype (E, the R-KA task-1 shape over a BCL `List<T>`) **3.55–3.96 ms**; task-2 candidates
`CollectionsMarshal.AsSpan(_l)[i]` (S1) **4.05 ms** and single-check
`Unsafe.Add(ref MemoryMarshal.GetReference(span), i)` (S2) **6.1 ms**; BCL `List<T>` (C)
**2.2–2.3 ms**; i.e. today 5.9× BCL, task-1 shape 1.6–1.75× BCL (the owner's container measured 2.5× →
1.7×, the same ratio). **JIT disassembly** (`DOTNET_JitDisasm=MatMul* DOTNET_JitStdOutFile=…`): the
hot block (`IG16`) of A and B each carry **8 × `blr`** — indirect calls with `w2=w3=0`, i.e.
`Index.Normalize(i, max, false, false)` / `GetItemUnchecked`; E, S1, S2 and C carry **0**.
**IL (Release / Debug bytes):** `Index.Normalize` 102/138 with 7 calls (`Math.Max`, `Math.Min`, the
four interpolation-handler calls, `IndexError..ctor`) [CORRECTED: verify round 3]; `GetItemUnchecked` 82/98, 7 calls;
`List.this[int]` get 34/39 and set 35/37 (both inline; each calls `Normalize`); `Pop` 129/149 with
**one EH clause** (the `catch (IndexError)` rewrap); `List.Index(T,int,int)` 111/138 (two `Normalize`
calls + an interpolated `ValueError`); `ArrayHelpers.GetItem`/`SetItem` 40/41 (one `newobj`,
constant `ldstr` — inlines today); `StringHelpers.GetItem` 56; `Dict<K,V>.this[K]` get 65 (two
`newobj KeyError`); `Slice.Normalize` 24; `IndexExtensions.ToNormalizedInt32` 42; `Bytes.this[int]`
constant-message throws. **Behaviour baseline** (Release `sharpyc run` @ 89fba5eec, 2026-11-03; the program and its
output are saved as `.claude/tmp/p76/element_access_messages_2404.spy` + `.expected` (gitignored,
repo-local) and become the D6 fixture verbatim) [CORRECTED: verify round 2 — the first baseline pinned
list/str/dict only]: **every roster row with a Sharpy route × every direction** — list get/set
(`1 3 3 1`, `list index 3 out of range`, `list index -4 out of range`, set likewise; **a literal
non-negative index lowers to `GetItemUnchecked` (row 3), so row 1's plain read is pinned through a
function parameter** `get_at(xs, i)` — `1 3 3 1` and the same two messages — and through the
negative-step slices `zs[::-1] zs[3:0:-2] zs[-1:-3:-1]` → `[4, 3, 2, 1] [4, 2] [4, 3]`, which read
every element through `List.get_Item` (`Slice.cs:124,130`); python3 agrees [ADDED: verify round 6]), the `range`-loop
route (`18`, then `7 2 9` + `list index 3 out of range`), `pop` (`3 1 2 []`, `pop from empty list`,
`pop index 7 out of range`, `pop index -7 out of range`, `pop(-3)` → `1 [2, 3]`), `insert` clamps
(`[-1, 0, 1, 2, 3, 33, 4]`), `index` with clamped start/end (`1 3 3 0 2`) and `9 is not in list` /
`1 is not in list` / `2 is not in list`, array get/set (`5 13 8 5`, `array index out of range` ×4:
get +3/−4, set +3/−4), str (`a c c a`, `string index out of range` ×2), dict (`1 2`, `KeyError 'zz'`,
`KeyError None` on a missing `None` key, then `5 6 2` after `dn[None] = 5`), bytes (`10 30 30 10`,
`index out of range` ×3: +3, −4, empty), frozendict (`1 2`, `KeyError 'zz'`; the `None`-key branch `FrozenDict.cs:157–161` present → `1 2`
for `fdn[None], fdn["x"]` built from a `dict[str | None, int]`, absent → `KeyError None`
[ADDED: verify round 5 — a `frozendict({None: 1})` literal is refused, SPY0227, so the source dict is
annotated]), **slices (row 6, the
`Slice.GetSlice` route → `Slice.Normalize`: list `Slice.cs:110,128`, array `Slice.cs:151,173`, str
`Slice.cs:185,204`)**: list `[2, 3] [3, 4] [1, 3] [1, 2, 3, 4] [] [3]` for
`ys[1:3] ys[-2:] ys[::2] ys[-9:9] ys[3:1] ys[2:-1]`, the same six on an `array[int]`
(`[2, 3] [3, 4] [1, 3] [1, 2, 3, 4] [] [3]`) [ADDED: verify round 4], str `el lo hlo hello`; bytes
slices `b'\x02\x03' b'\x03\x04' b'\x01\x02\x03\x04'` are pinned as a neighbour only — they
dispatch to `Bytes.Slice` (`Partial.Bytes/Bytes.Slicing.cs:13`), which never reaches `Slice.Normalize`
[CORRECTED: verify round 4] [ADDED: verify round 3]; **compound element assignment** (`xs[i] += v`,
read + write through the same accessor, `RoslynEmitter.Statements.Assignments.cs:279–309`): list
`[11, 2, 23]` then `list index 3 out of range` / `list index -4 out of range`, array `5 0 7` then
`array index out of range` ×2, dict `6` then `KeyError 'zz'` [ADDED: verify round 4]; element
assignment during iteration admitted (`[9, 2, 3]`). Slice assignment is refused (SPY0225), so `SetSlice`/
`DeleteSlice` have no Sharpy route (they stay pinned by Core.Tests). **Row 5** (`ToNormalizedInt32`)
is C#-only: its sole caller is `RangeExtensions.cs:9` and nothing in the compiler or Stdlib calls
`ToSlice` — pinned by its existing Core.Tests. The compared cells (values, exception types, `insert`
clamping, `index` start/end, `KeyError None`) agree with python3 by hand (3.12, same session; the slice lines re-checked in round 3). Row 9
`DeleteAt` has no Sharpy route (`del` is SPY0144) and is pinned by `ListTests.DelItem.cs`. Every line
is unchanged by construction after the fix.

## Scope

- **Group:** P76 — Core element access inlines (one non-chore group; chore riding along: **#2365
  `None`-receiver draw filter** (R-KK; `ILCompilesPropertyTests` in Compiler.Tests only) — seam-free
  because it touches no file this group touches (this group edits `src/Sharpy.Core/` and
  `src/Sharpy.Core.Tests/` + one fixture + `benchmarks/`); it is one commit on this gate with its own
  mutation record (remove the filter → seeds `5F-Ai3FcPnEx` `8sLgyTw-IOM3` `c4U0RdI-d-Uw` red; a second
  shape is not skipped))
- **Tier:** light — from charter §3.20g′ (Core-only; the seam measured in #2404 and re-measured
  here; the one design point — the span path — ruled by R-KA) (contract §12)
- **Alone:** no — Core-internal; no printed-output or emitted-layout change (the emitter is untouched;
  every message text stays byte-identical)
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger; the lead files by class
  (contract §10); trackers named at the end of this plan
- **Override:** none

## Defect Class

- **Violated contract:** every Sharpy.Core element accessor the compiler emits on a hot path
  inlines into its caller; only the negative-index wraparound and the exception construction live
  behind a call — uniform across the accessor roster (list get / set / unchecked get, the
  `Index.Normalize` callers, the `str`, array, `bytes`, `dict` and `frozendict` indexers).
- **Meta-class:** not in the §1 table — **cold-path construction inline on a hot path** (an
  interpolated/allocating throw inside an accessor body defeats the JIT's inline budget). **Standard
  cure:** the throw-helper pattern at the ONE shared seam (`Sharpy.Index`) plus a static IL-shape
  conformance scan over the roster (the sweeps cannot see inlining — the guard's mutation record IS
  the acceptance, R-KA). The round charter §2 table gains this row at close-out.
- **Known member cells:** #2404 (`List<T>.this[int]` get/set via `Index.Normalize`; `GetItemUnchecked`).
- **Sibling cells this plan must also cover (the roster — closed, literal, totality-anchored):**

  | # | Accessor | Today | Cure |
  |---|---|---|---|
  | 1 | `List<T>.this[int]` get | inlines; calls `Normalize` (102 B, interpolated throw) | calls the strict `Index.Normalize(i, n)` fast path |
  | 2 | `List<T>.this[int]` set | same | same |
  | 3 | `List<T>.GetItemUnchecked(int)` | 82 B, interpolated throw inline | fast path + `[NoInlining]` helper |
  | 4 | `Index.Normalize(i, max, forSlice, forInsertion)` | one method, two modes, the throw inline | split: strict `Normalize(i, max)` + `Clamp(i, max)` + `[NoInlining] WrapOrThrow` |
  | 5 | `IndexExtensions.ToNormalizedInt32` (public; C#-only — sole caller `RangeExtensions.ToSlice`, `RangeExtensions.cs:9`) | dispatches to the 4-arg | same signature; dispatches to `Clamp`/`Normalize` |
  | 6 | `Slice.Normalize(start, end, max)` (internal; reached from Sharpy by `xs[a:b]` → `Slice.GetSlice`, `Slice.cs:110,128`) | two 4-arg calls | two `Clamp` calls |
  | 7 | `List<T>.Pop(int)` | 129 B, **EH clause**, interpolated rewrap | explicit wrap + `[NoInlining] PopOutOfRange(i)` / `PopFromEmpty()`; no EH |
  | 8 | `List<T>.Insert(int, T)` | 4-arg (insertion mode) | `Clamp` |
  | 9 | `List<T>.DeleteAt(int)` | 4-arg strict | strict `Normalize` |
  | 10 | `List<T>.Index(T, int, int)` | two 4-arg (slice mode) + interpolated `ValueError` | two `Clamp` + `[NoInlining] NotInList(x)` |
  | 11 | `ArrayHelpers.GetItem` / `SetItem` | inline today (constant message) — the duplicated check | conform to the fast-path shape; message behind the helper |
  | 12 | `StringHelpers.GetItem(string, int)` | 56 B, constant-message throw inline, double `Length` read | fast path + helper |
  | 13 | `Dict<K,V>.this[K]` get **and set** (`src/Sharpy.Core/Dict.cs:160–191`, set at `src/Sharpy.Core/Dict.cs:178–190`; set has no throw; it is in the roster because anchor part (ii) lists setters) | get 65 B, two `newobj KeyError` inline; set 62 B, calls `Dictionary<,>.set_Item`/`get_Count` | get: `[NoInlining] KeyNotFound<K>(key)`; set: body unchanged — conformant once `Dictionary<,>.set_Item`/`get_Count` are in the allow-set (D5(c)) |
  | 14 | `Bytes.this[int]` | constant-message throws inline | fast path + helper |
  | 15 | `FrozenDict<TKey,TValue>.this[TKey]` get (`src/Sharpy.Core/FrozenDict.cs:153–171`; the same shape as row 13 over an `ImmutableDictionary<,>` backing store (`FrozenDict.cs:46`) — `frozendict` is a builtin, `BuiltinRegistry.cs:222`, so `fd[k]` is a direct hot call) [ADDED: verify round 1; backing store corrected in round 2] | 65 B, two `newobj KeyError` inline, calls `ImmutableDictionary<,>.TryGetValue` | `[NoInlining] KeyNotFound<K>(key)` (the same helper as row 13) |
  | — | tuple `t[i]` | **N/A** — compile-time `.ItemN` (collection_types.md:363), no runtime accessor exists. R-KA's contract sentence names "tuple"; it resolves to this N/A row by that spec fact — not a door (nothing to design); **owner acknowledged 2026-11-03 (plan approval, this session): the tuple wording in R-KA / row 6c⁶″ resolves to this N/A row** | — |
  | — | `IDict.this[object]` / `IDictionary.this[object]` / `IList<T>.this[int]` / `IReadOnlyList<T>.this[int]` / `IList.this[int]` / `IList.this[object]` (`Dict.IDict.cs`, `Dict.Interfaces.cs`, `List.Interfaces.cs:81,94,136`, `Partial.List/List.IList.cs:9`) | **N/A** — explicit interface implementations (private CLR members) reached only by interface dispatch, never a direct hot call; excluded from anchor part (ii) by construction (not public) | — |
  | — | the interface **declarations** `Sharpy.IDict.this[object]` (`src/Sharpy.Core/IDict.cs:33`) and `Sharpy.IList.this[object]` (`src/Sharpy.Core/IList.cs:32`) — public abstract getters + setters on public interfaces [ADDED: verify round 4] | **N/A** — abstract members have no body to inline; anchor part (ii) skips `IsAbstract` members, and these two are listed literally so the skip is not an open exemption (a NEW abstract indexer is still reported by the literal-vs-found diff) | — |
  | — | `StringExtensions.ResolveItem` / `StringExtensions.ResolveFieldValue` (`StringExtensions.Format.cs:300,452,465,484` — `"{0[5]}".format(...)` item access) | **N/A** — `str.format` runtime field access, not an element-access emit route; these two method names are the literal N/A entries for anchor part (iii) | — |
  | — | `List<T>.this[System.Index]` / `[System.Range]` (`List.Slicing.cs:25,31`) | **N/A** — C# list-pattern surface; `GetOffset` + the BCL indexer (throws `ArgumentOutOfRangeException`, never `IndexError`); no Python route | — |
  | — | `List<T>.this[int,int]` / `[int,int,int]` / `GetSlice` / `SetSlice` / `DeleteSlice` (`List.Slicing.cs:81,90,153,277`) | **N/A** — slice access, not element access; they reach `Slice.Normalize` (row 6) and are outside anchor part (ii) (two/three parameters) by construction; listed literally so a future one-parameter overload is red | — |

  Axes: accessor (the 15 rows) × direction {in range, negative in range, negative out of range,
  positive out of range} × route {`xs[i]` indexer read, `xs[i] = v` write, `xs[i] += v` compound (read + write through the
  same accessor), `range`-loop `GetItemUnchecked`, `xs[a:b]` slice (rows 6 via `GetSlice`),
  `pop`/`insert`/`index` (`del` is SPY0144 — row 9 has no Sharpy route)} [CORRECTED: verify round 4 —
  compound and slice were unnamed routes]. **Empty cells, literal** [ADDED: verify round 5]: row 3
  has no negative direction (the `NativeUnchecked` route is emitted only for a provably non-negative
  index, `SemanticInfo.cs:3048`); rows 13 and 15 have no negative direction (key lookup, not
  position); rows 12 and 14 have no write or compound route (no setter); row 3 has no write or compound
  route (`GetItemUnchecked` is read-only — a write or compound on the same index goes through rows
  1/2, `RoslynEmitter.Expressions.Access.cs:2279`); row 15 has no write or compound route
  (`fd["x"] = 3` is refused, SPY0320) [ADDED: verify round 6]; rows 5 and 9 have no Sharpy
  route; `SetSlice`/`DeleteSlice` have no Sharpy route (SPY0225). Every other (row with a route) ×
  direction × route cell is a line of the baseline. the second and third axes are the
  **behavioural** matrix (unchanged by construction, pinned by the fixture — every cell measured in
  Current State), the first is the **shape** matrix (the guard). **Generator (§11/§12(b)):** the shape
  axis is enumerated by reflection inside the guard (the roster is a literal, the anchor derives the
  universe from the assembly — the guard IS the generator over accessors); the compared behavioural
  surface (values, exception types) is generated by the existing `DifferentialExecutionTests`
  corpus; the hand fixture covers only the axis no generator can reach — the DEVIATION message
  texts, which python3 cannot oracle. The charter ruled light with this matrix in view (R-KA, row
  6c⁶″); **owner confirmed 2026-11-03 (plan approval, this session): light stands — the hand fixture
  is the §11 exception for the text axis, the guard and the differential corpus are the generators.**
- **Lane / reach:** tooling (performance) · program; **fidelity-scope row:** none touched — no
  Python-visible surface changes; the message texts are the `collection-error-message-texts` ledger
  row (`docs/deviations.yaml:2027`, DEVIATION by default, R-CI) and stay byte-identical.
- **Standing harness that should have caught it:** none — a performance class; `DifferentialExecutionTests`
  is the behavioural control. This plan adds `ElementAccessInlinabilityConformanceTests` (Core.Tests).
- **Generator:** not required — the shape axis is a closed literal roster of 15 methods under ONE
  predicate; the behavioural axes are 4 × 3 but every cell is one line of one fixture whose expected
  output is the measured baseline above (no combinatorial growth, no allowlist).
- **Owner rulings:** R-KA (#2404 batching comment, 2026-10-31; charter §4): (a) one light plan, two
  staged tasks — (1) the throw-helper pattern at every roster accessor; (2) direct backing-storage
  indexing (`CollectionsMarshal.AsSpan` under `#if NET10_0_OR_GREATER`, netstandard2.1 keeps (1)) **only
  if** the task-1 kernel stays > 1.3× BCL. Rejected: fold into P45; task 1 only. R-KK: the #2365
  chore rides this gate. Do not re-ask.

## Adversarial Review (pre-mortem)

- **How the fix could be inert:** (i) the accessor is restructured but still calls a helper that is
  NOT `[NoInlining]` and still holds the interpolation — the JIT then inlines the helper back into the
  accessor and the hot block regains its calls; the guard's recursive callee rule (D5) and mutation M2
  catch it. (ii) `Pop` keeps its `try/catch` — an EH clause blocks inlining regardless of the throw
  shape; the guard asserts `ExceptionHandlingClauses.Count == 0`. (iii) the fast path is written but
  a second copy of `Normalize` survives behind another name and one caller keeps using it, or an
  indexer outside the `Sharpy.Index` family (the `dict`/`str`/`bytes` rows) is left with its inline
  throw — the three-part totality anchor (D5: `Sharpy.Index` referrers + every public one-parameter
  indexer on a public Core type + every `newobj IndexError` site is a roster row or a literal N/A
  row) catches both; verify round 1 found `FrozenDict.this[TKey]` precisely because the anchor's first
  draft reached only the `Sharpy.Index` referrers. (iv) The kernel is re-measured against a **Debug** Core
  (`IsJITOptimizerDisabled=true` — the JIT compiles its methods with MinOpts and the numbers say
  nothing): the benchmark project references the Core **project** in Release and prints the
  `DebuggableAttribute` state on every run; a reading with `IsJITOptimizerDisabled=True` is discarded.
- **Blast radius:** `Sharpy.Core.Tests` (the new guard + `Partial.ListTests/ListTests.{GetItem,SetItem,Pop,DelItem}.cs`,
  `ArrayHelpersTests`, `FrozenDictTests`, `KeyErrorTextTests`, `StrFormatItemAccessTests`), `Sharpy.Stdlib.Tests` (`Heapq`, `Functools`,
  `Statistics`, `Itertools`, `Re` (`ReModule.cs:754–797`) call `GetItemUnchecked`; `Yaml/CommentedSeq` calls `DeleteAt`; `Textwrap`/`Fnmatch` call
  `StringHelpers.GetItem` — API unchanged, bodies re-executed), `Sharpy.Compiler.Tests` (every fixture
  that prints an `IndexError`/`KeyError`, `DifferentialExecutionTests`, `ILCompilesPropertyTests` — the
  #2365 chore lives there), `Sharpy.Cli.Tests`, LSP parity (whole gate); the **netstandard2.1** Core
  build (`release.yml`; LangVersion 9 — the helpers use no C# 10+ syntax; `CollectionsMarshal` only
  under `#if NET10_0_OR_GREATER`). Generated artifacts: **none** — the compiler is untouched, so the
  spy-stdlib C#, spy-test C#, stdlib docs and oracle ledger gates are expected to report no diff at
  `/push`; a diff there is a finding.

## Design Decisions

- **D1 — ONE seam, rung 2 (Core C#).** `Sharpy.Index` (internal) is the single place negative-index
  normalisation lives, and stays so. The 4-argument `Normalize(i, max, forSlice, forInsertion)` is
  **deleted** and replaced by two mode-free methods — strict
  `internal static int Normalize(int i, int max) => (uint)i < (uint)max ? i : WrapOrThrow(i, max);`
  (IL ≤ 16 B — the JIT's always-inline band) and
  `internal static int Clamp(int i, int max)` (today's slice/insertion branch: wrap negatives, clamp
  to `[0, max]`, no throw) — plus
  `[MethodImpl(MethodImplOptions.NoInlining)] internal static int WrapOrThrow(int i, int max)`
  (returns `max + i` for `-max <= i < 0`, otherwise `throw new IndexError($"list index {i} out of range")`
  — the text verbatim). The bool flags were constants at every call site; each of the 8 callers picks
  `Normalize` or `Clamp` by the constants it passed today (rows 4–10). `IndexExtensions.ToNormalizedInt32`
  keeps its **public** signature (`RangeExtensions.ToSlice` and C# users call it) and dispatches
  `forSlice || forInsertion ? Clamp : Normalize`. *Rungs:* rung 1 (`.spy`) cannot carry a JIT-shape
  fact about a C# type; rungs 3/4 (compiler) are not involved — the emitter already names these
  accessors and does not change.
- **D2 — the uniform fast-path shape (the invariant the guard checks).** Every **position-keyed**
  roster accessor reads
  `if ((uint)index < (uint)count) { <direct backing access> } else { <slow path via a [NoInlining] helper> }`;
  the two **key-keyed** rows (13, 15) read `if (_dict.TryGetValue(key, out v)) return v; throw
  Index.KeyNotFound<K>(key);` (plus the null-key branch they have today — `IsNullKey(key)` on `Dict`, a bare `key is null` on `FrozenDict`) — the fast path is the lookup,
  the slow path the `[NoInlining]` helper [CORRECTED: verify round 5];
  the slow path may be `return storage[Index.WrapOrThrow(i, n)]` (list/array/string/bytes) or
  `throw Index.OutOfRange(i)` (a helper that **returns** the exception, for `GetItemUnchecked` where no
  wraparound is admitted — the `throw` instruction stays in the accessor, as in today's inlining
  `ArrayHelpers.GetItem`). One `[NoInlining]` helper **per message family** is fine (`list index {i}
  out of range`, `array index out of range`, `string index out of range`, `index out of range` (bytes),
  `pop index {i} out of range`, `pop from empty list`, `KeyError(key)`, `{x} is not in list`); the
  message texts are copied verbatim — this group changes no observable behaviour (R-KA: two-way,
  Core-internal). Helpers live on `Sharpy.Index` (internal; `InternalsVisibleTo` Core.Tests already)
  unless a family is type-generic (`KeyNotFound<K>` on `Dict`).
- **D3 — `Pop` loses its EH clause.** `catch (IndexError)` + rethrow exists only to swap the message;
  an EH clause blocks inlining outright. New body: empty → `throw Index.PopFromEmpty()`; `j = i < 0 ?
  i + n : i`; `(uint)j >= (uint)n` → `throw Index.PopOutOfRange(i)` **with the original `i`** (today's
  message names the un-normalised index: `pop index -7 out of range`, baseline above); then
  `_list[j]` + `RemoveAt(j)`.
- **D4 — task 2 is measured, and lands only if it wins.** R-KA's gate (task-1 kernel > 1.3× BCL)
  is met on this machine (1.6–1.75×), **but both `AsSpan` shapes measured at planning do not beat
  task 1** (S1 4.05 ms ≈ E 3.55–3.96 ms; S2 6.1 ms, worse). Predicate: after task 1 lands, the
  implementer measures the real `Sharpy.List<T>` kernel with the `#if NET10_0_OR_GREATER` span path on
  the **get** fast path only (set keeps `_list[index] = value` — the BCL setter's `_version` bump is
  observable through BCL enumerators and stays), arms interleaved A/B/A/B, ≥ 4 rounds; task 2 lands
  only if it is faster than task 1 by more than the 15 % single-sequence floor (`benchmarks/BASELINE.md`)
  in both orders — **the margin is this plan's decision, not R-KA's** (R-KA rules only the 1.3× BCL
  trigger; the floor is the repo's standing rule for what counts as a measured delta, so a task-2 win
  inside it is "unmeasured", not a win) [ADDED: verify round 3; **owner accepted the 15 % margin 2026-11-03 (plan approval, this session)**]. Otherwise the close-out records "task 2 measured, not taken" with the numbers on
  #2404 — a measured outcome of R-KA's conditional, not a deviation from it. Task 2 never changes the
  netstandard2.1 body.
- **D5 — the guard: `ElementAccessInlinabilityConformanceTests` (Core.Tests), a static IL-shape scan.**
  R-KA's guard sentence admits either "IL body under the JIT inline budget **or** `AggressiveInlining`
  present"; this plan takes the **narrower** rule — the IL shape alone, no `AggressiveInlining` on any
  roster accessor — because the attribute only raises the JIT's willingness and does not remove the
  out-of-line construction the class is about; an accessor that needs the attribute to pass is a
  finding, not a pass [ADDED: verify round 6 — the plan's own tightening of R-KA, within the ruling].
  Why static: the gate runs the Debug Core (`DebuggableAttribute.IsJITOptimizerDisabled` — no inlining
  to observe), `DOTNET_JitDisasm` is read at JIT start-up (not settable in-process), and the only
  faithful instrument is a Release-Core child process — that is the **commit-body evidence procedure
  (D8)**, not a standing test. The scan walks each roster method's IL with an opcode table built from
  `System.Reflection.Emit.OpCodes` (operand sizes per `OperandType`; `0xFE` two-byte prefix) and asserts
  per method: (a) `ExceptionHandlingClauses.Count == 0`; (b) no `newobj` of an `Exception`-derived type, no `ldstr`, no call into
  `DefaultInterpolatedStringHandler`/`String.Concat`/`String.Format` [CORRECTED: verify round 2 — a
  flat `newobj` ban reds row 6, whose `(int, int)` return is `newobj ValueTuple`]; (c) every
  `call`/`callvirt` target is either in the literal BCL allow-set — **the complete set, read off the
  roster bodies** [CORRECTED: verify round 1 — `RemoveAt`/`Insert`/`IndexOf`/`Math.*` were missing, so
  rows 5–10 would have been red at landing]: `List<T>.get_Item/set_Item/get_Count/RemoveAt/Insert/
  IndexOf(T,int,int)`, `String.get_Length/get_Chars`, `Char.ToString`, `Dictionary<,>.TryGetValue/
  set_Item/get_Count` (row 13 set), `ImmutableDictionary<,>.TryGetValue` (row 15), `System.Index.get_IsFromEnd/
  get_Value` (row 5), `Array.Empty<T>` (row 14, through `Bytes.get__data`, `Bytes.cs:21`),
  `Math.Min/Math.Max` (reached through `Index.Clamp`), `CollectionsMarshal.AsSpan` + `Span<T>.get_Item`/
  `get_Length` only if task 2 lands [CORRECTED: verify round 2 — the set/ImmutableDictionary/System.Index/
  Array.Empty members were read off the roster bodies in the Release DLL] — or a Sharpy.Core method
  that is itself conformant (recursive, cycle-safe; `Dict.IsNullKey` (10 B), `Index.Clamp` (23 B
  Release) and `Bytes.get__data` (16 B) are the three such callees today) or carries
  `MethodImplOptions.NoInlining`; (d) **IL length ≤ 100 B under Release (the JIT's
  `DEFAULT_MAX_INLINE_SIZE` candidate limit) and ≤ 128 B under Debug**, the ceiling chosen at runtime
  from the Core assembly's `DebuggableAttribute.IsJITOptimizerDisabled` [CORRECTED: verify round 1 —
  the first draft's uniform 64 B was measured red on rows 7, 10 (both configurations) and 13 (Debug)].
  Measured sizes of the D2/D3 shapes @ 89fba5eec (Release / Debug; scratchpad `ilshape/Program.cs`,
  `LangVersion 9`): strict `Normalize` 14/15, `Clamp` 23/32, list get 29/34, set 30/32,
  `GetItemUnchecked` 34/46, `Pop` 68/92, `Insert` 30/32, `DeleteAt` 29/31, `Index(T,int,int)` 71/95,
  `ArrayHelpers.GetItem` 30/41, `SetItem` 32/39, `StringHelpers.GetItem` 52/63, `Dict` get 55/71,
  `Bytes` get 42/53, `BadShapeControl` 61/73, `GoodShapeControl` 14/25 — the landing commit records
  the real roster's sizes next to these. (d) is a secondary rule: an interpolation adds ≥ 45 B, which
  (d) catches on the large rows and (b) (`newobj`/`ldstr`) catches on every row. **Totality anchor
  (three parts, each a literal placement check)** [CORRECTED: verify round 1 — the first draft's single
  `Sharpy.Index`-referrer rule reached rows 1–10 only, which is how row 15 was missed]: every method in
  `Sharpy.Core` that (i) references any `Sharpy.Index` member, or (ii) is a **public key indexer declared on a public Core type — a `get_Item` with
  one parameter or a `set_Item` with two (key, value), **non-abstract** (an interface declaration has
  no IL; the two Core declarations `src/Sharpy.Core/IDict.cs:33` / `src/Sharpy.Core/IList.cs:32` are N/A rows and the test asserts the
  abstract set it skipped equals that literal, so a new abstract indexer is still reported)
  [CORRECTED: verify round 4]** [CORRECTED: verify round 2 — "one-parameter" never reached a setter] (reflection over `Assembly.GetExportedTypes()`; explicit interface
  implementations are private and excluded by construction) — **parts (i) and (iii) and the helper
  attribute check enumerate `Assembly.GetTypes()` with `BindingFlags.Public | NonPublic | Static |
  Instance | DeclaredOnly` (nested types included), because `Sharpy.Index` (`src/Sharpy.Core/Index.cs:3`),
  `Slice.Normalize` (`Slice.cs:68`) and the helpers are internal or private** [CORRECTED: verify
  round 3 — under exported-only enumeration M3, M8 and M9 could never go red], or (iii) holds a
  `newobj IndexError::.ctor` in its IL, is in the roster literal, in the literal N/A list (each N/A
  row carries its reason from the Defect Class table), **or — for (iii) only — is a
  `[MethodImpl(NoInlining)]` throw helper on `Sharpy.Index` named in the test's helper literal
  (`WrapOrThrow`, `OutOfRange`, `PopOutOfRange`, `PopFromEmpty`, `ArrayOutOfRange`, `StringOutOfRange`,
  `BytesOutOfRange`; `NotInList<T>` and `KeyNotFound<K>` hold `ValueError`/`KeyError`, trip no
  clause, and are listed for completeness only)** [ADDED: verify round 2 — without this category the
  cure's own helpers red the anchor]; the helper literal is itself checked both ways: every named helper carries `NoInlining`
  (red if the attribute is dropped — M2/M8), and every `NoInlining` Core method holding
  `newobj IndexError` is in the literal (red if one is added unnamed — M9). (ii) adds
  `List<T>.this[System.Index]`/`[System.Range]` (N/A) and `Dict.set_Item` (row 13); (iii) adds
  `StringExtensions.ResolveItem`/`ResolveFieldValue` (N/A) — no other Core method trips (i)–(iii)
  @ 89fba5eec (round-2 DLL scan + `grep -rn 'new IndexError\|Index\.Normalize\|public .* this\['`).
  `KeyError` construction is deliberately NOT an anchor: its non-indexer sites (`Dict.Pop`/`Remove`/
  `PopItem`, `Set.Pop`/`Remove`, `str.format` field lookup, the explicit `IDict`/`ISet` members) are
  not element access; the dict family is anchored by (ii). A new `Normalize` caller, a new public
  indexer, or a new `IndexError` site is red until it is placed. **Positive controls** (contract §2, the
  absence assertions): the test class carries `BadShapeControl(int)` (an inline interpolated throw —
  reported by (b): `newobj IndexError` + `ldstr` + handler calls; at 61 B Release it is under the (d)
  ceiling, so (b) alone catches it), `BigShapeControl(int)` (no `newobj`/`ldstr`, 40 straight-line
  integer ops on the parameter — **measured 179 B Release / 266 B Debug** (`ilshape/Program.cs`, round
  3), above both ceilings, reported by (d) alone) [ADDED: verify round 2; measured round 3],
  `EhShapeControl(int)` (the good shape wrapped in `try { } catch (IndexError) { throw; }` — reported by
  (a) alone) and `CallShapeControl(int)` (the good shape plus `System.Console.Out.Flush()` — reported
  by (c) alone) [ADDED: verify round 4 — every clause now has its own control] and `GoodShapeControl(int)` (the D2 shape whose slow path is `Index.WrapOrThrow` — a Core
  `[NoInlining]` helper, visible through `InternalsVisibleTo`; `EhShapeControl`/`CallShapeControl`
  wrap the same body, so no control holds `newobj IndexError` itself and none would be reported by
  anchor part (iii) under any universe) [CORRECTED: verify round 5]; one `Fact` asserts the predicate
  REPORTS each bad control **with its own clause among the reported clauses** (`BadShapeControl` is
  reported by (b) and also by (c), since the interpolation lowers to BCL calls outside the allow-set;
  `Big`/`Eh`/`Call` are reported by (d)/(a)/(c) **and no other**) and PASSES the good one with no clause,
  so a scanner that stops decoding is red. **Anchor positive control** [ADDED: verify round 3; re-specified round
  4]: the scan is a function of an explicit type universe (`IEnumerable<Type>`) that applies the member
  `BindingFlags` itself; the Core run passes `coreAssembly.GetTypes()` to parts (i)/(iii) and
  `coreAssembly.GetExportedTypes()` to part (ii); within a universe, (ii) filters at the **member**
  level (`MethodInfo.IsPublic`, non-abstract) and never on `Type.IsPublic` (false for a public nested
  type such as `PlantedHolder`) [ADDED: verify round 5]. The test class carries a **public** nested type
  `PlantedHolder` (public so (ii)'s exported universe can see it — a private nested type cannot be
  exported) with a **private** method holding `newobj IndexError`, a **private** method calling
  `Index.Normalize`, and a public one-parameter `this[int]`; one `Fact` runs the three parts over the
  universe `{ typeof(PlantedHolder) }` and asserts each part reports exactly its planted member (the
  private members prove the member flags include `NonPublic`; the other test-class members —
  `BadShapeControl`, `GoodShapeControl.Slow`, the formatting tests' nested indexer — are outside that
  universe by construction). A second `Fact` pins the **type-level** enumeration literally: the Core
  (i)/(iii) universe contains `typeof(Sharpy.Index)` (internal) and the (ii) universe contains
  `typeof(Sharpy.List<>)` but not `typeof(Sharpy.Index)`.
  No allowlist (the roster is the contract; nothing is known-red at landing).
- **D6 — the behavioural pin.** Fixture
  `src/Sharpy.Compiler.Tests/Integration/TestFixtures/collections/element_access_messages_2404.spy`
  + `.expected` = `.claude/tmp/p76/element_access_messages_2404.{spy,expected}` copied verbatim (the
  measured baseline in Current State: every roster row with a Sharpy route × every direction × the
  routes). It pins "unchanged by construction" at the Sharpy level; its positive control is a
  message-text mutation in Core (D9 M4). The program is functions + `main` (the entry point requires
  it: SPY0340/SPY0403) and uses the `array[int]` and `frozendict` names, which the
  `DifferentialExecutionTests` shared-subset filter rejects (`src/Sharpy.Compiler.Tests/Properties/Differential/DifferentialExecutionTests.cs:1598`),
  so its DEVIATION message texts never reach the python3 arm — the implementer reads the sweep's
  exclusion report line for this fixture once (shape id `sharpy-only-call:explicit-type-arguments`,
  `src/Sharpy.Compiler.Tests/Properties/Differential/DifferentialExecutionTests.cs:1598`, the name set at `:1328`) and records it in the commit body.
  The `.expected` holds program stdout only (`FileBasedIntegrationTests` compares stdout; the
  `Successfully compiled` / `=== Running Program ===` lines are `sharpyc run`'s own header). `DifferentialExecutionTests` stays the python3 control for the compared surface over
  its own corpus (values, exception types).
- **D7 — the benchmark is committed, not attached.** `benchmarks/core-element-access/` (a console
  project **outside** `sharpy.sln`, `ProjectReference` to `Sharpy.Core.csproj`, `Optimize=true`): the
  kernel in its variants — Sharpy indexer, `GetItemUnchecked`, BCL `List<T>`, `int[][]` via
  `ArrayHelpers`, raw array — plus a `README.md` with the two commands: timing
  (`DOTNET_TieredCompilation=0 dotnet run -c Release -- <variant> 30`, through
  `.claude/scripts/dotnet-serialized` for the build) and disassembly
  (`DOTNET_JitDisasm='MatMul*' DOTNET_JitStdOutFile=<file>`; count `call`/`blr` lines in the hot
  block — arm64 spells the indirect call `blr`). The program prints the Core assembly's
  `IsJITOptimizerDisabled` state on every run (Adversarial Review iv). **No-difference control**
  [ADDED: verify round 1]: before any before/after or A/B is read, the same variant is run twice
  interleaved and the two medians must land inside the 15 % floor (`benchmarks/BASELINE.md:31`) —
  otherwise the machine is noisy and the reading is discarded.
- **D8 — what the commit bodies record.** Task 1: the kernel before/after on the same machine, same
  session (today 13.2 ms → expected ≈ 3.6–4.0 ms here; the owner's container 12.9 → 8.8), the hot-block
  call count (8 → 0) for variants A and B, the IL sizes of every roster method Debug/Release, and the
  three guard mutations (D9). Task 2 (or its not-taken record): the interleaved A/B numbers.
- **D9 — mutations (every one recorded "broken → red (N failed), restored → green" in the commit
  body; restore from a copy, never from git):** **M1** put
  `throw new IndexError($"list index {i} out of range")` back on `Index.Normalize`'s fast path → the guard
  reds rows 1, 2, 4, 9 (newobj/ldstr/handler calls; size); **M2** delete `[MethodImpl(NoInlining)]`
  from `Index.WrapOrThrow` → the guard reds **rows 1, 2, 4, 5, 9** (row 4 by (c) directly — a
  non-`NoInlining` callee holding `newobj IndexError`; rows 1, 2, 5, 9 because strict `Normalize` is
  then non-conformant; the per-family helpers of rows 11, 12, 14 are separate methods and stay
  green), the helper-literal attribute check, **and the three good-shaped controls (`Good`/`Eh`/`Call`
  call `WrapOrThrow`, so each gains a clause (c) report — the controls Fact reds too)** [red set
  named: verify round 5; controls added round 6]; **M3** add a private Core method that calls
  `Index.Normalize` without a roster row → the totality anchor reds; **M4** change one message text
  (`"list index "` → `"list idx "`) → the fixture reds (the positive control that the fixture reads
  Core's text), restored → green; **M5** (chore) remove the #2365 draw filter → the three seeds red;
  **M6** [ADDED: verify round 1] put `throw new KeyError(key)` back inline on `Dict<K,V>.this[K]` get
  → row 13 red by (b) (`newobj` of an `Exception` type); **M6b** [ADDED: verify round 2] the same on
  `FrozenDict<TKey,TValue>.this[TKey]` → row 15 red; **M7** [ADDED: verify round 1] declare
  `public int this[int i] => i;` on a Core type with no roster row → anchor part (ii) red; **M7b**
  [ADDED: verify round 2] give that indexer a throwing setter (`set => throw new IndexError("x")`)
  with the getter rostered → (ii) red on the setter; **M8** [ADDED: verify round 2] drop `NoInlining`
  from a named helper other than `WrapOrThrow` (e.g. `PopFromEmpty`) → the helper literal's attribute
  check red (and row 7 via (c)); **M9** [ADDED: verify round 2] add a private `[NoInlining]` Core method
  holding `newobj IndexError` that is not in the helper literal → anchor part (iii) red. **M10** [ADDED: verify round 3] put the `try { … } catch (IndexError)` back in `Pop` → row 7 red
  by (a) (`ExceptionHandlingClauses.Count == 1`); **M11** [ADDED: verify round 3] add
  `System.Console.Out.Flush()` to the fast path of `List<T>.this[int]` get → row 1 red by (c) (an
  unlisted BCL call); **M13** [ADDED: verify round 5; made buildable round 6] add `int this[int i] { get; }` to
  `Sharpy.IList` **together with** its explicit implementation `int IList.this[int i] => 0;` in
  `Partial.List/List.IList.cs` (`List<T>` implements the interface, so the declaration alone is CS0535)
  → the new abstract member makes anchor part (ii)'s skipped-abstract set differ from its literal →
  red; the private explicit implementation trips nothing;
  **M12** [ADDED: verify round 3; CORRECTED round 4 — a dead-local pad is folded to 2 B in Release]
  pad `GetItemUnchecked` with `BigShapeControl`'s 40 ops threaded through the `index` parameter
  (live, returned; no `newobj`/`ldstr`/calls) → row 3 red by (d) alone (~34 → ~180 B Release, ~270 B
  Debug; the same op list measured 179/266 in `BigShapeControl`).
  **M1 reddens rows 1, 2, 4, 5 and 9** (row 5 `ToNormalizedInt32` reaches strict `Normalize` in
  non-slice mode) [CORRECTED: verify round 2]. Ordering: the #2365 chore commit lands first on this gate, so M5 is
  recorded on that commit; the `#2365` filter does not exist @ 89fba5eec.
- **D10 — no spec or ledger change.** `collection_types.md:43` (`list[i]` throws `IndexError`) and
  `deviations.yaml:2027` stay as they are; the plan adds no spec block. `#2194` keeps the codegen side
  (`range` loops as `foreach`, `ArrayHelpers` on arrays); this plan does not touch the emitter.

## Acceptance

- **Shape matrix goes green:** `ElementAccessInlinabilityConformanceTests` passes over the 15-row
  roster with the 6 N/A rows declared, totality-anchored by all three parts of D5 (every `Sharpy.Index`
  referrer, every public one-parameter Core indexer, every `newobj IndexError` site placed), no
  allowlist; its positive-control `Fact` reports `BadShapeControl` and passes `GoodShapeControl`.
  Evidence: `Sharpy.Core.Tests` green @ landing sha; mutations **M1, M2, M3, M6, M6b, M7, M7b, M8, M9, M10, M11, M12, M13** (one per clause (a)–(d),
  per anchor part and for the abstract skip) recorded broken → red / restored → green in the task-1 commit body; its
  positive-control `Fact`s report `BadShapeControl` by (b), `BigShapeControl` by (d), `EhShapeControl`
  by (a), `CallShapeControl` by (c), the three `PlantedHolder` members by anchor parts (i)–(iii), and
  the type-level universes contain/exclude `Sharpy.Index` as pinned; the per-row IL sizes (Release / Debug) recorded
  beside the D5 table.
- **Behavioural matrix unchanged:** fixture `collections/element_access_messages_2404` (`.spy` +
  `.expected` = the baseline output measured @ 89fba5eec) passes; mutation **M4** recorded; the
  existing `src/Sharpy.Core.Tests/Partial.ListTests/ListTests.{GetItem,SetItem,Pop,DelItem}.cs`, `ArrayHelpersTests`,
  `FrozenDictTests`, `KeyErrorTextTests` (its `frozendict[str]` cell), `StrFormatItemAccessTests` stay green;
  `DifferentialExecutionTests` green in the whole-solution gate.
- **The defect is gone, measured (D7/D8):** with the Release Core at the landing sha, the committed
  kernel's hot block shows **0** calls to any `Sharpy.*` method for the Sharpy-indexer and
  `GetItemUnchecked` variants (today 8 each), `IsJITOptimizerDisabled=False` printed, and the ms per
  multiply for both variants recorded before/after on the same machine in the commit body (A: 13.2 ms
  @ 89fba5eec here; the owner's 12.9 → 8.8 ms is the container figure). A reading with the Debug Core
  is not evidence.
- **Task 2 verdict recorded (D4):** either the span get-path lands with the interleaved A/B numbers
  (faster than task 1 by > 15 % in both orders) and the guard's allow-set gains the span members, or
  the task-2 commit is absent and #2404 carries the "measured, not taken" comment with the numbers.
- **Roster totality and the N/A rows:** the 6 N/A rows (tuple, the explicit-interface indexers,
  the abstract interface declarations, `StringExtensions.ResolveItem`/`ResolveFieldValue`, the C#
  `System.Index`/`System.Range` indexers, the slicing indexers/methods) are literal entries with their reasons in the test; the test is red
  if any method trips anchor part (i), (ii) or (iii) without a placement.
- **Chore #2365 (R-KK):** one commit, `ILCompilesPropertyTests` reports "n skipped (#2365)" for the
  `None`-literal-receiver shape and FAILS the first time such a draw compiles; mutation **M5** recorded;
  a second shape is not skipped (positive control).
- **Gate:** the whole solution,
  `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`,
  counts reported `passed/failed/skipped @ sha (measured)`; the netstandard2.1 Core build green
  (`dotnet build src/Sharpy.Core -f netstandard2.1` through the wrapper); `/push` staleness gates
  report no diff (D10 — a diff is a finding).
- **Blast-radius projects re-executed:** `Sharpy.Stdlib.Tests` (the `GetItemUnchecked`/`DeleteAt`/
  `StringHelpers.GetItem` callers), `Sharpy.Compiler.Tests` fixtures printing `IndexError`/`KeyError`,
  `Sharpy.Cli.Tests`, `Sharpy.Lsp.Tests` — all inside the one gate.

## Issues to Close

- #2404 — `Sharpy.List<T>` element access never inlines — closed by this plan; **close criterion:**
  the guard green with M1–M3, M6, M6b, M7, M7b, M8–M13 recorded; the fixture green with M4 recorded; the kernel's hot block at
  0 Sharpy calls with the Release Core @ landing sha and the before/after ms recorded; the task-2
  verdict (landed or measured-not-taken with numbers) posted on the issue; the whole-solution gate
  count @ sha. The charter §2 table gains the "cold-path construction inline on a hot path" row at
  close-out.
- #2365 (chore, R-KK) — **not closed**: the draw filter is a pin that drains with P17's not-found
  arm; the chore commit sha and M5 record are posted as a comment on #2365.

## Class trackers this plan may post cells to

- #2404 — Core element-access inlinability (this class; **it is the tracker**) — an accessor outside
  the roster found non-conformant, or a Core hot path with an inline interpolated throw outside
  element access (e.g. `StringExtensions.Format`, `Bytes` slicing), goes here as a cell comment.
  **Pre-known cells outside R-KA's Core contract, for the lead to post at close-out (read @ 89fba5eec,
  verify round 1):** the Stdlib indexers `Collections/Collections.cs:771` (`DefaultDict.this[TKey]`),
  `Collections/Collections.cs:315` (`this[int]`), `Collections/OrderedDict.cs:57`, `Collections/ChainMap.cs:87`,
  `Yaml/CommentedSeq.cs:42`, `Xml/Element.cs:254`, `Sqlite3/Row.cs:28` — same shape class, Stdlib
  rung; not this plan's work.
- #2194 — codegen-side element access (P45: `range` loops as `foreach`, `ArrayHelpers` on arrays,
  the `GetItemUnchecked` route's reach) — a cell where the EMITTED route, not the Core body, leaves
  a call on the hot path.
- #2365 — the `None`-receiver shape (P17's receiver-kind axis) — the chore's seeds and any new
  `None`-receiver draw.

No implementer runs `gh issue create`; cells outside these three go to the findings ledger for the
lead to file by class (contract §10).

## Verification Summary — round 1 (superseded by the revision log below; the fresh pass re-stamps)

**Result:** NEEDS REVISION
**Adequacy:** CELL
**Verified on:** 2026-11-03 @ 89fba5eec
**Plan file:** .claude/plans/plan-460cbf.md
**Mode:** `--adequacy` (tier: light) — no inline corrections; the author fixes the plan. `check_plan_refs.py --issues`: RESULT OK (3 paths, 4 file:line, 4 issues; the 18 "not found" symbols are new helpers/guard names or BCL names; #1052 CLOSED is cited only as `GetItemUnchecked`'s origin, which the Core comment confirms).

### Adequacy
- **Contract:** named — matches R-KA's own words on #2404 ("every Sharpy.Core element accessor on a hot path … the `Dict`/tuple/`str` indexers").
- **Matrix:** larger than the repro list (14 rows + 4 N/A vs the issue's 2), BUT one arm is missing. The touched dispatch sites were listed from the code: the 8 `Index.Normalize` call sites (`IndexExtensions.cs:11,14`, `Slice.cs:70`, `List.Methods.cs:91,119,214,215`, `List.Slicing.cs:50,55,258`) — all in rows 1–10 ✓; the emit routes `RoslynEmitter.Expressions.Access.cs:2265` (`StringHelpers.GetItem`), `:2272` (`ArrayHelpers`), `:2279` (`GetItemUnchecked`) — rows 11, 12, 3 ✓; the public one-parameter indexers declared in Core (`grep -E 'public .* this\['`): `List<T>.this[int]`, `List<T>.this[System.Index]`, `Dict<K,V>.this[K]`, `Bytes.this[int]` — rows 1/2, N/A, 13, 14 ✓ — **and `FrozenDict<TKey,TValue>.this[TKey]` get (`src/Sharpy.Core/FrozenDict.cs:153–171`)**, which is the byte-for-byte twin of row 13 (two inline `newobj KeyError`, lines 161 and 169), is reached by a direct hot call (`frozendict` is a Sharpy builtin: `BuiltinRegistry.cs:222` maps `BuiltinNames.FrozenDict` → `Sharpy.FrozenDict<,>`, so `fd[k]` emits this indexer), and is in neither the roster nor the N/A list. **The totality anchor cannot catch it:** the anchor is "every Core method whose IL references a `Sharpy.Index` member", which reaches rows 1–10 only; rows 11–14 (and the missing FrozenDict row) reference no `Sharpy.Index` member, so a new or forgotten indexer in the `Dict`/`str`/`bytes` family is never red. Required: (i) add row 15 `FrozenDict<TKey,TValue>.this[TKey]` get (cure: `[NoInlining] KeyNotFound<K>` as row 13); (ii) widen the anchor so it covers the whole roster — e.g. "every public one-parameter `get_Item`/`set_Item` declared on a public `Sharpy.Core` type, every Core method that references a `Sharpy.Index` member, and every Core method whose IL holds `newobj IndexError::.ctor`" must be a roster row or a literal N/A row with its reason (the `Dict.Methods.cs` pop/Remove/popitem, `Set`, `StringExtensions.Format` and `Dict.IDict`/`Set.ISet` explicit-interface sites then become N/A rows — closed and literal, as the plan already does for 4).
- **Cure:** seam — the ONE `Sharpy.Index` seam (D1) plus the per-family `[NoInlining]` helpers (D2); meta-class "cold-path construction inline on a hot path" (new §2 row, named by the plan) ✓.
- **Harness:** "none; this plan adds `ElementAccessInlinabilityConformanceTests`" ✓; no allowlist ✓. Stdlib siblings outside the Core contract (not plan defects; ledger/tracker cells for #2404): `Sharpy.Stdlib/Collections/Collections.cs:771` `DefaultDict.this[TKey]`, `:315` `this[int]`, `OrderedDict.cs:57`, `ChainMap.cs:87`, `Yaml/CommentedSeq.cs:42`, `Xml/Element.cs:254`, `Sqlite3/Row.cs:28`.
- **Layer:** every decision rung-justified (D1 rung 2; the compiler is untouched — confirmed, no `Semantic/`/`CodeGen/` file in the plan's edit set) ✓.

### Design decisions checked (Dimension 4, design rows — read against the code @ 89fba5eec)
- **D1 holds:** all 8 `Index.Normalize` call sites pass literal bool flags (get/set/DeleteAt/Pop `false,false`; Insert `false,true`; Index ×2 and `Slice.Normalize` ×2 `true,false`; `ToNormalizedInt32` forwards its own parameters), so the strict/`Clamp` split is a per-site constant ✓. `ToNormalizedInt32` is public and called by `RangeExtensions.ToSlice` ✓. `InternalsVisibleTo` Core.Tests at `Sharpy.Core.csproj:30` ✓. The strict `Normalize` body measures **14 B Release / 15 B Debug** (probe below) — the "≤ 16 B" claim holds.
- **D2 holds** for the shapes read: `Dict.this[K]` set has no throw (`src/Sharpy.Core/Dict.cs:178–190`) ✓; `Bytes.this[int]` throws constant messages (`Bytes.cs:94,104`) ✓; `StringHelpers.GetItem` reads `s.Length` twice (`StringHelpers.cs:66–67`) ✓; `ArrayHelpers` holds a duplicated check with `$"array index out of range"` (an interpolation with no holes — `ldstr`, inlines today) ✓. The Dict fast path also calls the private `IsNullKey` (`src/Sharpy.Core/Dict.cs:31`, `key is null`, 10 B) — a conformant callee under rule (c) ✓.
- **D3 holds:** `j = i < 0 ? i + n : i; (uint)j >= (uint)n → throw` rejects exactly `i >= n || i < -n` and wraps exactly `-n <= i < 0`, i.e. today's `Normalize` branch; the message takes the original `i` ✓ (`pop index -7 out of range` per the baseline).
- **D5(d) REFUTED by measurement.** The plan's own D2/D3 shapes were compiled (scratchpad `ilshape/Program.cs`, `LangVersion 9`, net10.0, Debug then Release through the wrapper) and their IL lengths read by reflection: `Pop` **68 B Release / 92 B Debug**, `Index(T,int,int)` **71 / 95**, `Dict.get_Item` **55 / 71**, `StringHelpers.GetItem` 52 / 63, `Bytes.get_Item` 42 / 53, `GetItemUnchecked` 34 / 46, list get/set 29–30 / 32–34, `Clamp` 23 / 32, `BadShapeControl` 61 / 73, `GoodShapeControl` 14 / 25. The uniform "≤ 64 B in the configuration under test" ceiling is **red at landing on rows 7 and 10 in both configurations and on row 13 in Debug (the gate's configuration)**; the "fixed shapes are ~15–40 B" estimate holds only for rows 1–6, 8, 9, 11, 14. Either pin a per-row expected-size table (the measured numbers above, with a tolerance) or set the ceiling to the JIT's candidate limit (100 B, Release) and record Debug sizes without gating them; left as written, the implementer's first edit is to loosen the guard — the exemption becomes the subject (contract §2).
- **D5(c) allow-set is incomplete for the roster it covers:** rows 7, 8, 9 call `List<T>.RemoveAt` / `List<T>.Insert`, row 10 calls `List<T>.IndexOf(T,int,int)`, and `Index.Clamp` (reached by rows 5, 6, 8, 10 and conformant only if its callees are) calls `Math.Min`/`Math.Max`. None is in the literal allow-set (`get_Item/set_Item/get_Count`, `String.get_Length/get_Chars`, `Char.ToString`, `Dictionary.TryGetValue`, `CollectionsMarshal.AsSpan`), so rows 5–10 are red at landing until the set is widened. Name the full set in the plan so widening is a plan fact, not an implementation-time exemption.
- **D5 premise holds:** `Directory.Build.props`, `Sharpy.Core.csproj` and `Sharpy.Core.Tests.csproj` set no `Optimize`, so the gate's Debug Core carries `IsJITOptimizerDisabled=True` (the probe prints the same for its Debug build) — a static IL scan is the right instrument ✓.
- **D4 / R-KA read as the plan states:** comment 6539776539 on #2404 (2026-10-31) rules one light plan, two staged tasks, task 2 only if task 1 stays > 1.3× BCL, (b) and (c) rejected; the charter row 6c⁶″ (§1 table line 33, §3.20g′ line 487) says light ✓. R-KK on #2365 (2026-11-03) matches the chore's seeds and mutation ✓.
- **D10 holds:** `deviations.yaml` row `collection-error-message-texts` (id at line 2018, text through 2033) and `collection_types.md:43` / `:363` say what the plan says ✓.

### Falsifiability
- `ElementAccessInlinabilityConformanceTests` (D5): mutation **named** — M1 (interpolated throw back on strict `Normalize`: 14 B → ~60 B, `newobj` + `ldstr` + 5 handler calls; reds rows 1, 2, 4, 9), M2 (`[NoInlining]` removed from `WrapOrThrow` → every row reaching it), M3 (an un-rostered `Index.Normalize` caller → totality anchor); exemption **not the subject** for (a)–(c), **but (d)'s ceiling IS at risk of becoming one** (see D5(d) above — if the implementer raises it per row to pass, the size rule no longer catches M1 on the larger rows; the plan must fix the numbers first); positive control **yes** (`BadShapeControl` must be reported, `GoodShapeControl` must pass — measured 61/73 B vs 14/25 B, so both (b) and a corrected (d) discriminate); inert-fix path **covered** (M2 is exactly the "helper inlined back" inert fix; M3 the "second `Normalize` survives" inert fix). **Gap:** no mutation reaches rows 11–14 (or the missing FrozenDict row) through the anchor — add **M6**: put a `newobj KeyError` back inline on `Dict.this[K]` get (or `FrozenDict`) → rows 13/15 red; and the widened anchor's mutation: declare a new public `this[int]` on a Core type without a roster row → red.
- Fixture `collections/element_access_messages_2404` (D6): mutation **named** (M4, `"list index "` → `"list idx "` in Core → red); positive control **yes** (M4 is it); discriminating **yes** — every expected line is an exception message or a mutated list; by-direction **n/a** (no new refusal; the fixture pins today's measured output, every cell `worked` at base).
- Chore `ILCompilesPropertyTests` draw filter (#2365): mutation **named** (M5, remove the filter → the three seeds red); exemption **not the subject** (the filter FAILS when a filtered draw compiles, so the skip drains loudly); positive control **yes** (a second shape is not skipped).
- Benchmark `benchmarks/core-element-access/` (D7/D8): not a test; its instrument check is the printed `IsJITOptimizerDisabled` (Adversarial Review iv) ✓ — the plan should also state the **no-difference control** (two runs of the same variant interleaved must land inside the 15 % floor) before an A/B is read, as `benchmarks/BASELINE.md:31` requires.

### Warnings (author action before `/implement-plan`)
1. Add the FrozenDict row and widen the totality anchor (Adequacy — this is what makes the result NEEDS REVISION).
2. Re-pin D5(d) to the measured sizes or the 100 B JIT limit; name the complete allow-set for D5(c) (`List<T>.RemoveAt/Insert/IndexOf(T,int,int)`, `Math.Min/Max`).
3. Add mutation M6 (a `KeyError` back inline on a dict-family indexer) so rows 13–15 have a named red.
4. Spellings only (no correction made in this mode): the Core test is `ArrayHelpersTests` (not `ArrayHelpers_Tests`); `ListTests.GetItem/SetItem/Pop` are three files `ListTests.GetItem.cs`, `ListTests.SetItem.cs`, `ListTests.Pop.cs` (exist ✓).
5. Stdlib indexers listed under Adequacy › Harness are #2404 tracker cells for the lead, not plan work (R-KA scopes the contract to Core).

### Unchecked Claims
- The planning-time kernel numbers (13.2 ms → 3.55–3.96 ms, 8 `blr` → 0) were not re-run; D8 makes the landing commit re-measure them on the same machine, which is the evidence that counts.
- The Debug/Release IL sizes of today's bodies quoted in Current State (e.g. `Normalize` 102/138) were not re-read; only the proposed shapes were measured (probe at the scratchpad path `ilshape/Program.cs`, re-runnable with `.claude/scripts/dotnet-serialized run -c Release --project <that dir>`).
- Whether the span members' names for task 2 (`CollectionsMarshal.AsSpan`, `Span<T>.get_Item`) are the complete allow-set delta is settled only if task 2 lands.

## Revision log

**2026-11-03 @ 89fba5eec — after verify round 1 (NEEDS REVISION / CELL), by the plan author:**
1. Roster row 15 `FrozenDict<TKey,TValue>.this[TKey]` get added; row 13 now names get and set; counts 14 → 15 (Defect Class, Generator, Acceptance).
2. D5 totality anchor widened from "references a `Sharpy.Index` member" to three literal parts: (i) `Sharpy.Index` referrers, (ii) public one-parameter `get_Item`/`set_Item` on public Core types, (iii) `newobj IndexError` sites; `KeyError` deliberately excluded with the reason recorded. Adversarial Review (iii) updated.
3. D5(c) allow-set completed from the roster bodies (`List<T>.RemoveAt/Insert/IndexOf(T,int,int)`, `Math.Min/Max`); `Dict.IsNullKey` and `Index.Clamp` named as the conformant Core callees.
4. D5(d) ceiling re-pinned from a uniform 64 B to 100 B Release / 128 B Debug (chosen at runtime from `IsJITOptimizerDisabled`), with the measured sizes of the D2/D3 shapes recorded; (b) named as the load-bearing rule for M1.
5. Mutations M6 (inline `KeyError` back on `Dict` get → row 13) and M7 (an un-rostered public indexer → anchor (ii)) added to D9, Acceptance and the #2404 close criterion.
6. D7 gains the no-difference control (`BASELINE.md:31`).
7. Spellings: `ArrayHelpersTests`; `ListTests.GetItem.cs`/`SetItem.cs`/`Pop.cs`; `FrozenDictTests`/`KeyErrorTextTests` added to the blast radius.
8. Stdlib indexer cells recorded under the #2404 tracker entry for the lead (outside R-KA's Core contract; not plan work).

## Verification Summary — round 2 (superseded by the revision log below; the fresh pass re-stamps)

**Result:** NEEDS REVISION
**Adequacy:** CELL
**Verified on:** 2026-11-03 @ 89fba5eec
**Plan file:** .claude/plans/plan-460cbf.md
**Mode:** `--adequacy` (tier: light), fresh read by a new verifier. Dimensions 4-design, 6 and 7 graded; `check_plan_refs.py --issues` reports RESULT OK (the 20 unresolved symbols are new helpers, new test names or BCL names; the fixture path is not yet created; the two ambiguous hits `src/Sharpy.Core/Dict.cs:178-190` and `src/Sharpy.Core/Dict.cs:31` should cite `src/Sharpy.Core/Dict.cs` explicitly). No inline corrections were made.

**Method.** I scanned the Release `net10.0` `Sharpy.Core.dll` (built 2026-11-01; last Core source commit 909b79467, 2026-10-27) with a reflection IL scanner at `/private/tmp/claude-501/-Users-anton-Documents-github-sharpy/b9b5bb5f-1bd1-4778-82ad-641da3eebfc5/scratchpad/callscan/Program.cs` (output `out-net10c.txt` in the same directory). It decodes every method's IL, lists call and newobj targets, and reports the whole-assembly `IndexError`, `KeyError`, `Index` and key-indexer sets. The scanner's sizes for today's bodies match the plan's Current State numbers (Normalize 102, GetItemUnchecked 82, Pop 129, Index(T,int,int) 111, list get 34 and set 35, Dict get 65, StringHelpers.GetItem 56, Slice.Normalize 24, ToNormalizedInt32 42, ArrayHelpers 40 and 41), so the instrument agrees with the plan's baseline.

### Adequacy
- **Contract:** named. R-KA (#2404 comment 6539776539) states the contract: element accessors on hot paths inline; only wraparound and IndexError construction stay behind a call. The plan's sentence matches it.
- **Matrix:** the shape roster is complete for the arms; the behavioural matrix is not. The touched arms, read from code: the eight `Index.Normalize` callers (IndexExtensions.cs:11,14; Slice.cs:70; List.Methods.cs:91,119,214,215; List.Slicing.cs:50,55,258; eleven call expressions in eight methods), the three emitter routes (RoslynEmitter.Expressions.Access.cs:2265 StringHelpers.GetItem, :2272 ArrayHelpers.GetItem, :2279 GetItemUnchecked), and every public key indexer (List get and set, Dict, FrozenDict, Bytes). Every method in the scanned DLL that constructs an IndexError is a roster row or the str.format N/A group. **The behavioural baseline (Current State, lines 47-53) pins list get, set, pop, insert, index, str and one dict KeyError.** It does not pin array (row 11), bytes (row 14), frozendict (row 15), the dict None key or out-of-range cases (row 13), or List.Index's ValueError (row 10). The cure rewrites every one of those arms. D6 (line 240) says the fixture pins "accessor × direction × route"; it does not, so the matrix is missing cells.
- **Cure:** seam plus harness. One `Sharpy.Index` seam with NoInlining throw helpers (D1 to D3), and a new meta-class row for cold-path construction. The seam is right; the helper placement is wrong (finding 3 below).
- **Harness:** named, no allowlist. As specified, the harness cannot be green at landing (findings 1 to 4).
- **Layer:** every decision is rung 2 (Core C#). The compiler is untouched; no Semantic or CodeGen file is in the edit set.

### Findings the author must act on
Each finding is red at landing against the code as it stands, measured from the DLL unless marked otherwise.

1. **D5(c) allow-set omits calls the roster bodies make today.**
   - Row 5, `IndexExtensions.ToNormalizedInt32` (src/Sharpy.Core/IndexExtensions.cs:7-15), calls `System.Index::get_IsFromEnd` and `System.Index::get_Value` (scan: IL 42).
   - Row 15, `FrozenDict.this[TKey]` get (src/Sharpy.Core/FrozenDict.cs:153-171), calls `System.Collections.Immutable.ImmutableDictionary<,>::TryGetValue` (scan: IL 65). The field at FrozenDict.cs:46 is `ImmutableDictionary`, not `Dictionary`, so the plan's "byte-for-byte twin of row 13" is false for the call target.
   - Row 13 set (src/Sharpy.Core/Dict.cs:178-190) calls `Dictionary<,>::set_Item` and `Dictionary<,>::get_Count` (scan: Dict set_Item IL 62). The plan's "already conformant" claim is refuted by the allow-set as written.
   - Row 14, `Bytes.this[int]` get, calls `Bytes::get__data` (src/Sharpy.Core/Bytes.cs:21, `_storage ?? System.Array.Empty<byte>()`), which calls `System.Array::Empty` (scan: IL 16). That accessor is a non-conformant callee under rule (c) and is not in the conformant-callee list.
   - Fix: name each of these in the allow-set with a reason, or change the cure (row 14 can read `_storage` directly). The plan must choose.
2. **D5(b) forbids newobj, but row 6 constructs a tuple.** `Slice.Normalize` returns `(int, int)` (src/Sharpy.Core/Slice.cs:68-71). The current IL contains `newobj System.ValueTuple<int,int>::.ctor` (scan). A cured row 6 is red by (b) at landing. Fix: exempt the ValueTuple constructor by name, or restate (b) as "no newobj of a reference type and no ldstr".
3. **D5(iii) reds the cure's own throw helpers.** D2 (lines 170-176) puts the IndexError construction in `[NoInlining]` helpers on `Sharpy.Index`: `WrapOrThrow`, `OutOfRange`, `PopOutOfRange`, `PopFromEmpty`, and the per-family helpers. Each holds `newobj IndexError::.ctor`. Clause (iii) requires every such method to be a roster row or an N/A row, and none is. The guard is red on the first landing. Fix: add a positive category: every IndexError holder is a roster row or a NoInlining helper listed by name. Mutate that category too.
4. **D5(ii) cannot reach a dict setter.** Clause (ii) says "public one-parameter get_Item/set_Item". A CLR indexer setter takes the key and the value, so `Dict<K,V>.set_Item` has two parameters (scan: `params=2`). Row 13 says the set is in the roster "because the anchor lists it"; by the anchor's own wording it does not. A future throwing dict setter escapes all three parts. Fix: state (ii) as "key indexer: a getter with one parameter, or a setter with the key then the value". Add a mutation for a throwing setter.
5. **Mutation list gaps.** Row 15 has no mutation; M6 covers the Dict getter only. Nothing mutates clause (iii), so it has no recorded red. M1 also reddens row 5, because ToNormalizedInt32 calls strict Normalize in non-slice mode.
6. **Behavioural matrix gap (the reason the adequacy is CELL).** Add fixture cells for: array get and set, in range, negative in range, and out of range; bytes in range and out of range; frozendict missing key and None key; dict None key; List.Index with a missing value. Each expected output must come from `run` at the landing sha. Python agreement is required only where CPython agrees; the existing deviation row covers message text.

### Warnings (author action before /implement-plan)
- **The str.format N/A row needs method names.** The IndexError holders are `StringExtensions.ResolveItem` and `StringExtensions.ResolveFieldValue` (src/Sharpy.Core/StringExtensions.Format.cs:300, 452, 465, 484; scan). The N/A row names the feature, not the methods the guard must list.
- **The tuple N/A departs from R-KA's named contract.** R-KA's comment on #2404 lists "the Dict/tuple/str indexers" as contract. The plan's N/A cites docs/language_specification/collection_types.md:363 (compile-time `.ItemN` lowering), which is a sound reason. Carry it into the charter note or cite the owner's acceptance.
- **Slicing indexers are outside the anchor by design; say so literally.** `List.this[int,int]` and `[int,int,int]` (src/Sharpy.Core/Partial.List/List.Slicing.cs:81, 90; scan params 2 and 3) and the GetSlice, SetSlice and DeleteSlice callers of Slice.Normalize are not one-parameter, so no clause reaches them. Name them in the N/A list.
- **The `this[System.Index]` N/A reason is wrong.** The plan says "no throw". Its body (List.Slicing.cs:25) calls the BCL indexer, which throws ArgumentOutOfRangeException. The reason should be "no Python-visible route".
- **Generator criterion.** The Defect Class matrix is accessor (15) by direction (4) by route (3). Contract section 12(b) lists a matrix of two or more axes with three or more values as a full-tier criterion. R-KA ruled light, and the shape roster is enumerated by reflection, which is a reasonable argument. The owner should confirm that the hand-written behavioural fixture satisfies section 11 for this matrix. Dimension 9 was not graded (light mode).
- **Clause (d) does not discriminate the positive control.** BadShapeControl measures 61 B Release and 73 B Debug, under the 100 B and 128 B ceilings. Only clause (b) reports it. The D5 text is correct; the control should be described as caught by (b) alone.
- **Chore M5 cannot run yet.** No `#2365` draw filter exists under `src/` (grep). The seeds `5F-Ai3FcPnEx`, `8sLgyTw-IOM3` and `c4U0RdI-d-Uw` appear only in the #2365 issue thread, so M5 is valid only after the chore lands.

### Falsifiability
- **ElementAccessInlinabilityConformanceTests (D5 a to d):** mutations M1, M2 and M3 are named. M1 reddens rows 1, 2, 4, 5 and 9, not only 1, 2, 4 and 9. **Missing:** a mutation for clause (iii). Exemption: IS the subject for rule (c) and for "or NoInlining". The allow-set is the exemption the implementer will widen (finding 1). Positive control: yes; BadShapeControl is reported by (b) and GoodShapeControl passes. Inert-fix: M2 and M3 cover a helper inlined back and a surviving second Normalize. By-direction: n/a, no new refusal.
- **Totality anchor clause (ii):** M7 (a getter) is named. The setter escape (finding 4) has no mutation.
- **Totality anchor clause (iii):** no mutation; red at landing (finding 3).
- **Fixture collections/element_access_messages_2404 (D6):** M4 named; exemption not the subject; positive control is M4 itself; discriminating, since every expected line is an exception message or a mutated list. Coverage gap: finding 6.
- **Chore draw filter for #2365 (M5):** named; exemption not the subject, because the filter fails when a filtered draw compiles; the second-shape positive control is named. Runnable only after the chore lands.
- **Row 13 and row 15 mutation M6:** the Dict getter only. FrozenDict needs its own mutation (M6b) and the Dict setter needs one (finding 4).
- **Benchmark (D7, D8):** not a test. The printed IsJITOptimizerDisabled state and the no-difference control are named.

### Design decisions checked
- **D1 holds.** Eight methods call Index.Normalize with constant flags: the two IndexExtensions calls, Slice.Normalize, List.Insert, Pop, Index, get_Item, set_Item and DeleteAt. RangeExtensions.cs:9 calls ToNormalizedInt32. No Index.Normalize call exists outside Core (grep of src/ excluding Core: none). InternalsVisibleTo Core.Tests is at src/Sharpy.Core/Sharpy.Core.csproj:30. I checked the strict Normalize equivalence to today's branch by hand; I did not re-measure its IL size.
- **D2 holds for the shapes read,** except helper placement (finding 3) and the allow-set (finding 1).
- **D3 holds.** `j = i < 0 ? i + n : i; (uint)j >= (uint)n` rejects exactly today's throw set (i >= n or i < -n), and wraps the rest. The message uses the original i, because today's catch rewraps an i that was never reassigned (List.Methods.cs:114-123).
- **D4 is a measured gate and was not re-measured here.** The R-KA reading in the plan matches the ruling text.
- **D5(d) ceilings hold for the shapes measured in round 1** (largest Release 71 B against 100 B; largest Debug 95 B against 128 B). The unmeasured shapes are FrozenDict and ToNormalizedInt32, which measure 65 B and 42 B today. The ceilings are not the defect; the allow-set and clause (b) are.
- **D10 holds.** collection_types.md:43 says `list[i]` throws IndexError; :363 says tuple negative indices are unsupported because access is compile-time. deviations.yaml:2027 is the row id, and the plan's citation at 2032 is the sharpy_behavior line.
- **R-KK (#2365)** matches the chore description in the plan (issue comment dated 2026-11-03 @ 89fba5eec).

### Unchecked Claims
- **Kernel timings** (13.2 ms today; 3.55 to 3.96 ms for task 1; 1.6 to 1.75 times BCL) were not re-run. The probe inputs the plan cites (`kernel/`, `ilprobe/`, `p76-baseline2.spy`) are not in this session's scratchpad.
- **Round-1 IL sizes of the proposed shapes** were not re-run. The FrozenDict and ToNormalizedInt32 proposed shapes were never measured.
- **Behavioural baseline outputs** at HEAD were not run. The fixture `.expected` file does not exist yet.
- **Chore seeds** were not run under `CsCheck_Seed`.
- **Debug Core sizes** were not re-read; only the Release DLL was scanned.
- **StringExtensions method mapping:** the line numbers were read from source; the method names came from the DLL scan.

**2026-11-03 @ 89fba5eec — after verify round 2 (NEEDS REVISION / CELL), by the plan author:**
1. Behavioural baseline re-measured over every roster row with a Sharpy route × every direction × route (Release `sharpyc run`); program + output saved at `.claude/tmp/p76/element_access_messages_2404.{spy,expected}` and quoted in Current State; D6 copies them verbatim and records why the fixture stays out of the differential pool (`array`/`frozendict` names). Row 9 `DeleteAt` recorded as route-less (`del` = SPY0144), pinned by `ListTests.DelItem.cs`.
2. D5(b) restated: `newobj` of `Exception`-derived types, `ldstr`, interpolation-handler/`String.Concat`/`Format` calls — `ValueTuple` (row 6) admitted by construction.
3. D5(c) allow-set completed from the DLL scan: `Dictionary.set_Item/get_Count`, `ImmutableDictionary.TryGetValue`, `System.Index.get_IsFromEnd/get_Value`, `Array.Empty<T>`; `Bytes.get__data` named as a conformant callee. Row 15's backing store corrected (`ImmutableDictionary`).
4. Anchor (ii) restated as "getter with one parameter or setter with two"; anchor (iii) gains the named `[NoInlining]` helper category, checked both ways.
5. N/A rows made literal: explicit-interface list indexers, `StringExtensions.ResolveItem`/`ResolveFieldValue` by name, `[System.Index]` reason corrected, slicing indexers/methods added. Tuple N/A carries the R-KA wording note for the owner's acknowledgement.
6. Mutations M6b, M7b, M8, M9 added; M1's red set corrected to rows 1, 2, 4, 5, 9; M5 ordering (chore commit first) recorded. `BigShapeControl` added so (d) has its own positive control.
7. Generator paragraph added under the axes (shape axis enumerated by reflection in the guard; compared surface by the existing differential; the hand fixture only for the message-text axis), with the owner's confirmation requested at approval.

## Verification Summary — round 3 (superseded by the revision log below; the fresh pass re-stamps)

**Result:** NEEDS REVISION
**Adequacy:** CELL
**Verified on:** 2026-11-03 @ 89fba5eec
**Plan file:** .claude/plans/plan-460cbf.md
**Mode:** `--adequacy` (tier: light), fresh read, no inline corrections. `check_plan_refs.py --issues`: RESULT OK (14 paths, 45 file:line, 4 issues; the 24 unresolved symbols are new helpers, guard names and BCL names). `src/Sharpy.Core/Dict.cs:178-190` and `src/Sharpy.Core/Dict.cs:31` are ambiguous and should carry `src/Sharpy.Core/`.
**Method:** grep of every `Index.Normalize`, `Slice.Normalize`, `ToNormalizedInt32`, `new IndexError`, public `this[` and emitter access route in `src/Sharpy.Core` and `src/Sharpy.Compiler`; reflection scan of the Release net10.0 Core DLL (round-2 scanner, output `scratchpad/callscan/out-net10c.txt`, DLL built 2026-11-01 per round 2); re-run of the saved fixture with `src/Sharpy.Cli/bin/Release/net10.0/sharpyc run`; `emit csharp` probes of slice, range-loop, dict, bytes and frozendict routes; python3 3.12 on the fixture's index, insert, pop and `KeyError` lines.

### Adequacy
- **Contract:** named. R-KA (#2404 comment 6539776539) and charter row 6c⁶″ (the charter §1 table, line 33). The owner's list names tuple accessors too; see Design decisions.
- **Matrix:** larger than the repro lists, and the shape roster is complete. The behavioural arm for roster row 6 is missing.
  - Shape roster, from the scan: public one-parameter getters and two-parameter setters are List get (three overloads, of which `this[int]` is row 1 and `[System.Index]`/`[System.Range]` are the N/A rows), List set (row 2), Dict get and set (row 13), FrozenDict get (row 15) and Bytes get (row 14). Methods holding `newobj IndexError` are ArrayHelpers get and set (row 11), `Index.Normalize` (row 4), `GetItemUnchecked` (row 3), `Pop` (row 7), `StringHelpers.GetItem` (row 12), `Bytes.get_Item` (row 14), and `StringExtensions.ResolveItem` and `ResolveFieldValue` (N/A). Methods referencing `Sharpy.Index` are rows 1, 2, 5, 6, 7, 8, 9 and 10. No Core method outside the roster and N/A list trips any clause.
  - Emitter routes all land on roster rows: `RoslynEmitter.Expressions.Access.cs:2261` (StringHelpers.GetItem, row 12), `:2272` (ArrayHelpers.GetItem, row 11), `:2279` (GetItemUnchecked, row 3), `:2317` (GetSlice, row 6), `RoslynEmitter.Statements.Assignments.cs:283-309` and `:1476` (ArrayHelpers.SetItem, row 11), and the default `[]` route (rows 1, 2, 13, 14, 15).
  - **Row 6 has a Sharpy route and no fixture cell.** `print(ys[1:3], ys[-2:], ys[::2])` emits `global::Sharpy.Slice.GetSlice(ys, 1, 3, null)`, which reaches `Slice.Normalize` through the list overload at `src/Sharpy.Core/Slice.cs:110` and `:128`. The probe prints `[2, 3] [3, 4] [1, 3]`, and `ys[-9:9]` prints `[1, 2, 3, 4]`; python3 3.12 agrees. The saved fixture has no slice line, so Current State's claim that every roster row with a Sharpy route is pinned is false for row 6. Add slice cells (in range, negative, past either end with clamping, a step), each output taken from `run` at the landing sha.
  - **Row 5 has no Sharpy route.** `RangeExtensions.ToSlice` (`src/Sharpy.Core/RangeExtensions.cs:9`) is its only caller, and nothing in `src/Sharpy.Compiler` or `src/Sharpy.Stdlib` calls `ToSlice`. Current State should say row 5 is C#-only.
- **Cure:** seam plus per-family NoInlining helpers, with the meta-class row named. Correct.
- **Harness:** named, no allowlist. The universe for clauses (i) and (iii) is missing; see Falsifiability.
- **Layer:** every decision is rung-justified. No file under `Semantic/`, `Lowering/` or `CodeGen/` is in the edit set.

### Falsifiability
- **`ElementAccessInlinabilityConformanceTests`, clauses (i) and (iii): mutations M3, M8 and M9 cannot go red under the universe the plan names.** The only enumerator named is `Assembly.GetExportedTypes()`, under clause (ii). `Index` is `internal static class Index` (`src/Sharpy.Core/Index.cs:3`), `Slice.Normalize` is `internal static` (`src/Sharpy.Core/Slice.cs:68`), and the helpers are internal or private by construction. Under exported-only enumeration, a private un-rostered caller (M3), a helper without `NoInlining` (M8) and a private helper holding `newobj IndexError` (M9) are all invisible. Verdict: **MISSING** as written. State that clauses (i) and (iii), and the helper attribute check, enumerate `GetTypes()` with `BindingFlags.Public | NonPublic | Static | Instance | DeclaredOnly`.
- **Clause (d) positive control, `BigShapeControl`: unmeasured.** The probe at the `ilshape` probe (round 1) holds only `BadShapeControl` (61 B Release, 73 B Debug, measured) and `GoodShapeControl` (14 / 25). No `BigShapeControl` exists. The Fact must report it under the 128 B Debug ceiling if the test assembly is built Debug, and the "≥ 101 B Release" figure does not show that. Measure it in the configuration the Fact runs under and record both sizes.
- **No named mutation for clauses (a), (c) and (d).** (a): Pop keeps its `catch` → row 7 red. (c): an unlisted BCL call in row 1 → row 1 red. (d): a roster body padded above the ceiling → red. M1 exercises only (b) and (c).
- **The totality anchor has no standing positive control.** The recorded M-runs are the only presence case. Contract §2 asks that the anchor's own universe be shown to hit, so add a planted holder in the test assembly or record the gap as a deliberate limit.
- **M1 red set: correct.** Strict `Index.Normalize` callers after the cure are get (row 1), set (row 2), DeleteAt (row 9) and ToNormalizedInt32 in non-slice mode (row 5), with row 4 itself. Clamp callers (rows 6, 8, 10) stay green, so {1, 2, 4, 5, 9} is the expected red set.
- **M6 and M6b: correct.** Clause (b) catches the two `newobj KeyError` sites in `src/Sharpy.Core/Dict.cs:168,176` and `FrozenDict.cs:161,169`.
- **M7 and M7b: correct.** A new public one-parameter getter or two-parameter setter on a public type is visible to `GetExportedTypes`.
- **M4 positive control: real.** The saved fixture pins `list index` text on its lines 2, 5 and 11 of the expected output; the body matches the expected output with `cmp`.
- **Fixture exclusion from the python3 pool: holds.** `array[int](3)` at fixture line 94 is rejected by the explicit-type-argument rule, `DifferentialExecutionTests.cs:1598`, using the name set declared at `:1328`.
- **M5 (#2365): not yet runnable.** `grep -rn 2365 src` finds nothing, which matches the plan's ordering note.
- **Inert fixes: covered.** M2 covers the helper inlined back, and M3 covers a surviving second `Normalize`. The static scan cannot see JIT inlining, which D8's disassembly evidence covers. This matches D5's own statement.

### Design decisions checked
- **D1 holds.** Eight `Index.Normalize` call sites use constant flags. `IndexExtensions.cs:11,14` forward the public method's own parameters, so the split follows those flags. Strict `Normalize` equals today's non-slice branch (`src/Sharpy.Core/Index.cs:26-36`), and `Clamp` equals the slice branch (`src/Sharpy.Core/Index.cs:16-24`). `InternalsVisibleTo` for Core.Tests is at `src/Sharpy.Core/Sharpy.Core.csproj:30`.
- **D2 holds** for the shapes read, provided the helpers live on `Sharpy.Index`, which clause (iii) requires.
- **D3 holds.** Today's `Pop` catch rewraps the un-reassigned `i` (`src/Sharpy.Core/Partial.List/List.Methods.cs:114-123`), so the message names the original index. The test `j = i < 0 ? i + n : i; (uint)j >= (uint)n` rejects exactly `i >= n || i < -n`.
- **D5(b) holds as restated.** It admits row 6's `newobj ValueTuple<int,int>` (scan: `Slice::Normalize`, IL 24) and still reds M1 (`newobj IndexError` in `Index::Normalize`) and M6 (`newobj KeyError` in `Dict::get_Item`).
- **D5(c) holds for the current bodies.** Scanned call targets for rows 5, 13 (set), 14 and 15 are `System.Index::get_IsFromEnd/get_Value`, `Dictionary::set_Item/get_Count`, `Array::Empty` via `Bytes::get__data` (IL 16), and `ImmutableDictionary::TryGetValue`. All are in the allow-set. Rows 7 to 10 need `List::RemoveAt`, `Insert` and `IndexOf(T,int,int)`, which are listed. `Math.Min` and `Math.Max` are reached through `Index::Normalize` and `Clamp`, and are listed. Post-cure NoInlining helper calls need no entry.
- **D5 anchor, the (ii) clause.** Its eight matches are exactly the public key accessors above. Every roster row is reached by at least one clause, so placement is green at landing once the helpers sit on `Index`. The universe caveat is in Falsifiability.
- **D6 exclusion rule: holds.** The cite is `DifferentialExecutionTests.cs:1598`, not `:1329`. The shape id the implementer will see is `sharpy-only-call:explicit-type-arguments`, not the "sharpy-only name" D6 gives.
- **D4 task 2 adds a predicate R-KA does not rule.** R-KA gates task 2 on the task-1 kernel staying above 1.3 times BCL. D4 also requires a 15 percent margin in both orders. State it as the plan's own decision or record the owner's acknowledgement.
- **Tuple N/A departs from the owner's named list.** R-KA names "the Dict/tuple/str indexers", and charter row 6c⁶″ names tuple accessors too. The N/A rests on a verified fact: `docs/language_specification/collection_types.md:363` says tuple access lowers to `.ItemN`, and the scan shows no tuple accessor. The plan calls this "not a door", but it still changes the owner's named scope. Record the owner's acknowledgement before landing.
- **D10 holds with citation drift.** `collection_types.md:43` and `:363` say what D10 says. In `docs/deviations.yaml` the row id is at `:2027`; `:2032` is its `python_behavior` line.

### Unchecked Claims
- Kernel timings (13.2 ms today, 3.55 to 3.96 ms for task 1, 1.6 to 1.75 times BCL) and the `blr` counts were not re-run. D8 makes the landing commit re-measure them.
- Debug IL sizes of today's bodies were not re-read. The scanned Release sizes match Current State.
- Debug sizes of the FrozenDict and ToNormalizedInt32 proposed shapes were not measured.
- The `Index.Normalize` call count in Current State is "8 calls". The scan lists four interpolation-handler calls, so the total is seven. Correct the count or drop it.

### Warnings
- **Citations.** `ListTests.GetItem.cs`, `SetItem.cs`, `Pop.cs` and `DelItem.cs` live under `src/Sharpy.Core.Tests/Partial.ListTests/`; name that path. `src/Sharpy.Core/Dict.cs:178-190` and `src/Sharpy.Core/Dict.cs:31` need `src/Sharpy.Core/`.
- **Unlisted call site.** `RangeExtensions.cs:9` calls `ToNormalizedInt32`. Name it under row 5 as C#-only.
- **Blast radius (Dimension 8, not graded in light mode).** `GetItemUnchecked` text also appears in `src/Sharpy.Stdlib/Itertools/Itertools.cs` and `src/Sharpy.Stdlib/Re/ReModule.cs`, which the blast-radius line does not name. Confirm whether they are calls. The whole-solution gate covers them either way.
- **Fixture header.** `run` prints "Successfully compiled to ..." and "=== Running Program ===" before the program output. The saved `.expected` omits them. D6 should say the harness compares program stdout only.
- **Generator row.** Defect Class says "Generator: not required", but the matrix has three axes of three or more values (accessor 15, direction 4, route 3). The plan's §11 argument is present: a reflection-enumerated guard, the differential corpus, and a hand fixture for the text axis. Keep the owner confirmation request.
- **Helper literal.** D2 names `NotInList` and `KeyNotFound<K>`, but the clause (iii) literal omits them. They hold ValueError and KeyError, so the two-way check does not need them. Say so in the literal.

**2026-11-03 @ 89fba5eec — after verify round 3 (NEEDS REVISION / CELL), by the plan author:**
1. Slice cells (list, str, bytes get-slices: in range, negative, clamped past both ends, empty, step) added to the baseline program and measured with `run`; `.claude/tmp/p76/element_access_messages_2404.{spy,expected}` updated (58 lines of stdout after round 6); python3 agrees on every slice line. Slice assignment recorded as refused (SPY0225). Row 5 recorded as C#-only (`RangeExtensions.cs:9`).
2. Anchor parts (i)/(iii) and the helper attribute check now enumerate `GetTypes()` with `Public | NonPublic | Static | Instance | DeclaredOnly`; (ii) keeps `GetExportedTypes()`. A `PlantedHolder` positive control for the three anchor parts added.
3. `BigShapeControl` measured: 179 B Release / 266 B Debug.
4. Per-clause mutations M10 (a), M11 (c), M12 (d) added; the helper literal notes `NotInList`/`KeyNotFound` trip no clause.
5. D4's 15 % margin recorded as the plan's own decision (R-KA rules only the 1.3× trigger). The tuple N/A note and the §11 generator argument stand for the owner's acknowledgement at approval — recorded in the plan when given.
6. Citations: `Partial.ListTests/` path, `DifferentialExecutionTests.cs:1598` + shape id, `deviations.yaml:2027`, `Index.Normalize` call count 7, `Itertools`/`Re` added to the blast radius, stdout-only `.expected` noted.
7. Cite hygiene for `check_plan_refs.py` (reads the whole file, history sections included): the superseded summaries' bare `Dict.cs:*`/`Index.cs:*` cites qualified with `src/Sharpy.Core/`, two scratchpad/charter cites reworded.
8. Owner rulings recorded 2026-11-03 (plan approval, this session): tuple N/A acknowledged; light tier stands under the §11 argument; the task-2 15 % margin accepted as the plan's landing rule.

## Verification Summary — round 4 (superseded by the revision log below; the fresh pass re-stamps)

**Result:** NEEDS REVISION
**Adequacy:** CELL
**Verified on:** 2026-11-03 @ 89fba5eec
**Plan file:** .claude/plans/plan-460cbf.md
**Mode:** `--adequacy` (tier: light), fresh read. Dimensions 4-design, 6 and 7 graded; no inline corrections. `check_plan_refs.py --issues`: RESULT OK, with one ambiguous citation (see Warnings). Scratch probes lived in the session scratchpad; the fixture run wrote `.claude/tmp/p76/round4-run.out` and `.claude/tmp/p76/element_access_messages_2404.cs` (gitignored, created by this pass and not removed).

### Adequacy
- **Contract:** named (R-KA on #2404; plan lines 89–92). The sentence holds.
- **Matrix:** larger than the repro lists, but it misses two arms the fix reaches, and the anchor is red at landing (findings 1 to 4).
- **Cure:** seam (`Sharpy.Index`, D1) plus per-family `[NoInlining]` helpers (D2); the meta-class row is named (lines 93–97). Correct.
- **Harness:** `ElementAccessInlinabilityConformanceTests`, no allowlist. Its anchor is red at landing and its positive control cannot pass as written (findings 1 and 2).
- **Layer:** every decision is rung-justified. The compiler is untouched; no `Semantic/`, `Lowering/` or `CodeGen/` file is in the edit set.

Findings the author must act on:

1. **Anchor clause (ii) is red at landing.** `IDict` (`src/Sharpy.Core/IDict.cs:26`, member at :33) and `IList` (`src/Sharpy.Core/IList.cs:25`, member at :32) are public interfaces. Their abstract `get_Item/1` and `set_Item/2` members are exported, so (ii) reports four unplaced matches. Probe over the Release `Sharpy.Core.dll` built 2026-11-01: `IDict::get_Item/1`, `IDict::set_Item/2`, `IList::get_Item/1`, `IList::set_Item/2`, each `abstract=True IL=none`. The N/A list (line 119) names only the explicit implementations, which are private and excluded by construction. Fix: add the four declarations as literal N/A rows with a reason, or state that (ii) excludes abstract members.

2. **The PlantedHolder control cannot pass as written.** (a) A private nested type is absent from `GetExportedTypes()` (probe: `exported-has-PlantedHolder=False`, `GetTypes` finds it), so the (ii) branch never sees the holder. The "same enumeration code path" claim (lines 285–290) holds only for (i) and (iii). (b) "Reports exactly its planted member" fails over the test universe. (ii) also reports the public nested test type `LenGetitemSeq.this[int]` (`src/Sharpy.Core.Tests/SharpyInstanceFormattingTests.cs:99`, nested in the public class at :27). (iii) reports `BadShapeControl` and `GoodShapeControl`'s `Slow`, both of which hold `newobj IndexError`. Fix: make the holder a public nested type for (ii), assert containment instead of equality, and separately assert zero unplaced matches on the Core scan.

3. **The row 6 array-slice route is not pinned.** `arr[a:b]` on `array[int]` emits `Slice.GetSlice(arr, …)` (the round-4 `arrslice` probe's generated C#, line 20 in the scratchpad). That resolves to the `T[]` overload (`src/Sharpy.Core/Slice.cs:151`) and reaches `Slice.Normalize` at `Slice.cs:173`. The Release `sharpyc run` prints `[2, 3] [3, 4] [1, 2, 3, 4] [1, 3]` for `arr[1:3], arr[-2:], arr[-9:9], arr[::2]` over `[1, 2, 3, 4]`. The fixture's slice cell (the saved fixture's slice cell (its lines 183–191)) covers list and str only. Current State (lines 58–60) also credits bytes slices to row 6, but bytes emits `Slice.GetSlice(bb, …)`, which dispatches to `Bytes.Slice` (`src/Sharpy.Core/Partial.Bytes/Bytes.Slicing.cs:13`) and never calls `Slice.Normalize`. Fix: add array slice cells (in range, negative, clamped past both ends, step) and correct the bytes attribution.

4. **Compound element assignment is an unpinned route into rows 1, 2, 11 and 13.** `xs[i] op= v`, `arr[i] op= v` and `d[k] op= v` each emit a read and then a write through the same accessors. The array branch reads with `ArrayHelpers.GetItem` and writes with `ArrayHelpers.SetItem` (`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.Assignments.cs:279–309`). The Release binary runs `xs[0] += 5; xs[-1] *= 2; arr[-1] += 4; d["a"] += 10; d["b"] += 3` and prints `[6, 2, 6] 1 4 11 3`. It also raises `IndexError list index 5 out of range` for `xs[5] += 1` and `KeyError 'zz'` for `d["zz"] += 1` (`arrslice/aug.spy`). No fixture cell covers this, and the route axis (lines 124–126) does not name it. Fix: add compound assignment to the axis and add list, array and dict cells.

**Warnings**
- **M12 is inert under Release.** A 40-operation pad on dead locals compiles to 2 B in Release and 348 B in Debug (`deadpad/Program.cs`, `DeadPad`). Lines 342–343 expect "~34 → ~180 B Release", which this measurement refutes. M12 reds (d) only in Debug, the configuration the gate runs. Fix: name M12 as a Debug mutation, or pad with live operations. `BigShapeControl`-style live padding measured 305 B Release in the same probe.
- **The clause (a) and (c) controls are missing.** Nothing plants a method with a `try`/`catch` that (a) reports alone, or a method that calls an unlisted BCL member that (c) reports alone. M10 and M11 are recorded only in the commit body, so neither has a standing control. Add both, the way `BigShapeControl` does for (d).
- **One ambiguous citation.** `check_plan_refs.py` reports `src/Sharpy.Core/Dict.cs:178-190` with five candidates. Qualify it as `src/Sharpy.Core/Dict.cs`.
- **The N/A citations are incomplete.** Line 119 cites `List.Interfaces.cs:81,94,136` but omits `List.src/Sharpy.Core/IList.cs:9` (explicit `IList.this[object]`) and the interface declarations in finding 1. Make the list literal.

### Falsifiability
- `ElementAccessInlinabilityConformanceTests`, clause (a): mutation M10 named; `Pop` still has its `try`/`catch` at `src/Sharpy.Core/Partial.List/List.Methods.cs:117–124`, so M10 reverts a real shape. Exemption not the subject. Positive control: missing. An exception-handling clause is an absence assertion (`ExceptionHandlingClauses.Count == 0`), so it needs a planted method with a `try`/`catch` that the Fact reports by (a) alone.
- Clause (b): mutation M1 named (red set {1, 2, 4, 5, 9}, which matches the strict callers). The `ValueTuple` `newobj` in row 6 is admitted by construction, not by an exemption. Positive control: `BadShapeControl` at 61 B Release and 73 B Debug, re-measured.
- Clause (c): mutation M11 named; the call to `Console.Out.Flush()` is not dead code, so it reds row 1. Exemption: the allow-set, which D4 says task 2 widens. That is a stated plan fact. Positive control: missing (see Warnings).
- Clause (d): mutation M12 named but inert in Release (see Warnings). `BigShapeControl` at 179 B Release and 266 B Debug, re-measured, is reported by (d) alone. Positive control yes.
- Anchor (i): mutation M3 named and reds under `GetTypes()` with NonPublic. The nonpublic scan lists `Sharpy.Index::Normalize` and `Sharpy.Slice::Normalize` as nonpublic, so an exported-only enumeration would miss M3-style callers. Positive control: finding 2.
- Anchor (ii): mutations M7 and M7b named. Red at landing (finding 1). Positive control: finding 2.
- Anchor (iii): mutation M9 named; helper attribute mutation M8 named. Positive control: finding 2.
- Helper literal: M2 (`WrapOrThrow`) named; the attribute check reds it. Verified.
- M6 and M6b: the `newobj KeyError` sites at `src/Sharpy.Core/Dict.cs:168,176` and `src/Sharpy.Core/FrozenDict.cs:161,169` match the plan.
- Fixture M4: positive control holds. The `.expected` carries `list index` text on five lines. The saved fixture reproduces the `.expected` byte-for-byte after the two `sharpyc run` header lines are stripped.
- Fixture by-direction: not applicable, no refusal added.
- Chore M5 (#2365): not run; the plan says it is not runnable until the chore lands.
- Benchmark no-difference control: `benchmarks/BASELINE.md:31` supplies the 15 % floor; the plan cites it correctly.

### Design decisions checked
- **D1 holds.** The strict form matches `src/Sharpy.Core/Index.cs:14–37` for every input, including `max = 0` and `i = max`. `Clamp` matches `src/Sharpy.Core/Index.cs:16–24`. The strict `Normalize` measures 14 B Release (probe re-run). The eight call sites match `IndexExtensions.cs:11,14`, `Slice.cs:70`, `List.Methods.cs:91,119,214,215` and `List.Slicing.cs:50,55,258`.
- **D2 holds** for the shapes read, given the helper placement. `src/Sharpy.Core/Dict.cs:178–190` set has no throw. `FrozenDict.cs:153–171` uses `key is null` and no `IsNullKey` call. `StringHelpers.cs:66–67` reads `s.Length` twice. `Bytes.cs:94,104` throw in one getter. `ArrayHelpers.cs:10–45` holds the duplicated check.
- **D3 holds.** The `catch` at `List.Methods.cs:117–124` rewraps the un-reassigned `i`, so the message names the original index. The new body reproduces it.
- **D4 holds as written.** The R-KA comment on #2404 states the 1.3× BCL trigger and the light ruling. `benchmarks/BASELINE.md:31` supplies the floor. The 1.6–1.75× ratio is not re-measured (unchecked).
- **D5 ceilings hold.** The Debug 128 B ceiling covers every proposed roster shape (largest: `Index(T,int,int)` at 95 B). The Release 100 B ceiling covers the Release shapes (largest: `Index(T,int,int)` at 71 B).
- **D5(c) allow-set holds.** Every roster-body call target in the nonpublic scan is listed, including the `System.Index` getters, `ImmutableDictionary.TryGetValue`, `Dictionary` `set_Item` and `get_Count`, and `Array.Empty`.
- **D5 anchors (i) and (iii) hold.** The nonpublic scan finds 16 methods that reference `Sharpy.Index` or construct `IndexError`. Each is a roster row or an N/A row, and no `IndexError` site is unplaced.
- **D5 anchor (ii) fails.** Pre-cure there are 12 matches. Eight are placed (`List` get and set, `List` get on `Index` and `Range`, `Dict` get and set, `FrozenDict`, `Bytes`). Four are the interface declarations in finding 1.
- **D6 pin holds.** The saved fixture reproduces `.expected`. The Release `sharpyc` binary built 2026-11-01 20:07 postdates the last compiler and Core commit (`3ef53c6ec`, 01:14), so it reflects HEAD's compiler; it was not rebuilt.
- **D6 exclusion holds.** `DifferentialExecutionTests.cs:1328` (`NonSubscriptableInCPython`) and `:1598` (`Reject`) match the plan.
- **Row 5 is C#-only.** No `.cs` or `.spy` caller of `ToSlice` exists outside `RangeExtensions.cs`.
- **Routes verified.** `del xs[0]` refuses with SPY0144 and `xs[0:1] = [9]` refuses with SPY0225, matching lines 73 and 187 of the fixture.
- **D10 holds.** `docs/language_specification/collection_types.md:43` states `list[i]` throws `IndexError`. `:363` states the tuple `.Item1` lowering. `docs/deviations.yaml:2027` is the row id.

### Unchecked Claims
- Kernel timings (13.2 ms today, 3.55–3.96 ms for task 1, the task 2 variants, the 1.6–1.75× BCL ratio) are not re-run. No kernel probe is in this session's scratchpad.
- The JIT constants (the 100 B candidate limit and the 16 B always-inline band) are taken as stated, not checked against the runtime source.
- Debug IL of today's bodies is not re-read. The Release figures in Current State were re-read from the scan.
- Post-cure roster sizes were measured only on the probe shapes, not the real bodies. D8 requires the landing commit to record them.
- #2365 (M5) was not run; the plan says it is not runnable until the chore lands.

**2026-11-03 @ 89fba5eec — after verify round 4 (NEEDS REVISION / CELL), by the plan author:**
1. Baseline extended (measured with `run`; python3 agrees on values/types): the six array slices (row 6's array route, `Slice.cs:173`) and compound element assignment `xs[i] += v` / `arr[i] += v` / `d[k] += v` in range, negative, out of range, missing key (`Assignments.cs:279–309`). Bytes slices re-attributed to `Bytes.Slice` (not row 6). `.claude/tmp/p76/element_access_messages_2404.{spy,expected}` updated (58 stdout lines). Route axis names read / write / compound / range-loop / slice / methods.
2. Anchor part (ii) skips abstract members; the two Core interface declarations (`src/Sharpy.Core/IDict.cs:33`, `src/Sharpy.Core/IList.cs:32`) are literal N/A rows and the skipped set is asserted equal to that literal.
3. `PlantedHolder` control re-specified: public nested type with private planted members; the scan takes an explicit type universe; a second Fact pins the type-level universes (`Sharpy.Index` in `GetTypes()`, not in `GetExportedTypes()`).
4. M12 made live in Release (ops threaded through the parameter, as `BigShapeControl`); `EhShapeControl` and `CallShapeControl` added so clauses (a) and (c) have controls.
5. The remaining ambiguous `src/Sharpy.Core/Dict.cs:178-190` cite qualified.

## Verification Summary — round 5 (superseded by the revision log below; the fresh pass re-stamps)

**Result:** NEEDS REVISION
**Adequacy:** CELL
**Verified on:** 2026-11-03 @ 89fba5eec
**Plan file:** .claude/plans/plan-460cbf.md
**Mode:** `--adequacy` (tier: light), fresh read; Dimensions 4-design, 6 and 7 graded; Dimensions 2, 3, 5, 8, 9 skipped; no inline corrections. `check_plan_refs.py --issues`: RESULT OK, 0 ambiguous. The three owner rulings (tuple N/A, light tier, 15 % margin) are not re-raised.

### Adequacy
- **Contract:** named. R-KA (#2404 comment 6539776539) states the contract; the tuple N/A is the owner-acknowledged resolution.
- **Matrix:** larger than the repro lists, and the shape roster is complete at baseline. The behavioural matrix misses one arm the cure rewrites (finding A1).
- **Cure:** seam (`Sharpy.Index`, D1) plus per-family `[NoInlining]` helpers (D2), meta-class row named. Correct.
- **Harness:** `ElementAccessInlinabilityConformanceTests`, no allowlist. Its positive controls cannot pass as written (findings F1, F2).
- **Layer:** rung 2 for every decision. The emitter is read, not edited; no `Semantic/`, `Lowering/` or `CodeGen/` file is in the edit set.

Round-4 revision items, each checked against the plan text and the code:
- **Array-slice and compound-assignment cells:** present in the saved fixture. Run through the Release `sharpyc` binary, its output equals the 52-line `.expected` after the run header and one blank line. Measured @ 89fba5eec.
- **Anchor (ii) abstract skip:** the declarations `src/Sharpy.Core/IDict.cs:33` and `src/Sharpy.Core/IList.cs:32` are public and abstract, and the N/A list names them. The explicit-implementation N/A list is still incomplete (W2).
- **PlantedHolder re-specification:** the explicit-universe design is coherent. The visibility test it needs is unstated (W4).
- **M12 live in Release:** the size figure is the round-3 BigShapeControl measurement, not re-run. M12 itself is not run.
- **EhShapeControl and CallShapeControl:** specified, but their construction is unstated and inherits F2.

Roster sweep, measured @ 89fba5eec (Release `src/Sharpy.Core/bin/Release/net10.0/Sharpy.Core.dll`, built 2026-11-01; Core sources last changed at 909b79467; 1076 methods parsed):
- **Index referrers (anchor i):** 8 methods, all placed as rows 1, 2, 5, 6, 7, 8, 9, 10. Unplaced: none.
- **IndexError holders (anchor iii):** 9 methods, all placed as rows 3, 4, 7, 11, 12, 14, or as the two N/A `StringExtensions` rows. Unplaced: none.
- **Key indexers (anchor ii, abstract skipped):** 8 matches, placed as rows 1, 2, 13, 14, 15 and the two N/A `System.Index`/`System.Range` getters. Unplaced: none.
- **Emitter routes:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Access.cs:2255-2320` routes String to row 12, Array to row 11, NativeUnchecked to row 3, and Native `[]` to rows 1, 2, 13, 14, 15; slice get reaches row 6 (list, array, str) or `Bytes.Slice` (no `Normalize`). `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.Assignments.cs:283-309` and `:1476-1477` route array writes and compounds to row 11. `ParamsSpread` is Stdlib (`NdArray`) and `TupleItem` is compile-time, so both sit outside Core.
- **Callees of the current roster bodies:** all are on the D5(c) allow-set, are pre-cure constructs the cure removes, or are the ValueTuple newobj admitted by (b). The set is complete for the cured shapes once `Index.Clamp`, `Math.Min` and `Math.Max` are counted.
- **Fixture route cells:** every roster row with a Sharpy route has a cell in every direction it can take, except A1.

Findings the author must act on:
- **A1 (matrix, red): frozendict None key is unpinned.** `src/Sharpy.Core/FrozenDict.cs:157-161` is the null-key branch that the cure rewrites in row 15. The fixture's `cell_10` pins only present and missing string keys. Probe @ 89fba5eec: an annotated `dict[str | None, int]` fed to `frozendict(...)` prints `1 2` and `KeyError None` under `sharpyc run`; python3 3.12 prints the same for the dict analogue. The literal `frozendict({None: 1, "x": 2})` is refused with SPY0227, so the cell must use the annotated-dict form. Add both cells and take their expected output from `run`.
- **F1 (positive control, red): `BadShapeControl` is not "(b) alone".** Its interpolated throw lowers to BCL calls outside the D5(c) allow-set (the handler members, or `String.Format` under LangVersion 9), so clause (c) also reports it. The "each by its own clause and no other" Fact is red as written. Either state that (c) excludes the calls (b) already owns, or assert clause membership instead of exclusivity.
- **F2 (positive control, red): `GoodShapeControl` does not pass as written.** Its local `[NoInlining]` slow path holds `newobj IndexError` (IL 55 @ 89fba5eec, round-5 IL probe). That holder is not a roster row, an N/A row, or a named `Sharpy.Index` helper, so anchor (iii) reports it. Route the slow path through a Core `[NoInlining]` helper such as `Index.WrapOrThrow` (visible to Core.Tests through `src/Sharpy.Core/Sharpy.Core.csproj:30`), so the control holds no `newobj`. Or restrict the controls' universe to clauses (a)-(d) and say so. Apply the same rule to EhShapeControl and CallShapeControl.

### Falsifiability
- **`ElementAccessInlinabilityConformanceTests`, clause (a):** mutation M10 named; the real shape is `src/Sharpy.Core/Partial.List/List.Methods.cs:117-124`. Exemption not the subject. Positive control EhShapeControl specified but subject to F2. By-direction n/a. Inert-fix paths M2 and M3 covered.
- **Clause (b):** mutations M1, M6, M6b named. The ValueTuple newobj in row 6 (`src/Sharpy.Core/Slice.cs:68-71`) is admitted by construction, not by an exemption. Positive control BadShapeControl: MISSING as written (F1).
- **Clause (c):** mutation M11 named. The NoInlining exemption IS the subject; M2 and M8 parameterize it. Positive control CallShapeControl specified but subject to F2.
- **Clause (d):** mutation M12 named (live Release pad). Positive control BigShapeControl; its figures are the round-3 measurement, not re-run here. Exemption not the subject.
- **Anchor (i):** mutation M3 named; falsifiable only under the `GetTypes()` NonPublic universe the plan states. Positive control PlantedHolder specified.
- **Anchor (ii):** mutations M7 and M7b named. The abstract skip IS the subject and has NO mutation (W3). Positive control PlantedHolder specified, subject to W4.
- **Anchor (iii):** mutation M9 named. Positive control PlantedHolder specified.
- **Helper literal:** M2 and M8 named; M2's expected red set is unnamed (W5).
- **Controls Fact ("PASSES the good one"):** MISSING as written (F2).
- **Fixture `collections/element_access_messages_2404`:** M4 named; its `list index ` lines are the positive control; discriminating; by-direction n/a (no new refusal). Coverage gap A1.
- **Chore #2365 (M5):** mutation named, positive control (a second shape is not skipped) named. The draw filter is absent from `src/` at 89fba5eec, so M5 cannot run before the chore lands. Exemption not the subject.
- **Benchmark (D7/D8):** not a test. The no-difference control and the `IsJITOptimizerDisabled` print are named; the 15 % floor is `benchmarks/BASELINE.md` line 31.

### Design decisions checked
- **D1 holds.** Strict `Normalize` (IL target 14 B) and `Clamp` are equal to the two branches of `src/Sharpy.Core/Index.cs:14-37` for max >= 0, checked by hand at i = max, max = 0 and the negative boundary. The eight callers match `src/Sharpy.Core/IndexExtensions.cs:11,14`, `src/Sharpy.Core/Slice.cs:70`, `src/Sharpy.Core/Partial.List/List.Methods.cs:91,119,214,215` and `src/Sharpy.Core/Partial.List/List.Slicing.cs:50,55,258`. No caller exists outside Core, including the test and Stdlib projects.
- **D2 holds for the shapes read,** with the wording gap in W7. The Dict set has no throw (`src/Sharpy.Core/Dict.cs:178-190`).
- **D3 holds.** The catch at `src/Sharpy.Core/Partial.List/List.Methods.cs:117-124` rewraps an `i` the try never reassigns. The replacement `j = i < 0 ? i + n : i; (uint)j >= (uint)n` rejects exactly `i >= n || i < -n`.
- **D4 holds as written.** The R-KA comment on #2404 states the 1.3x BCL trigger and the light ruling. The single-sequence floor is `benchmarks/BASELINE.md` line 31.
- **D5 holds for the anchors and allow-set** (roster sweep above). The Debug and Release ceilings are the plan's figures; the JIT constants are unchecked. The controls are not clean (F1, F2).
- **D6 holds.** The saved fixture reproduces its `.expected` exactly (52 lines). The fixture file is not yet at its TestFixtures path, which is planned work.
- **D10 holds.** `docs/language_specification/collection_types.md:43` and `:363` and `docs/deviations.yaml:2027` say what D10 says.

### Warnings
- **W1 (N/A count):** the Acceptance bullets say "5 N/A rows", but the Defect Class table has six. The sixth is the `src/Sharpy.Core/IDict.cs:33` and `src/Sharpy.Core/IList.cs:32` declarations row. Make the count and the literal list agree.
- **W2 (literal N/A list incomplete):** the explicit `IList.this[object]` at `src/Sharpy.Core/Partial.List/List.IList.cs:9` is not named. It is private, so it cannot red (ii), but the list is described as literal.
- **W3 (abstract skip has no mutation):** a new public abstract indexer on a public Core interface must go red through the literal-vs-found diff. Name that mutation (for example, add `int this[int i] { get; }` to a public Core interface).
- **W4 (visibility test):** state whether anchor (ii) filters member or type visibility. `Type.IsPublic` is false for a public nested type, so a type filter would hide PlantedHolder. Use member-level `Public` on the explicit universe.
- **W5 (M2 expected red set):** name it. The reds run through `WrapOrThrow` directly (row 4) and through the conformance of `Index.Normalize` (rows 1, 2, 5, 9); say which of these the mutation is expected to make red.
- **W6 (literal N/A matrix cells):** row 3 cannot receive a negative index (see the NativeUnchecked proof at `src/Sharpy.Compiler/Semantic/SemanticInfo.cs:3048`). Rows 13 and 15 have no negative direction. Rows 12 and 14 have no compound route (no setter). Name these cells literally so "every cell" is checkable.
- **W7 (D2 shape wording):** D2's uniform `if ((uint)index < (uint)count)` does not describe rows 13 and 15, which are key lookups (`src/Sharpy.Core/Dict.cs:160-176`, `src/Sharpy.Core/FrozenDict.cs:153-171`). State their fast path as the key lookup and their slow path as `KeyNotFound<K>`.

### Unchecked Claims
- Kernel timings (13.2 ms, 3.55-3.96 ms, the BCL ratio) and `blr` counts: not re-run; no kernel probe exists in this session.
- JIT constants (100 B candidate limit, 16 B always-inline band): taken as stated, not checked against the runtime.
- Post-cure roster sizes and callees: measured only on probe shapes; the real cured bodies do not exist yet.
- BigShapeControl and M12 figures: the round-3 measurement is quoted, not re-run.
- M1 red set and M2 red set: derived by reading the post-cure call graph, not by a mutation run.
- M5 (#2365): not runnable; no draw filter exists in `src/` at 89fba5eec.
- Interface declarations (`src/Sharpy.Core/IDict.cs:33`, `src/Sharpy.Core/IList.cs:32`): verified by source; the scanner omits bodiless members, so no IL scan covers them.

**2026-11-03 @ 89fba5eec — after verify round 5 (NEEDS REVISION / CELL), by the plan author:**
1. Baseline: the frozendict `None`-key branch pinned both ways (`1 2`, `KeyError None`; source dict annotated `dict[str | None, int]` because the literal is SPY0227); `.claude/tmp/p76/` pair updated (58 stdout lines).
2. Controls: `Good`/`Eh`/`Call` route their slow path through `Index.WrapOrThrow`, so no control holds `newobj IndexError`; the Fact asserts clause inclusion for `BadShapeControl` ((b) and (c)) and exclusivity for the other three.
3. N/A count 6 everywhere; `Partial.List/List.IList.cs:9` added to the explicit-implementation literal; anchor (ii) filters at member level; M13 (abstract-skip mutation) added; M2's red set named (rows 1, 2, 4, 5, 9); the empty matrix cells listed literally; D2 restated for the key-keyed rows.


## Verification Summary — round 6

**Result:** PASS
**Adequacy:** CLASS
**Verified on:** 2026-11-03 @ 89fba5eec
**Plan file:** .claude/plans/plan-460cbf.md
**Mode:** `--adequacy` (tier: light), fresh read. Dimensions 4-design, 6 and 7 graded; Dimensions 2, 3, 5, 8 and 9 skipped. No inline corrections. `check_plan_refs.py --issues`: RESULT OK (re-run after this summary was written).

### Adequacy
- **Contract:** named. R-KA on #2404 (comment 6539776539, created 2026-10-31) states the same contract; the tuple N/A is the owner-acknowledged resolution.
- **Matrix:** complete for arms. The 15-row roster, the six N/A rows and every emitter access route match the code (sweep below). The literal empty-cell list is incomplete (Warnings 1 and 2), and one list route is unnamed (Warning 3). Every accessor by direction cell is pinned by at least one fixture line, so none is an arm the cure can change silently.
- **Cure:** seam (`Sharpy.Index`, D1) plus per-family NoInlining helpers (D2). The meta-class row is named. Correct.
- **Harness:** `ElementAccessInlinabilityConformanceTests`, no allowlist. Every anchor placement is green on the current bodies (sweep below); the guard itself is not yet written.
- **Layer:** rung 2 for every decision. No file under `Semantic/`, `Lowering/` or `CodeGen/` is edited; the emitter was read only.
- **Round-5 revision items, each checked against the plan and the code:**
  - Frozendict None-key cells: present in the fixture (`.claude/tmp/p76/element_access_messages_2404.spy` lines 174–186) and in its expected output (lines 50–53). Holds.
  - Control Facts: slow paths through `Index.WrapOrThrow` are stated. Re-measured: `BadShapeControl` 61 B Release, 73 B Debug, so (b) and (c) report it and (d) does not. `BigShapeControl` 179 B / 266 B, (d) alone. Holds.
  - N/A count 6 and `src/Sharpy.Core/Partial.List/List.IList.cs:9`: the six N/A rows are present and the explicit-implementation literal includes line 9. Holds.
  - Member-level visibility for anchor (ii): stated (`MethodInfo.IsPublic`, non-abstract). Holds.
  - M13: does not build as written (Warning 4).
  - M2 red set (rows 1, 2, 4, 5, 9): holds on the call graph; the three controls that call `Index.WrapOrThrow` also move (Warning 5).
  - Literal empty cells: incomplete (Warning 1).
  - D2 key-keyed wording: wrong for row 15 (Warning 6).

Anchor sweep, measured @ 89fba5eec (IL scan of the Release `src/Sharpy.Core/bin/Release/net10.0/Sharpy.Core.dll`, built 2026-11-01; Core sources last changed at 909b79467):
- **Index referrers (anchor i):** 8 methods, all placed as rows 1, 2, 5, 6, 7, 8, 9, 10. Unplaced: none.
- **IndexError holders (anchor iii):** 9 methods, placed as rows 3, 4, 7, 11 (two), 12, 14 and the two N/A `StringExtensions` methods. Unplaced: none.
- **Public key indexers (anchor ii, non-abstract):** 8 matches, placed as rows 1, 2, 13 (get and set), 14, 15 and the two N/A `List` getters for `System.Index` and `System.Range`. Unplaced: none.
- **Abstract declarations skipped:** `src/Sharpy.Core/IDict.cs:33` and `src/Sharpy.Core/IList.cs:32`, four accessors, listed as N/A. Explicit implementations are private and excluded by construction: `src/Sharpy.Core/Partial.Dict/Dict.IDict.cs:12`, `src/Sharpy.Core/Partial.Dict/Dict.Interfaces.cs:345`, `src/Sharpy.Core/Partial.List/List.Interfaces.cs:81,94,136`, `src/Sharpy.Core/Partial.List/List.IList.cs:9`.
- **`new IndexError` sites:** 14 in source, all inside the methods above.
- **`Normalize` callers:** 11 call expressions in 8 methods (`src/Sharpy.Core/IndexExtensions.cs:11,14`, `src/Sharpy.Core/Slice.cs:70`, `src/Sharpy.Core/Partial.List/List.Methods.cs:91,119,214,215`, `src/Sharpy.Core/Partial.List/List.Slicing.cs:50,55,258`), all rostered. No caller outside Core.
- **Emitter routes** (`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Access.cs`): String to row 12 (2265), Array to row 11 (2272), NativeUnchecked to row 3 (2279), default indexer to rows 1, 2, 13, 14, 15, and `GetSlice` to row 6 (2317). Array set and compound (`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.Assignments.cs:279-309`, `:1476-1477`) reach row 11. `ParamsSpread` needs a params indexer (NdArray, Stdlib); `TupleItem` is compile-time.
- **`IndexAccessLowering` and `SliceLoweringKind`:** every arm is either a roster row, an N/A row, or outside Core (`Builtin` and `UserSymbol` are call-target kinds, not element access).

### Falsifiability
- **Clause (a), `ElementAccessInlinabilityConformanceTests`:** M10 named; the real try/catch is at `src/Sharpy.Core/Partial.List/List.Methods.cs:117-124`. Exemption not the subject. `EhShapeControl` reports (a) alone on the planned shape.
- **Clause (b):** M1, M6 and M6b named. The `ValueTuple` newobj in row 6 (`src/Sharpy.Core/Slice.cs:68-71`) is admitted by construction, not by an exemption. `BadShapeControl` is the positive control.
- **Clause (c):** M11 named. The NoInlining exemption is the subject; M2 and M8 parameterize it through the helper-literal attribute check. `CallShapeControl` reports (c) alone. Under M2, `GoodShapeControl` and `EhShapeControl` also move (Warning 5).
- **Clause (d):** M12 named (live pad). `BigShapeControl` is reported by (d) alone (no calls, newobj or ldstr). Every measured roster-shape probe is under the 100 B Release and 128 B Debug ceilings; the largest is the `IndexOf`-shaped row 10 probe at 71 B / 95 B.
- **Anchor (i):** M3 named. It is falsifiable only under the NonPublic `GetTypes()` universe, because `Sharpy.Index.Normalize` and `Sharpy.Slice.Normalize` are non-public (measured). The plan states this.
- **Anchor (ii):** M7 and M7b named. The abstract skip has M13, which does not build as written (Warning 4).
- **Anchor (iii):** M9 named; the helper attribute check is M8.
- **Positive controls for anchors (PlantedHolder):** not written yet. The design is coherent: public nested type, private planted members, explicit universes, type-level pins.
- **Fixture M4:** named; the expected output carries `list index` text on lines 6, 7, 14 and 15, so the positive control is real.
- **M5 (#2365):** not runnable. No draw filter exists in the tree; the three seeds appear in the #2365 thread (verified).
- **Inert-fix paths:** M2 (helper inlined back) and M3 (surviving second `Normalize`) are covered. JIT-level inlining is not visible to the static scan; the D8 disassembly evidence covers it, as the plan states.
- **Refusals:** none added, so by-direction probes are n/a.
- **Benchmark:** not a test. The no-difference control cites `benchmarks/BASELINE.md:31` correctly.

### Design decisions checked
- **D1 holds.** `src/Sharpy.Core/Index.cs:14-37` (strict and slice branches) equals the proposed strict `Normalize` and `Clamp`. Measured strict `Normalize`: 14 B Release, 15 B Debug. The eight call sites pass constant flags, so the split is per site.
- **D2 holds for rows 1–12 and 14; wrong for row 15** (Warning 6).
- **D3 holds.** The catch at `src/Sharpy.Core/Partial.List/List.Methods.cs:117-124` rewraps an `i` the try never reassigns, so the message names the original index. The `j`-based body rejects exactly `i >= n` or `i < -n`.
- **D4 holds.** The R-KA comment states the 1.3× BCL trigger and the one-plan, two-task ruling. The 15 % margin is the plan's decision, which the plan records as owner-accepted.
- **D5 holds.** The allow-set covers every callee of the 16 current roster bodies, and every post-cure callee is on it or is a NoInlining helper. The anchors and ceilings hold (sweep above). The attribute route R-KA mentions is not addressed (Warning 8).
- **D6 holds.** The saved fixture's stdout equals `.claude/tmp/p76/element_access_messages_2404.expected` after the two run-header lines and the blank line before the program output are removed. Citation in Warning 7.
- **D10 holds.** `docs/language_specification/collection_types.md:43` and `:363`, and `docs/deviations.yaml:2027` (the id line).

### Warnings
Each warning is a matrix, wording or mutation-spec defect. None turns a gate red at landing, so none changes the result. The author must act on all of them before `/implement-plan`.

1. **Literal empty-cell list is incomplete.** Name row 3 write and row 3 compound (no unchecked setter; the compound read uses the indexer, line 415 of the gitignored emit output `.claude/tmp/p76/element_access_messages_2404.cs`). Name row 15 write and row 15 compound, which are refused with SPY0320 (probes: `fd["x"] = 3` and `fd["x"] += 1`; str and bytes refuse the same way). Say rows 8 and 10 clamp rather than throw.
2. **Route labels are wrong for plain reads.** Plain reads of literal non-negative list indices emit `GetItemUnchecked` (row 3): lines 21 and 24 of `.claude/tmp/p76/element_access_messages_2404.cs`, selected at `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Access.cs:2279`. Current State's "list get" cells for `xs[0]`, `xs[2]` and `xs[3]` pin row 3. Row 1's in-range and out-of-range cells are pinned only through compound reads (lines 415 and 420 of the same file). Name the route per emitted site, and pin a plain row-1 read through an index the checker cannot prove non-negative: a function parameter emits the indexer (`xs[k]`, probed).
3. **Unnamed route into row 1.** Every list slice reads elements through `List.get_Item`: `src/Sharpy.Core/Slice.cs:124` and `:130`. The negative-step path (`:122`) never reaches `Slice.Normalize`, and the fixture pins only positive steps. Probe: `ys = [1, 2, 3, 4]` and `ys[::-1], ys[3:0:-2], ys[-1:-3:-1]` prints `[4, 3, 2, 1] [4, 2] [4, 3]`, which python3 matches. Add that line to the fixture and name the route.
4. **M13 does not build as written.** `Sharpy.IList` is implemented by `List<T>` (`src/Sharpy.Core/Partial.List/List.IList.cs:7`), and `Sharpy.IDict` by `Dict<K,V>` (`src/Sharpy.Core/Partial.Dict/Dict.IDict.cs:8`). A new abstract indexer fails with CS0535 unless the mutation also adds an explicit implementation. Name that implementation in M13, or the recorded red is a compile error and not the guard.
5. **M2's expected red set omits the controls.** `GoodShapeControl`, `EhShapeControl` and `CallShapeControl` all call `Index.WrapOrThrow`. Without NoInlining each gains a clause (c) report, so their pass and exclusivity Facts go red. This is not a vacuity problem; name it in the M2 entry.
6. **D2 wording.** Row 15 has no `IsNullKey` call: `src/Sharpy.Core/FrozenDict.cs:153-171` tests `key is null` and `_hasNullKey`, while `src/Sharpy.Core/Dict.cs:160-176` calls `IsNullKey`. D2's "(plus the IsNullKey branch they have today)" is wrong for row 15. D2 also names the helper `Index.KeyNotFound(key)` in one sentence and `KeyNotFound<K>` on `Dict` in another; choose one.
7. **Citation hygiene.** D6 cites `DifferentialExecutionTests.cs:1598` as a bare filename. The full path is `src/Sharpy.Compiler.Tests/Properties/Differential/DifferentialExecutionTests.cs:1598` (the `Reject("sharpy-only-call:explicit-type-arguments")` line). `:1328` was not re-read this round.
8. **R-KA guard wording.** The R-KA comment on #2404 accepts "IL body under the JIT inline budget or `AggressiveInlining` present". The plan's guard is IL-only and does not say it considered the attribute route. State the narrower rule as the plan's own decision.

### Unchecked Claims
- Kernel timings (13.2 ms today, 3.55–3.96 ms for task 1, the blr counts, the 1.6–1.75× BCL ratio): not re-run.
- Post-cure shapes were measured only as probes. The real FrozenDict body does not exist yet; its post-cure size is unmeasured.
- The guard, the controls and PlantedHolder are not written. Their behaviour is reasoned from probe sizes and call graphs, not run.
- Red sets for M1, M2, M3, M6–M12 are derived from the call graph, not from mutation runs.
- M5 (#2365): not runnable until the chore lands.
- `src/Sharpy.Core/Sharpy.Core.csproj:30` (InternalsVisibleTo) and the 16 B figure for `Bytes.get__data` were not re-read this round; round 5 read them.
- Dimensions 8 and 9 are skipped by mode.
- The gitignored `.claude/tmp/p76/element_access_messages_2404.cs` was regenerated with `sharpyc emit csharp`; no tracked file changed except this plan.

**2026-11-03 @ 89fba5eec — after verify round 6 (PASS / CLASS), warnings applied by the plan author (no design decision changed; the stamp stands):**
1. Baseline: row 1's plain read pinned through a function parameter (literal non-negative indices lower to row 3) and through negative-step slices (`[4, 3, 2, 1] [4, 2] [4, 3]`, python3 agrees); `.claude/tmp/p76/` pair updated (58 stdout lines). Empty cells: row 3 and row 15 have no write/compound route (read-only route; SPY0320).
2. M13 made buildable (declaration + explicit implementation); M2's red set includes the three good-shaped controls; D2 names FrozenDict's bare `key is null` test and the helper uniformly as `Index.KeyNotFound<K>`; D6 cites the differential test by full path; D5 records the IL-only guard as the plan's narrower reading of R-KA's "or `AggressiveInlining`".

## Implementation Checklist

Tier: light · base 89fba5eec · lead-owned board (no task tools in this harness).

- [x] T0 baseline: build green; gate **83334 passed / 1 failed / 9 skipped @ 89fba5eec (measured)** — the 1 red (`LetEmissionShardTotalityTests.ShardUnion_EqualsTheUnshardedCorpus_AsAMultiset`) is lead-induced: the T5 fixture was copied into `TestFixtures/collections/` mid-run of a `--no-build` gate and the corpus enumerations disagree on exactly `collections/element_access_messages_2404` — lead
- [x] T1 (22e017005; M5 red ×3 seeds / green) chore #2365 (R-KK): `None`-literal-receiver known-red pin in `ILCompilesPropertyTests` + drain Fact + positive control; M5 recorded — lead — blocked by T0 (lock)
- [x] T2 (6f47aea81; lead-reviewed; Core.Tests 4055/0/0) Core cure, rows 1–15 (D1–D3) — core-library-expert — blocked by T0 (lock)
- [x] T3 (6f47aea81; 4/4 Debug + Release; pre-cure roster red 78 violations) guard `ElementAccessInlinabilityConformanceTests` (D5) + controls + PlantedHolder — test-expert — parallel with T2
- [x] T4 (6f47aea81; 13/13 red→green, md5-restored; lead re-ran M2: 2 failed → green) mutations M1–M3, M6, M6b, M7, M7b, M8–M13 on T2+T3; IL size table — test-expert — blocked by T2, T3
- [x] T5 (6f47aea81; green; M4 red 1/1 → green; differential: sharpy-only-call:explicit-type-arguments) fixture `collections/element_access_messages_2404` (D6) + M4 + differential exclusion line — lead — blocked by T2
- [x] T6 (6f47aea81) commit task 1 (T2+T3+T4+T5 evidence) — lead
- [x] T7 (6f47aea81; indexer 13.1→4.1 ms, unchecked 12.9→2.9 ms; in-range-path calls 8→0) benchmark `benchmarks/core-element-access/` (D7) + before/after + disasm (D8) — lead — blocked by T2
- [x] T8 (NOT TAKEN: indexer ≈5 % faster < 15 % floor, unchecked ≈17 % slower; 8 rounds both orders; comment draft scratchpad/t8-not-taken.md) task 2 measurement (D4): span get path A/B; land or record "measured, not taken" — lead — blocked by T6, T7
- [x] T9 (**83361 passed / 0 failed / 9 skipped @ 6273ed667 (measured)**; ns2.1 Core 0 warn; staleness gates + format verify clean; differential 0 non-allowlisted, fixture excluded as sharpy-only-call:explicit-type-arguments; #2404 CLOSED, comments posted on #2404 ×3 and #2365) final: build, whole-solution gate, netstandard2.1 Core build, staleness gates, format, diff re-check — lead
- [x] T10 (p76-verify, opus: checks 2–6 HOLD; check 1 REFUTED — N1 NoInlining on Normalize (benchmark 4.0→6.9 ms), N2 NoInlining on list get, N3 StringBuilder newobj on the fast path left the guard green → fixed in 6273ed667: clauses (e), (c′), (c″) + 3 controls; all 16 mutations red→green; lead re-ran N1, N4, N5 → red 1/4 → green) standalone verification-expert refutation pass — lead
- [x] T9a (cb76ea319) LSP census re-pins for the new fixture stem (gate @ 6f47aea81 was 83359/2/9; digest-subtraction proof + fixture-removed control 3363/0) — lead

## Verify-implementation round (2026-11-04, light: Agents 2, 3, 4; completeness by the lead)

Scope 89fba5eec..6273ed667 (4 implementation commits). HEAD gate **83361/0/9 @ 6273ed667 (measured)**
(log `.claude/tmp/dotnet-serialized-1.log`, binaries newer than the commit). Benchmark re-run by the lead @ 6273ed667:
indexer 4.21 ms, unchecked 2.99–3.40 ms, BCL 2.23–2.27 ms, `IsJITOptimizerDisabled=False`; in-range path 0 calls.

Defect Class corrections [CORRECTED: verify-implementation]:
- The N/A row "`List<T>.this[System.Index]` / `[System.Range]` — no Python route" is FALSE for `this[System.Index]`:
  `match xs: case [a, *_, b]` emits `case [var a, .., var b]` and reads `b` through it (Agent 3, X9). It is
  **roster row 16** (body already conformant: `_list[index.GetOffset(_list.Count)]`; allow-set +`System.Index::GetOffset`).
  `this[System.Range]` stays N/A with the corrected reason: the `*rest` capture is slice access.
- Row 5 (`ToNormalizedInt32`) had no executing test anywhere (Agent 3, X8) — `IndexExtensionsTests` added.

Guard holes closed (Agent 3): X1 (a discarded `Index.OutOfRange` call on the get fast path) and X2 (`Normalize`
forwarding every index to `WrapOrThrow`) left the guard green — clause (c′) judged which helper, never where.
New clause (f): a helper-free entry→ret path must exist, and an exception-returning helper is only `throw Helper()`.
X6 (`[IndexerName("At")]` public indexer) evaded anchor (ii) — the anchor is now keyed on index parameters.

- **Final gate @ 7a08df402:** 83371/0/9 @ 7a08df402 (measured) — Core 4065, Stdlib 4295, Cli 1126/1 skip, Lsp 22359, Compiler 51526/8 skip; build 0; EXIT=0. +10 over HEAD = IndexExtensionsTests.
