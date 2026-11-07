<!-- Verified by /verify-plan on 2026-09-04 @ dff55b2cd -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->

# P2 plan C — seam & promotion residue (#1749 #1750 #1751 #1754 #1755 #1759 #1760 #1761 #1762 #1763 #1766 #1767 #1768 #1769 #1770-user)

> Charter: `.claude/plans/remediation-round-2026-08-30-batching.md` › *Addendum 2026-09-04 › P2 plan C*.
> Sequencing ruled 2026-09-04: **plan C before P7; tag 0.18.0 only AFTER plan C.**
> Every measurement below is `sharpyc run` @ **dff55b2cd** (Debug CLI built 2026-09-04 16:59, HEAD committed 16:51).
> Probe corpus (346 programs + 2 incremental projects, one file per cell, `LEDGER.tsv` per batch):
> `.claude/tmp/plan-757fbb-probes/probes{,2,3,4}/` (copied from session e05000fd; `LEDGER.tsv` = name, exit, first diagnostic or RUNS) — reuse rather than re-probe; regenerate with the `gen*.py` scripts, run with `run1.sh`.

## Context

Plans A (`plan-14853b`, store seam + strict Optional R-G) and B (`plan-299c1b`, numeric promotion) landed at 949862bd5 with 24761/0/4 measured. Their own 17×24 store matrix, the numeric matrix's sibling prober and the executing-fixture corpus arm of the Optional guard left **ratcheted residue**: 17 KnownRed store cells (#1759–#1763), 3 `FixtureAllowlist` rows (#1755 B/C), two promotion consumers nobody listed (`sum`, `in`), a `LiteralString` that is a `str` only at the store seam (#1766), a symbol serializer that is not total (#1751), the mutator half of the iterable ring (#1754), a `??=` that never consults the seam (#1767), a qualified case-constructor route that types open (#1770), and the one place a `T?` silently takes a wrong-typed value in the strict-Optional release (#1768).

**0.18.0 must not ship with the strict-Optional ICEs** (#1761 #1762 #1763 #1768) — the tag waits for this plan.

Rulings (authoritative, posted as issue comments; do not re-ask): **R-R** [#1762](https://github.com/antonsynd/sharpy/issues/1762#issuecomment-5546241566) refuse-by-name now (option C is the recorded future lowering, B rejected) · **R-T** [#1755](https://github.com/antonsynd/sharpy/issues/1755#issuecomment-5546241731) payload rule for stores into a narrowed `T?` · **R-U** [#1750](https://github.com/antonsynd/sharpy/issues/1750#issuecomment-5546236147) `in`/`not in` join the promotion-consumer axis · **R-V** #1757 (charter table) walrus is value-typed · **R-S** #1770 body (user unions keep `Union.Case(...)`; builtin refusal + name reservation is **Batch 3**, not here) · #1755 Class C needs no ruling (`null_coalescing_assignment.md:26-27` [CORRECTED: `:28` is the `T | None` comment header] and `:249`).

## Current State (measured @ dff55b2cd with `run`)

### Row 1 — store-seam positions

| Cell | Today | Contract |
|---|---|---|
| `t: tuple[int?, int] = (42, 1)` / `xs: list[int?] = [42]` / `s: set[int?] = {42}` / `d: dict[str, int?] = {"k": 42}` / `[[42]]` into `list[list[int?]]` / `dict[int?, str]` key / return `[42]` for `list[int?]` (SPY0260) / argument `take([42])` | SPY0220 naming the CONTAINER pair, no steer | SPY0604 at the element span (#1759) |
| `f: () -> int? = lambda: 42` | SPY0220 naming `() -> int32` vs `() -> int32?` | SPY0604 at the body (#1759) |
| `xs: list[int] = [ov()]` (`ov() -> int?`), `tuple[int32 \| None, int]` into `tuple[int?, int]` | SPY0220, steer dropped | seam steer (narrow/unwrap · `maybe`) (#1759) |
| `xs: list[int?] = [None]`, `list[int] = [None]`, `set[int?] = {None}`, `dict[str,int?] = {"k": None}`, argument `take([None])` | SPY0227 "list of only 'None'; add a type annotation (e.g., list[object])" | SPY0604 / SPY0229 (#1760); `list[int \| None] = [None]` and `[None()]` run today (controls) |
| `f: () -> int? = lambda: None`, `() -> int = lambda: None`, `apply(lambda: None)` with `() -> int` | **SPY0908 / CS1643** | SPY0604 / SPY0229 (#1761); `() -> int \| None` and `() -> None` run (controls) |
| `def f(x: int? = Some(42))`, method, `__init__`, `def f(r: int!str = Ok(1))`, `Err("e")`, lambda `Some(42)`/`None()` defaults | **SPY0908 / CS1736** | SPY0401 + `= None()` / `x ??= Some(v)` steer (R-R); `def f(x: int? = None())` runs (control) |
| `def f3(t: tuple[int, int] = (1, 2))`, nested tuple, `tuple[str, int]`, lambda tuple default | **SPY0908 / CS1736** | SPY0401 (#1769); controls: `g()`/`Box(1)` SPY0401, `[]` SPY0400, `"a" + "b"` / `K` / `-1` / `1 if True else 2` / enum / `None` into `int \| None` run |
| `(x := Some(42))` after `x: int? = None()`; under `print`; under `if … is not None`; under `while`; `int8?` | SPY0227 / SPY0230 / SPY0244 | runs (#1763); `y: int? = (x := Some(42))` runs (control) |
| `n: int = (m := 5)` with `m: int \| None`; `s: str = (o := "s")` with `o: object`; `take((m := 5))` | SPY0220 naming the DECLARED type | runs — R-V value-typed |
| `x: int? ??= r` (`r: int \| None`); `y: int \| None ??= o` (`o: int?`) | **SPY0908 / CS0029** both ways | SPY0220 + cross-family steer (#1767) |
| `x: int8? ??= 7`, `x: float32? ??= 0.5`, `x: int8? ??= 7 if c else 8`, `x: int8 \| None ??= 7` | **SPY0222 false refusal** ("does not support '??=' with operand of type 'int32'") | runs — the seam's constant/float32/conditional arms (#1767 siblings) |
| `x: int? ??= 42`, `??= Some(42)`, `w: int? = Some(7); w ??= 42` → 7, member `c.v ??= 42`, index `d["k"] ??= 42` | run **via `Optional<T>`'s implicit operator** (`FixtureAllowlist` row `null_coalescing_assignment_optional`) | run with `Optional<T>.Some(...)` emitted (#1755 C) |
| `x: int? = Some(10); if x is not None: x += 5` | runs via the implicit operator (`FixtureAllowlist` rows `optional_augmented_assign_narrowing`, `algorithms/linked_list`) | emitted `x = Optional<int>.Some(x.Unwrap() + 5)` (#1755 B) |
| `if x is not None: x += 5; n: int = x` | SPY0220 (`int32?` into `int32`) — the narrowing is KILLED by the store | narrowing survives (R-T) |
| `if d is not None: d = 5; e: int = d + 1` | **SPY0908 / CS0019** (rebinding version typed `int32`, no accessor) | runs, prints 6 (R-T) |
| `n: int \| None = None; n = 5; m: int = n` | **SPY0908 / CS0266** (pre-existing: rebinding-narrowed read of a value-type nullable has no `.Value`) | runs (R-T's read-side twin) |
| `a: int? = Some(1); b: int? = None(); if a is not None: b = a; c: int = b` | **SPY0908 / CS0029** | `b = a` passes the Optional through, `c: int = b` is SPY0220 (R-T pass-through) |
| `def f(x: int? = Some(42)) …; f(Some(1))` (default never omitted) | CS1736 | SPY0401 (R-R — the signature is refused, not the call) |

**#1768 re-measured — the class is not narrowing.** A plain identifier store into a name declared in an **enclosing block scope** skips the seam entirely; narrowing is incidental:

| Store in a nested block (`if True:` / `while` / `for` / `else` / `try` / `with` body) | Today | Same-scope twin |
|---|---|---|
| `d: int = 1; if True: d = "s"` | **SPY0908 / CS0029** | SPY0220 |
| `d: int? = Some(10); if True: d = "s"` (also `while`, `else`, `try`, `with`) | **SPY0908 / CS0029** | SPY0604 |
| `d: int? = Some(10); if True: d = 5` (also `for` body, `is None` branch) | runs via the implicit operator | SPY0604 (R-G) |
| `n: int \| None = 1; if True: n = "s"`; `n: str \| None = "a"; if True: n = 5` | **SPY0908 / CS0029** | SPY0220 |
| `d: int8 = 1; if True: d = 300` | **SPY0908 / CS0031** | SPY0220 |
| `d: int8 = 1; if True: d = 7 if c else 8` | **SPY0908 / CS0266** (no branch cast) | runs |
| `f: float32 = 0.0; if True: f = 0.5` | **SPY0908 / CS0664** (literal not re-typed) | runs |
| `s: LiteralString = "a"; v: str = "b"; if True: s = v` | **runs — silently admits a non-literal `str` into a `LiteralString` slot** | SPY0220 |
| `d: int = 1; if True: d = None` | SPY0229 (the `VoidType` arm reads the parent predecessor — the ONLY arm that does) | SPY0229 |
| `assert d is not None; d = "s"` (same scope) | SPY0604 (correct) | — |
| walrus `if True: if (d := "s") != "": …`, comprehension walrus, tuple-element `d, n = "s", 1`, attribute, index | refused correctly (these arms look up the predecessor through the parent scope) | — |

Mechanism (`TypeChecker.Statements.cs:96-172`): `existingSymbol = Lookup(name, searchParents: false)`; the `CheckStore` block is guarded by `if (existingSymbol is VariableSymbol boundSymbol)`, so a predecessor found only through `parentSymbol` gets `EnterStore` (expected-type push) and the `VoidType` arm but **no seam call**, and the new binding version takes the raw value type. The walrus arm (`Expressions.cs:695-722`) already does it right: `candidate = Lookup(searchParents: false) ?? Lookup(searchParents: true)`, filtered by `ExpressionRebindingPredecessor(candidate, walrus.IsNameBacktickEscaped)` [CORRECTED: the enclosing-scope rule is the lookup chain; the two-argument helper only filters out constants and mismatched backtick-escape identity]. The same chain crosses the **function** boundary, so a nested `def` storing to the enclosing function's name is the same class: `d: int = 1; def inner() -> None: d = "s"` ICEs CS0029 today [ADDED: adequacy — measured 2026-09-04 @ dff55b2cd].

### Row 2 — promotion consumers

| Cell | Today |
|---|---|
| `sum(list[uint64])`, `sum(xs, 10)`, `sum(set[uint64])`, `sum(tuple[uint64, uint64])`, `sum(dict[uint64, str])`, `sum([x*2 for x in xs])` | SPY0354 "No overload of 'sum' matches (list[uint64])" |
| `sum(list[int8])`, `int16`, `uint8`, `uint16`, `uint32`, `sum(list[uint32], 1)` | **SPY0908 / CS1503** — the checker binds `list[int8]` to `Sum(IEnumerable<int>)` (records `int32`), `list[uint32]` to `Sum(IEnumerable<long>)` (records `int64`); Roslyn refuses the covariant value-type conversion |
| `sum(list[int32])`, `int64`, `float32`, `range(4)` | run (controls) |
| `sum(list[bool])` | SPY0354 (Python-parity gap, **out of scope** — a refusal, note it) |
| `n: int32 = 2; n in xs` over `list[uint64]` / `set` / `dict` keys; `n: int8` likewise | **SPY0908 / CS1503** (24 cells: {int32, int8} × {list, set, dict} × {in, not in}) |
| `-1 in xs` over `list[uint64]`, `300 in xs` over `list[int8]` | **SPY0908 / CS1503** |
| `2 in xs` (const in range), `u: uint64`, `u: uint32` needles over `list[uint64]`, `n: int8 in list[int32]`, `1 in dict[int8, str]`, `1 in list[float]` | run (controls) |
| `"a" in list[int]`, `1 in list[str]`, `1.0 in list[int]`, `n: int32 in "abc"`, Optional needle, nullable needle, user `__contains__(self, x: int)` with a `str` needle | **SPY0908 / CS1503** — the needle is never checked against the element type at all |
| `2 in (1, 2)` (any tuple) | SPY0320 "does not support membership testing" — pre-existing Python-parity gap, **N/A cell**, file at close-out |
| `first(ys)` with `def first(xs: list[int])`, `ys: list[int8]` | **SPY0908 / CS1503** — `list[int8]` admitted into an INVARIANT `list[int]` parameter; this is #1748's data-level assignability class (P7), the same admission that binds `sum`'s narrow widths to the wrong overload |

### Row 3 — LiteralString as a type; serializer totality

| Cell (`x: LiteralString = "a"`) | Today |
|---|---|
| `x == "a"`, `x != ""`, `x < "b"`, `"a" < x < "c"`, `x + "b"` (into `str` OR `LiteralString`), `x * 2`, `x + s`, `q(x + "b")` | SPY0222 "Type 'LiteralString' does not support operator …" |
| `x == y` (both LiteralString) | **SPY0402** (a different code — `OperatorValidator`'s own `str` identity test) |
| `len(x)`, `x[0]`, `x[0:1]`, `for c in x`, `"a" in x` | SPY0320 |
| `if x:`, `not x` | SPY0220 "must be boolean / truth-testable, got 'LiteralString'" |
| **`x.split(",")`** | **runs and prints `a b`** — the `str` twin prints `['a', 'b']`. `RecordStaticExtensionDispatch` returns on `objectType != Str`, so the call emits an instance `x.Split(",")` that binds `System.String.Split` (the #1071/#1072 shadowing class, silent-wrong) |
| `x.upper()`, `x.startswith("a")`, `x.replace("a","b",1)` (→ `baa`, same as str), `x.format(1)`, `f"{x}"`, `str(x)`, dict key, `x in list[str]`, `isinstance(x, str)`, `sorted([x, y])`, `match x: case "a"` | run (controls — member resolution and the store seam already see `str`) |
| `s: LiteralString = "a"; s += v` (`v: str`) | SPY0220 (correct — R-P forms: the result is not literal-derived) |
| two-file project, `def take_ls(s: LiteralString) -> str`, `sharpyc project x.spyproj --incremental` | cold OK; warm **SPY0909 `Unhandled SemanticType in SerializeType: LiteralStringType`** (crash bundle written); same with a `LiteralString` return type and class field |
| two-file project, `class C: … def clone(self) -> Self` in `lib.spy` [ADDED: adequacy — measured 2026-09-04 @ dff55b2cd] | cold OK; warm **SPY0909 `Unhandled SemanticType in SerializeType: SelfType`** — the same class as the row above. `MK = C` (a constructor reference) at module level is refused at the cold build (SPY0301/SPY0342: no runtime value of its own), so `ConstructorReferenceType` can never reach the serializer |

`TypeCodecRegistry` (`SymbolSerializer.cs:1270-1625`; the `Register<…>` calls at `:1302-1516`) registers **17** kinds [CORRECTED: was "18" — 17 `Register<` calls counted @ dff55b2cd; 20 − 3 = 17]; `SemanticType` has 20 concrete subclasses. Missing: `LiteralStringType`, `SelfType` (**a live warm ICE, see the table row above — not a theoretical gap**), `ConstructorReferenceType` (unreachable: refused at export).

### Row 4 — mutator ring

| Cell | Today |
|---|---|
| `xs.extend((2, 3))`, `xs.extend(t)` (tuple var), `ys.extend("ab")`, `xs.extend(d)` (dict keys), `xs.extend(t)` under `if t is not None` | SPY0220 "Cannot pass argument of type 'tuple[…]' to parameter of type 'list[int32]'" |
| `s.update((2, 3))`, `s.update("ab")`, `intersection_update`, `difference_update`, `symmetric_difference_update` with a tuple, `d.update((("b", 2),))` | SPY0354 "No matching overload for 'update'" |
| `xs.extend([2,3])`, `xs.extend(range(2,4))`, `xs.extend(s)` (set), `[i for …]`, `xs.extend(d.keys())`, `s.update([2,3])`, `d.update([("b",2)])`, `d.update({"b": 2})` | run (controls) |
| `xs += (2, 3)`, `ys += "ab"`, `d \|= [("b", 2)]` | run (the augmented twin admits what the mutator refuses — the inversion #1754 names) |
| `s \|= (2, 3)` | SPY0222 with the `s.update(xs)` steer — correct (Python's `\|=` takes sets only); the steer points at a form that is itself refused today |
| `xs.extend(("a", "b"))` into `list[int]` | SPY0220 (correct refusal; must survive as the mistyped control) |
| `s.update((2,), (3,))` | SPY0354 — Core has no `params` form; **N/A cell** (note) |
| `xs.extend(i for i in …)` | SPY0104 — a bare generator expression is not accepted as a call argument at all (parser; out of scope) |

### Row 5 — qualified case constructors on user generic unions (#1770 user half)

| Cell (`union Box[T]: case Full(v: T) / case Empty()`) | Today |
|---|---|
| `a: Box[int] = Box.Full(1)`, return `Box.Full(1)`, argument `take(Box.Full(1))`, `match b: case Box.Full(v)`, `Box.Full("s")` with slot → SPY0220 naming `int32` | run / correct |
| `print(Box.Full(1))`, `b = Box.Full(1)`, `x: bool = Box.Full(1)`, `Pair.Both(1, "s")`, `Box.Full([1, 2])`, `Box.Full(mk())`, `Box.Full(lambda: 1)` | **SPY0220 "… but field 'v' expects 'T'"** — an unsubstituted type parameter rendered in a diagnostic (the #1728 guard class), and the call types OPEN |
| `e = Box.Empty()` | **SPY0908 / CS0305** — the open generic reaches Roslyn |
| `Shape.Circle(1.0)` (non-generic, no slot) | runs |
| `c: Box[int] = Full(2)` | SPY0200 — user unions have no bare spelling (R-S: stays) |
| `def f(b: Box[int] = Box.Full(1))` | SPY0401 (consistent with R-R) |

## Defect Class

Four rows, one contract: **every ratcheted row the P2 seams left behind drains, and no store position, promotion consumer, or LiteralString consumer is outside its seam.**

### Row 1 — store-seam positions (#1759 #1760 #1761 #1763 #1767 #1755 B/C #1768 #1762 #1769 #1770-user)

- **Violated contract:** a value entering a typed slot is admitted or refused by ONE seam (`TypeChecker.ClassifyStore`/`CheckStore`) consulted at every store position, **whatever scope declared the target**; the seam's verdict and steer are what the user sees; every accepted verdict's side effects (re-typing, casts, Optional wrap) are recorded as node-keyed facts the emitter reads.
- **Meta-class → standard cure:** "Position/route missing a shared check" → move the check to the seam; a position × value-shape × **target-scope** matrix so a new position or scope cannot skip it. Sub-classes: SPY0908-as-a-net (#1761 #1762 #1767 #1768 #1769) → semantic refusal; silent wrong (nested-block `str` into `LiteralString`, the implicit-operator lowerings #1755 B/C) → fix + sweep.
- **Known member cells:** the 17 KnownRed rows of `StoreConversionMatrixTests` (#1759 5, #1760 2, #1761 2, #1762 5, #1763 3), the 3 `FixtureAllowlist` rows (#1755), the #1768 table, the #1767 3×3.
- **Sibling cells this plan must also cover (measured above, outside the issues' lists):** every nested-block store (`if`/`while`/`for`/`else`/`try`/`with` bodies) **and the closure store — a nested `def`/lambda body assigning to a name declared in the enclosing function (`d: int = 1; def inner() -> None: d = "s"` → CS0029 today; under C# scoping the local function captures the declared slot, and `Lookup(searchParents: true)` crosses the function boundary, so it is the same predecessor path)** [ADDED: adequacy — measured 2026-09-04 @ dff55b2cd]; binder forms (`for d in …` target, `except … as d`, `match … case d`) declare a fresh block-scoped name and are **N/A cells** with that reason (each prints the outer value after the block — measured); for every value shape the seam knows — mistyped, out-of-range constant, conditional-of-constants, float32 literal, non-literal into `LiteralString`, bare value into `T?`; the `??=` constant/float32/conditional/`int8 | None` cells; the lambda-as-argument body slot; set/dict/nested/tuple-in-list/dict-key/return/argument element hosts; walrus under `if`/`while` tests and the R-V read cells; `Ok(…)`/`Err(…)`/`__init__`/nested-tuple defaults; narrowing survival after a payload store and after an augmented store; the rebinding-narrowed read of a value-type `T | None`.
- **Standing harness that should have caught it:** `StoreConversionMatrixTests` (caught 17, ratcheted) · `StoreTargetMatrixTests` — its declared-type product has a scope axis by accident (`IsInstance` opens a block) but **no mistyped-store column**, so a refused value never met a nested-block target · `EmittedOptionalConstructionConformanceTests` corpus arm (caught #1755, ratcheted). Phase 1 adds the mistyped column and the explicit scope axis.
- **Owner rulings:** R-R, R-T, R-V, R-G/R-O (links above). Under R-T the payload rule is applied **payload-first, declared-second**: `d = None()`/`d = Some(v)` inside a narrowing keep running as declared-slot stores (a refusal there would be a regression by direction).

### Row 2 — promotion consumers (#1749 #1750)

- **Violated contract:** every consumer of the numeric promotion table decides a mixed-width/signedness pair the same way: a constant operand converts first (§10.2.11), a pair C# cannot bind is refused by name, and the bound Core overload's element type IS the recorded type.
- **Meta-class → cure:** SPY0908-as-a-net (`in`) and a missing overload family (`sum`) → the `in` needle becomes a store into the element slot decided by the seam's argument arm; `sum` gets the own-width family (like `FloorDiv(ulong,ulong)` #1662, `CheckedIntPow` #1700); both join the `NarrowWidthArithmeticMatrixTests` consumer axis.
- **Known member cells:** #1749 (`uint64`), #1750 (`int32` needle vs `uint64`).
- **Sibling cells:** 8 integer widths for `sum` (five ICE today); needle {const in range, const out of range, same-width var, narrower-signed var, signed var vs unsigned, non-numeric, Optional, nullable, user `__contains__` param} × container {list, set, dict keys, str, user type} × {in, not in}.
- **Standing harness:** `NarrowWidthArithmeticMatrixTests` — its consumers are `MixedFamilies` {arith, cmp, eq, bitwise}, the per-width `Ops` including `**` (`PowerCases`/`PowerRefusedCases`), and `MinMaxCases` [CORRECTED: named by the arrays the totality formula at `:1146-1152` sums — there is no `**` mixed family]; `sum` and `in` are absent. Phase 6 adds them as generated cells under the totality count. **Argument-position twins already agree with R-U and are the positive controls for the `in` cure** [ADDED: adequacy — measured 2026-09-04 @ dff55b2cd]: `xs.count(n)`, `xs.remove(n)`, `xs.append(n)`, `s.add(n)`, `d[n]` with `n: int32` over `uint64` elements are each SPY0220 by name today (the verdict `n in xs` must reach); `xs.index(n)` reports SPY0354 for the same shape — an overloaded-member code divergence to file at close-out, not fixed here.
- **Owner rulings:** R-U. The `list[int8] → list[int]` invariant admission is **#1748's class (P7)**, not fixed here: Phase 6 posts the measured cell to #1748.

### Row 3 — LiteralString as a type (#1766, #1741 binary cell) and serializer totality (#1751)

- **Violated contract:** `literal_string.md` §Type Relationship — a `LiteralString` value can be used anywhere a `str` is expected: operators, protocols, truthiness, indexing, iteration, method dispatch (the static-extension lowering included), and the warm build round-trips every `SemanticType` the cold build produced.
- **Meta-class → cure:** "Position/route missing a shared check" — the value-USE routes each test `== SemanticType.Str` by identity → ONE `OperandView` normalization consulted at every use route; a behavioral twin matrix (receiver `str` vs `LiteralString`, identical output) so a route cannot be missed. Warm ≠ cold → a reflection totality guard over `SemanticType` kinds.
- **Known member cells:** `x == "a"`, `len(x)`, `x + "b"`; the incremental ICE.
- **Sibling cells:** the 25 operations measured above, including the **silent-wrong `split`** (the `RecordStaticExtensionDispatch` route), the SPY0402 route (`OperatorValidator`), truthiness, slicing, chained comparison; for the serializer, `SelfType` (**a live warm ICE — a member cell, not an exclusion**) and `ConstructorReferenceType` (N/A: refused at export, SPY0342) [CORRECTED: the plan treated both as exclusions].
- **Standing harness:** none for the LiteralString surface; `ColdWarmBuildDifferentialTests` exists but its corpus has no `LiteralString` signature. Phase 7 adds `LiteralStringSurfaceMatrixTests` and `TypeCodecRegistryTotalityTests`.

### Row 4 — mutator ring (#1754)

- **Violated contract:** the iterable-argument projection ring (#1159 #1198): a position that accepts an iterable accepts every source `ClassifyIterableArgument` can lower, and the augmented operator admits exactly what the mutator it lowers to admits (#1682's inversion).
- **Meta-class → cure:** qualified/bare-style divergence between two routes → ONE position table (`GetMemberIterableKeyPositions`) grants acceptance + lowering together; a mutator × RHS-shape matrix paired with the augmented twin.
- **Known member cells:** `list.extend`, `set.update`, `dict.update` with tuple/str.
- **Sibling cells:** the three other set mutators, tuple var, dict-keys, range, comprehension, set, narrowed Optional tuple; the mistyped control (`("a","b")` into `list[int]`).
- **Standing harness:** none rosters the mutators (the ring's `RecordIterableArgumentMarks` is `refusal-net:FileBasedIntegrationTests` in the dispatch inventory). Phase 8 adds `MutatorIterableArgumentMatrixTests`.

## Adversarial Review (pre-mortem)

- **Alternative root cause — #1768 (refuted the issue's framing):** the discriminating probe `d: int = 1; if True: d = "s"` ICEs with no narrowing anywhere; `assert d is not None; d = "s"` (same scope, narrowed) refuses correctly. The class is the enclosing-scope predecessor path, and the narrowing rule (R-T) sits on top of it. A fix that only adds narrowing awareness would leave `if True: d = "s"` an ICE.
- **Alternative root cause — `sum` narrow widths:** the ICE is not a missing overload but an admission (`list[int8]` binds `IEnumerable<int>`). Adding exact overloads makes identity win and drains the cells; the admission defect is real, separately measurable (`first(ys)`), and belongs to #1748. The plan does both: overloads here, the cell posted to #1748.
- **How the fix could be inert:** (a) the identifier-store seam runs but the new binding version is still typed by the raw value → `d = 5` inside a narrowing "runs" through the implicit operator and `d + 1` still ICEs — the survival cells (`e: int = d + 1`) and the corpus-arm row are the detectors; (b) `??=` gets a seam call but `InferAugmentedAssignmentType` still refuses first → the `int8?` constant cell stays SPY0222; (c) the element position reports but the container ALSO reports → two diagnostics; the matrix asserts exactly one; (d) `OperandView` applied at `CheckBinaryOp` but not at `CheckComparisonChain` → `x == "a"` runs and `"a" < x < "c"` refuses; the twin matrix has both; (e) `Some(42)` refused at `def` but the lambda default validator arm is never reached → `lambda x: int? = Some(42): x` still CS1736; (f) the wrap fact recorded but `MergeFrom` not extended → correct in the single-file harness, wrong in `Sharpy.Stdlib.Tests` — the multi-file cell in Phase 1 executes a two-file project.
- **Before/after by direction (§3), every newly refused program:** nested-block **bare value into `T?`** (`if True: d = 5`, `for` body, `is None` branch): ran via the retired implicit operator → SPY0604 — the R-G contract already enforced in the same scope; the only "worked → refused" class in this plan, and it is the ruled language. Everything else: ICE → diagnostic (#1761 #1762 #1767 #1768 #1769 `in`, `Box.Empty()`), refused → runs (#1763, R-V, `??=` constants, LiteralString ops, `sum`, mutators), silent-wrong → correct (`split`, non-literal into `LiteralString` in a block, `Some(...)` default that was never omitted). `d = None()`/`d = Some(v)` inside a narrowing MUST keep running (declared-slot fallback).
- **Blast radius:** `Semantic/` + `CodeGen/` + `Core/` → `Sharpy.Stdlib.Tests` (its BUILD: spy-sourced stdlib and spy-tests may contain nested-block stores into `T?`/`T | None` and `LiteralString` uses — regenerate at the START of Phase 1 and read the diff as findings), `Sharpy.Cli.Tests` (incremental E2E), `FrontEndParityTests` (LSP; `Symbol.CodeGenInfo.IsCompileTimeConstant` consumers), the three GapDiscovery sweeps, `ColdWarmBuildDifferentialTests`, the `if x is None: x = value` pattern occurs at **7 sites in 7 files** under `src/` (6 `Sharpy.Compiler.Tests` fixtures, 1 `Sharpy.Stdlib` spy source; regex `if X is None:` followed by `X = <not None/Some(/None()>` @ dff55b2cd) [CORRECTED: was "~90 fixtures", a derived number; the wider class — any nested-block or closure bare store into a `T?` name — is unmeasured by me, and the Phase 1 whole-solution gate is what measures it] — each `T?` site becomes SPY0604 (rewrite to `Some(...)` as the R-G migration continues; a fixture that stops compiling is a finding, not noise).
- **Generated artifacts touched:** spy-stdlib C# (Phases 1, 7), spy-test C# (Phases 1, 7), stdlib docs (Phase 6 — Core `Sum` overloads), oracle ledger (Phase 9). Regen after Phase 1's first commit, after Phase 6, after Phase 7, and at close-out; the `/push` gates are the evidence, never a regen script's "no diff" (2026-09-04 lesson): `bash build_tools/check_spy_staleness.sh`, `bash build_tools/check_spy_tests_staleness.sh` (exit codes), `python3 -m build_tools stdlib generate --force` + `git status --short -- docs/stdlib`, `python3 -m build_tools.cpython_oracle ledger --write` + `git status --short -- build_tools/cpython_oracle/ledger.yaml` [CORRECTED: only two `check_*` scripts exist; the docs and ledger gates are generate-and-diff]. No `.spy` source under `src/Sharpy.Stdlib/spy` or `src/Sharpy.Stdlib.Tests/Spy` uses `LiteralString` @ dff55b2cd (the only grep hit is a test *name*, `TestLoadsLiteralString`, in generated `toml_module_tests.cs`), so the Phase 7 regen is expected to be a no-diff — still run the gates [ADDED: missing step].

## Design Decisions

1. **One predecessor lookup for every identifier store.** `CheckAssignment`'s identifier arm uses the store predecessor (same scope, else enclosing scope — the walrus arm's `Lookup(searchParents: false) ?? Lookup(searchParents: true)` chain filtered by `ExpressionRebindingPredecessor`) [CORRECTED: the helper filters; the chain looks up] for the seam call, not `existingSymbol` alone. The chain crosses the function boundary, so the closure cell (a nested `def` storing to the enclosing function's name) is decided by the same call — the Phase 1 matrix names it [ADDED: adequacy]. Axiom 1: the emitted C# local keeps its declared type across nested blocks, so the store is into the declared slot wherever it is written.
2. **R-T is a two-slot classification at the identifier-store seam, payload first.** `StoreSlotFor(name, declared)`: when the target currently carries a `RemoveNone` fact (`ResolveNarrowedTypeFromFacts`) and the declared type is `OptionalType`/`NullableType`, classify against the payload; accepted → the store is a payload store (Optional: record the wrap fact; binding version typed as the payload); refused → classify against the declared type; accepted → a declared-slot store (no wrap, version typed by the existing rebinding rule); refused → report against the **payload** slot with a narrowing-aware steer. Applies to the five identifier-store arms: plain, walrus, tuple-element, augmented, `??=`.
3. **A rebinding-narrowed read of a wrapper slot carries its accessor** — the read-side twin of R-T, at `CheckIdentifier`: a version whose type is the payload of the root binding's `OptionalType` (→ `UnwrapOptional`) or value-type `NullableType` (→ `NullableValue`; reference → `NullForgiving`) records a `NarrowedReadLowering`. Fixes the pre-existing CS0266 (`n = 5; m: int = n`) and makes narrowing survive a payload store. A walrus node is such a read.
4. **Augmented assignment preserves `RemoveNone` facts** in `NarrowingFlowAnalysis.Kill` (syntactic and sound: an augmented result on a narrowed name is payload-typed by construction — the checker refuses otherwise; `??=` on a non-None name is a no-op). `IsType` facts keep today's kill.
5. **One node-keyed wrap fact.** `SemanticInfo.SetOptionalStoreWrap(Node store, OptionalType wrapAs)` keyed on the `Assignment`/`WalrusExpression` (+ `MergeFrom`), read by the emitter's existing `WrapInOptionalSome` seam at four sites: plain store value, augmented result, `??=` RHS branch, walrus value. Rule 2: the checker decides, the emitter prints.
6. **`??=` is a store into the left slot** (`StorePosition.CoalesceAssign`): whole-slot first (`T?` into `T?`, `T | None` into `T | None`), then payload (constant/float32/decimal/literal/conditional arms apply; Optional → wrap fact); refusals through `CheckStore` so cross-family cells carry the `maybe`/unwrap steers. `None`/`None()` RHS keep today's refusals (SPY0222 / SPY0244), classified before the seam.
7. **An element or body position reports its own refusal and the container adopts the slot.** `AdmitCollectionElements`/`AdmitTupleElements` return `{Admitted, RefusedReported}`; on refusal the literal takes the expected type marked as error recovery so the enclosing store never re-reports. The unannotated lambda body under an expected `FunctionType` reports through `CheckStore` and returns the expected return type after a refusal. The all-`None` arm (`ResolveVoidElementType`) runs only when no expectation exists.
8. **Walrus pushes its slot and reads as its version.** `EnterStore(StorePosition.Walrus, DeclaredBindingType(predecessor))` around the value; one `RebindingVersionType(verdict, valueType, slot)` helper shared by the plain-store and walrus arms (loose slots version by the value's type; Optional slots by the declared type unless payload-accepted; float32/decimal by the narrowed type).
9. **One emittable-constant classifier** (`Semantic/Validation/ConstantDefaultClassifier`): `Classify(expr, constResolver) → EmittableConstantKind` {Literal, NegatedLiteral, FoldedOfAdmitted, ConstReference, EnumMember, NoneLiteral, NoneCall, TypeOf, ConditionalOfAdmitted, TupleLiteral, CaseConstructor (`Some`/`Ok`/`Err`), Collection, Comprehension, Call, Lambda, Other} plus per-site admission tables {ParameterDefault, LambdaParameterDefault, DecoratorArgument, ModuleConst} — exactly what the emitter prints as a C# constant or `default`. `DefaultParameterValidator` (def + lambda), `DecoratorValidator`, and `CodeGenInfoComputer` consult it; the emitter's `IsCompileTimeLiteral` fallback (`Assignments.cs:1139`) is deleted (Rule 2). One `GenerateParameterDefault` helper serves the four emitter default sites.
10. **User generic union cases infer from the slot, else from the arguments, never open.** `CheckUnionCaseConstruction` reuses the `TryInferConstructorTypeArgs` pattern (synthetic `FunctionSymbol` from the case fields + the union's type parameters) and **returns the substituted closed `GenericType` as the call's type — that is the fact the emitter reads**: the generic-union case route (`RoslynEmitter.Expressions.Access.cs:452-465`) prints `Box<int>.Full(…)` from `GetExpressionType(call)` only when every type argument is non-`Unknown`, and otherwise falls back to the bare `Box` name (→ CS0305); `GetInferredTypeArguments` is read only on the builtin-call routes (`:287`, `:349`, `:1187`), so recording it here would be an unread fact (§4 recorded ≠ applied) [CORRECTED: was "records `SetInferredTypeArguments`"]; failure → SPY0227 with the annotation steer and `Unknown`; the argument diagnostic runs only with substituted field types. Builtin `Optional.Some`/`Result.Ok` refusal and name reservation are Batch 3 (R-S).
11. **`OperandView`** (`TypeChecker`/`TypeInferenceService`): `LiteralStringType → SemanticType.Str` at every value-use entry; a `LiteralString`-typed identifier read is literal-derived (so `x + "b"` stays admissible into a `LiteralString` slot — #1741's binary cell); `ResolveBuiltinTypeInfo` routes `LiteralStringType` to `str`'s `TypeSymbol`. The store seam is untouched (it already sees `LiteralString <: str`).
12. **`sum` binds its own width**: Core `Sum(IEnumerable<sbyte|byte|short|ushort>) → int`, `Sum(IEnumerable<uint>) → uint`, `Sum(IEnumerable<ulong>) → ulong` (checked accumulation, like the `int` form's LINQ `Sum` throwing on overflow) with `start` twins; the recorded type is C#'s arithmetic width for the element (sbyte arithmetic is `int`). `in`/`not in`: the needle is an argument into the element slot (`IsArgumentAssignable` with the needle node) — constants convert, variables that C# cannot convert are refused by name with the cast steer.
13. **Serializer totality by reflection**: register `LiteralStringType` **and `SelfType`** [CORRECTED: `SelfType` is a live warm ICE — a `def clone(self) -> Self` method in a two-file `--incremental` project ICEs SPY0909 on the second build, measured 2026-09-04 @ dff55b2cd]; the guard enumerates every concrete `SemanticType` subclass and requires a round-trip or membership in an explicit **one-name** exclusion set `{ConstructorReferenceType}` whose reason is itself asserted by a positive control (exporting `MK = C` from `lib.spy` is refused SPY0342 at the cold build, so the kind cannot reach `SerializeType`). An exclusion whose kind CAN reach the serializer is the exemption-as-subject failure (§2): the guard would stay green with the ICE present.
14. **Spec is authoritative and lands first** (Rule 7): every sentence this plan adds is executed against HEAD before commit (§8).

## Implementation Checklist

- [x] Phase 0 — spec first + regen baseline (fdbe1db2f, 8bfb6a9e4, 9c37b17e3)
- [x] Phase 1 — identifier-store seam across scopes; R-T payload rule; rebinding-narrowed read accessor; wrap fact; Class B drains (b397c5ecc, 2e45f55df) — Tasks 1-5 done; Task 6 (matrix + regen) deferred to close-out
- [x] Phase 2 — `??=` is a store (#1767, Class C drains) (3cd5dc3e8) — FixtureAllowlist empty
- [x] Phase 3 — element/body positions report themselves (#1759 #1760 #1761); walrus slot + R-V (#1763) (514af5ff8, 7f3126172, 8c156ca0c) — KnownRed 17→5
- [x] Phase 4 — one emittable-constant classifier (#1762 #1769) (659141d40, dfcdd47fa, 320410dad) — KnownRed 5→0, ParameterDefaultConstantMatrixTests
- [x] Phase 5 — user generic union case inference (#1770 user half) (6af810f10, 06dd19eb6) — 10 guard cells, mutation-tested
- [x] Phase 6 — promotion consumers `in` + `sum` (#1750 #1749) (78be6c8ae, 18f35922e, f918788db, eee6bf973) — 48 consumer cells, #1748 cross-linked
- [x] Phase 7 — LiteralString operand view (#1766, #1741 cell) + serializer totality (#1751) (df97eab22, eac345708, 25310eac1, deb19657e) — twin matrix, codec totality, regen
- [x] Phase 8 — mutator ring (#1754) (686d71d66, 101d7f916) — 48 cells + 15 augmented twins, mutation-tested
- [x] Phase 9 — close-out: roster, regen, gate, sweeps, issues (06e0d86d1, 25310eac1, 3a500c39b, 852bf488b) — staleness gates clean, format applied, regressions fixed

## Implementation

### Phase 0: Spec first, regen baseline, pin

**Goal:** the language says what the seams will enforce; the generated artifacts are proven fresh before any codegen-touching commit.

**Acceptance:**
- Every new spec example executes against HEAD with the stated output/diagnostic (record `what ran @ sha` in the commit body).
- `check_spy_staleness.sh` [CORRECTED: was `check_spy_stdlib_staleness.sh`, which does not exist], `check_spy_tests_staleness.sh` (exit 0), and the stdlib-docs and oracle-ledger generate-and-diff gates (empty `git status`) all clean @ dff55b2cd (baseline, so a later diff is attributable).
- Allowlist drains: none.

#### Tasks

1. **Baseline the staleness gates** — `build_tools/`
   - Run the four `/push` gates [CORRECTED: only two are `check_*` scripts]: `bash build_tools/check_spy_staleness.sh`; `bash build_tools/check_spy_tests_staleness.sh`; `python3 -m build_tools stdlib generate --force && git status --short -- docs/stdlib`; `python3 -m build_tools.cpython_oracle ledger --write && git status --short -- build_tools/cpython_oracle/ledger.yaml`. Record the two exit codes and the two `git status` outputs @ dff55b2cd in the phase's first commit body. No regen script output counts as evidence.
   - No commit if clean; otherwise stop and report (a red baseline is a pre-existing fossil).

2. **Spec: parameter defaults (R-R)** — `docs/language_specification/function_default_parameters.md`
   - Table: add rows `None()` (Optional slot only), and the refused set: `Some(…)`/`Ok(…)`/`Err(…)`, tuple literals, constructor calls, lambdas → SPY0401. Add the steer example: `def f(x: int? = None()) -> None: x ??= Some(42)`. Note option C (forward-overload synthesis) as the recorded future lowering in an *Implementation* bullet.
   - Update `null_coalescing_assignment.md` §Optional: state that a bare payload (`x ??= 42`) and an Optional (`x ??= Some(42)`) are both accepted, a constant converts to the payload width (`y: int8? = None(); y ??= 7`), and a `T | None` value into a `T?` slot is refused with the `maybe` steer (and the reverse with the unwrap steer). Every example executed.
   - Commit: `docs(spec): parameter defaults refuse Some/Ok/Err and tuple literals by name (R-R); ??= is a store into the left slot`

3. **Spec: R-T carve-out and R-V** — `docs/language_specification/tagged_unions_optional.md` (under *Creating Optional Values*), `type_narrowing.md` **§"Stores Use the Declared Type" (`:42-78`) — the section that today states the rule R-T refines ("never against the narrowed type of the previous value or of an enclosing `assert`/`if`"); rewrite it payload-first, declared-second, and keep its executed example true (`x = None` on `str | None` and `b.v = None` are declared-slot stores under R-T)** [CORRECTED: the plan named the file but not the section it contradicts], `walrus_operator.md`
   - R-T sentence: inside a narrowing of `x: T?`, a store of a `T`-shaped value re-wraps and the narrowing survives (`x += 5`, `x = 5`; printed `15`/`5` and a following `n: int = x` runs); a store of `None()`/`Some(…)` is a store into the declared slot and ends the narrowing; a narrowed read stored into a slot of its own declared type passes the Optional through. The nested-block sentence: a store to a name from an enclosing block is a store into that name's declared slot (`d: int? …; if True: d = 5` is SPY0604 — use `Some(5)`).
   - R-V sentence in `walrus_operator.md`: a walrus is a store followed by a read of its target; its type is what the target reads as after the store (`m: int | None; n: int = (m := 5)` runs; `x: int? = None(); y: int? = (x := Some(2))` runs; `Some`/`None()` take the target's slot).
   - Commit: `docs(spec): payload rule for stores into a narrowed T? (R-T), enclosing-block stores, walrus is value-typed (R-V)`

4. **Spec: membership, sum, LiteralString surface, user-union spelling** — `membership_operators.md`, `builtin_functions.md`, `literal_string.md`, `tagged_unions.md:172-220`
   - `membership_operators.md`: new §Needle type — the needle is checked against the container's element type as an argument: a constant converts (`2 in xs` over `list[uint64]`), a variable C# cannot convert is SPY0222 naming both types with the cast steer (`n: int32 in list[uint64]`), a non-numeric mismatch (`"a" in list[int]`) is SPY0222, a `T?` needle needs narrowing/unwrapping; tuples are not containers (SPY0320, filed separately).
   - `builtin_functions.md` `sum` row: every integer width; result is the element's arithmetic width (`int8 → int32`, `uint32 → uint32`, `uint64 → uint64`); overflow raises.
   - `literal_string.md` §Type Relationship: enumerate the use surface (operators, comparison, `len`, indexing, slicing, iteration, `in`, truthiness, methods incl. `split` returning `list[str]`, f-strings); `x + "b"` is still literal-derived, `x + s` is not (`x += s` is SPY0220). Replace the *Diagnostics* example of `==` if present.
   - `tagged_unions.md`: replace "Short for `Result.Ok(42)`" with the R-S split — bare `Ok/Err/Some/None()` are the builtins' only spelling; user unions are constructed as `Union.Case(…)`, whose type arguments come from the annotation or the arguments (`b = Box.Full(1)` is `Box[int]`; `Box.Empty()` without an annotation is SPY0227). Note that the qualified builtin forms are refused in a later batch (#1758, Batch 3).
   - Commit: `docs(spec): in/not in needle rule (R-U), sum widths, LiteralString use surface, user-union case spelling (R-S)`

### Phase 1: The identifier-store seam reaches every scope; R-T; the wrap fact (#1768, #1755 B)

**Goal:** every identifier store — same scope, nested block, narrowed or not — is decided by `ClassifyStore`; a payload store into a narrowed wrapper re-wraps and the narrowing survives; the `FixtureAllowlist` Class B rows drain.

**Acceptance (class-level):**
- Matrix: `StoreTargetMatrixTests` gains the **mistyped-store product**: target {local, self.field, obj.field, tuple-element attribute, walrus, index} × scope {same, nested block (`if True:`), narrowing block (`if x is not None:`), `while` body, `for` body, `try`/`with`/`else` body, **nested `def` body (closure store to the enclosing function's name)** [ADDED: adequacy — `d: int = 1; def inner() -> None: d = "s"` ICEs CS0029 @ dff55b2cd]} × family {`T?`, `T | None`, plain `T`} × value {payload-convertible constant, mistyped, `None`, `None()`, `Some(v)`} — every cell prints its value or names its diagnostic (SPY0220 / SPY0604 / SPY0229); **SPY0908 in no cell**; `MatrixIsTotalOverItsAxes` anchored to literal axis sizes; N/A cells declared with reasons (e.g. `None()` into a plain `T`; the binder forms `for d in …`/`except … as d`/`match … case d`, which declare a fresh block-scoped name — measured: the outer value prints after the block) [ADDED: adequacy].
- Survival cells (executing): `x += 5; n: int = x` → 15; `d = 5; e: int = d + 1` → 6; `n: int | None = None; n = 5; m: int = n` → 5; `if a is not None: b = a; c: int = b` → SPY0220 (pass-through); a read after the block → SPY0220 (join).
- Nested-block value shapes: float32 literal, int8 conditional, out-of-range constant, `str` into `LiteralString` — run or refuse as the same-scope twin does (five executing cells + two refusals).
- Multi-file cell: the wrap fact survives `MergeFrom` (a two-file `ProjectCompilationHelper` program with `x += 5` under narrowing prints 15).
- Guard mutations (recorded in the commit body): (a) restore `if (existingSymbol is VariableSymbol …)` → nested-block mistyped cells red by ICE; (b) drop the `OptionalStoreWrap` read in `GenerateAssignment` → `d = 5` narrowed cell red (corpus arm + survival cell); (c) remove the read-side accessor (Decision 3) → `d + 1` cell red (CS0019); (d) revert the `Kill` exemption → augmented survival cell red (SPY0220).
- Allowlist drains: `EmittedOptionalConstructionConformanceTests.FixtureAllowlist` rows `type_system/optional_augmented_assign_narrowing`, `algorithms/linked_list` deleted (stale-entry assertion goes red first, then the rows are removed).
- Execution evidence: the matrix + survival cells @ sha; regen of spy-stdlib and spy-test C# with the diff read as findings (every spy source that stops compiling is either an R-G migration to `Some(...)` or a defect — say which).

#### Tasks

1. **Seam call from the store predecessor** — `Semantic/TypeChecker.Statements.cs` (identifier arm), `Semantic/TypeChecker.cs` (`DeclaredBindingType`)
   - Replace the `existingSymbol`-gated block with a `storePredecessor`-gated one; keep the `VoidType` arm but route it through the same call. Extract `RebindingVersionType(verdict, valueType, slot)` (Decision 8) and use it here.
   - Acceptance: nested-block product cells (plain `T`, `T?`, `T | None`) — mistyped → SPY0220/SPY0604, float32/int8/conditional → run; the closure cell (`def inner(): d = "s"`) → SPY0220 [ADDED: adequacy].
   - Commit: `fix(semantic): an identifier store into a name from an enclosing block is decided by the store seam — nested-block stores ICEd CS0029/CS0031/CS0266/CS0664 or silently admitted a str into LiteralString (#1768)`

2. **R-T payload rule + wrap fact + version typing** — `TypeChecker.StoreConversion.cs` (new `StoreSlotFor`, `StoreVerdict.AcceptedPayloadRewrap`), `SemanticInfo.cs` (`SetOptionalStoreWrap`/`GetOptionalStoreWrap` + `MergeFrom`), `Statements.cs` (plain + augmented arms), `Statements.Patterns.cs` (tuple-element arm), `Expressions.cs` (walrus arm)
   - Two-slot classification (Decision 2) in ONE helper consumed by the five identifier-store arms; the augmented arm records the wrap when the declared root is `OptionalType` and the target read is narrowed; the refusal message adds the steer: `— 'd' is narrowed from 'int32?' here: a value of type 'int32' re-wraps; use Some(...) or None() for an Optional`.
   - Acceptance: narrowing-block product cells; multi-file cell.
   - Commit: `feat(semantic): payload rule for stores into a narrowed T?/T|None (R-T) — one node-keyed OptionalStoreWrap fact, merged, for plain/augmented/tuple/walrus stores (#1755 B, #1768)`

3. **Rebinding-narrowed read accessor** — `TypeChecker.Expressions.cs` (`CheckIdentifier`), `Utilities.cs` (`LoweringForRemoveNone` reuse)
   - Decision 3; record via `RecordNarrowedReadLowering`.
   - Acceptance: survival cells incl. the pre-existing CS0266 cell.
   - Commit: `fix(semantic): a rebinding-narrowed read of an Optional/nullable-value slot carries its accessor — n = 5; m: int = n ICEd CS0266 (#1768, R-T)`

4. **Augmented stores keep `RemoveNone` facts** — `Analysis/ControlFlow/NarrowingFlowAnalysis.cs` (`Kill`)
   - Decision 4; document the soundness argument in the method comment.
   - Commit: `fix(analysis): an augmented assignment keeps a RemoveNone narrowing on its target — the result is payload-typed by construction (R-T, #1755 B)`

5. **Emitter reads the wrap fact** — `CodeGen/RoslynEmitter.Statements.Assignments.cs` (`GenerateAssignment` value, augmented result, `GenerateStore`), `RoslynEmitter.Expressions*.cs` (walrus)
   - One `ApplyOptionalStoreWrap(Node store, ExpressionSyntax value)` that calls `WrapInOptionalSome`; walrus output passes through the read-lowering seam (`.Unwrap()`/`.Value` when the version is payload-typed).
   - Acceptance: corpus arm rows drain; `emit csharp` of the augmented fixture shows `x = Optional<int>.Some(x.Unwrap() + 5)`.
   - Commit: `fix(codegen): stores into a narrowed Optional print Optional<T>.Some(...) from the recorded wrap fact — drains two #1755 Class B FixtureAllowlist rows`

6. **Matrix + regen** — `Integration/StoreTargetMatrixTests.cs`, `build_tools/regenerate_spy_stdlib.sh`, spy-test regen
   - Mistyped-store product (generated, totality-anchored), survival cells, multi-file cell; regenerate both spy C# corpora; fix every `.spy`/fixture the new refusals reach (`Some(...)` migration) in the same or next commit, listing each in the body.
   - Commit: `test(semantic): StoreTargetMatrixTests mistyped-store product over target × scope × family × value; survival + multi-file cells; regen spy C# after the seam (#1768)`

### Phase 2: `??=` is a store into the left slot (#1767, #1755 C)

**Goal:** the `??=` RHS is decided by the seam; cross-family cells refuse with steers; constants convert; the Optional payload form wraps.

**Acceptance:**
- Matrix: `StorePositionReachTests` gains the `CoalesceAssign` table: left ∈ {`T`, `T?`, `T | None`} × right ∈ {`T`, `T?`, `T | None`, `None`, `None()`, in-range constant into `int8?`/`int8 | None`, float32 literal into `float32?`, conditional of constants, mistyped `str`} × target kind {local, member, index, narrowed local} — runs with printed value / SPY0220+steer / SPY0222 / SPY0244; SPY0908 in no cell.
- Guard mutation: route `??=` back through `InferAugmentedAssignmentType` alone → the `int8?` constant cell red (SPY0222) and the cross-family cell red by ICE.
- Allowlist drains: `FixtureAllowlist` row `type_system/null_coalescing_assignment_optional` deleted.
- Execution evidence: the table @ sha; `emit csharp` shows `x = x.IsSome ? x : Optional<int>.Some(42)`.

#### Tasks

1. **Seam arm** — `TypeChecker.StoreConversion.cs` (`StorePosition.CoalesceAssign`, `FormatStoreError` arm), `TypeChecker.Statements.cs` (`NullCoalesceAssign` branch before the operator path)
   - Decision 6; keep `OptionalCoalesceBothOptional` for `T?` RHS; add the wrap fact for payload RHS.
   - Commit: `fix(semantic): ??= is a store into the left slot decided by the seam — cross-family cells ICEd CS0029, in-range constants were refused (#1767)`
2. **Emitter branch wrap** — `RoslynEmitter.Statements.Assignments.cs` (`GenerateNullCoalesceValue`)
   - Commit: `fix(codegen): ??= on an Optional wraps a payload RHS in Optional<T>.Some from the recorded fact — drains the #1755 Class C row`
3. **Table + drain** — `Semantic/StorePositionReachTests.cs`, `CodeGen/EmittedOptionalConstructionConformanceTests.cs`
   - Commit: `test(semantic): ??= left × right × target table in StorePositionReachTests; FixtureAllowlist empty (#1767, #1755)`

### Phase 3: Element and body positions report themselves; the walrus pushes its slot (#1759 #1760 #1761 #1763, R-V)

**Goal:** 12 KnownRed rows drain; the seam's code and steer reach the user at every element/body position; the walrus infers `Some`/`None()` from its target and reads as its version.

**Acceptance:**
- `StoreConversionMatrixTests.KnownRedCells` loses the #1759 (5), #1760 (2), #1761 (2), #1763 (3) rows; `KnownRedCellCount` → 5 (the #1762 rows, drained in Phase 4); the skipped `KnownRedCell_MeetsTheSeamContract` theory is un-skipped for those rows (each executes or names its code + steer).
- Sibling hosts: set/dict-value/dict-key/nested-list/tuple-in-list/return/argument element cells (SPY0604 at the element, exactly ONE diagnostic), the argument-position lambda (`apply(lambda: None)` → SPY0229), `lambda: None()` into `() -> int?` keeps running, `[None]` into `list[int | None]` keeps running.
- R-V read cells: `n: int = (m := 5)` → 5; `s: str = (o := "s")` → s; `take((m := 5))` → 5; `y: int? = (x := Some(2))` → 2; walrus under `if`/`while` tests with `Some(...)`.
- Guard mutations: (a) restore the container-level re-report → the element cell red on "exactly one diagnostic"; (b) remove the walrus `EnterStore` → `Walrus×SomeIntoOptional` red; (c) revert `RebindingVersionType` in the walrus arm → the R-V `int32 | None` cell red.
- Allowlist drains: 12 KnownRed rows.

#### Tasks

1. **Element/tuple/body positions report** — `TypeChecker.Utilities.cs` (`AdmitCollectionElements`, `AdmitTupleElements`), `TypeChecker.Expressions.Literals.cs` (list/set/dict/tuple literals — admission before `ResolveVoidElementType`), `TypeChecker.Expressions.Access.Lambdas.cs` (unannotated body under an expected return)
   - Decision 7. The dict literal reports keys and values separately.
   - Commit: `fix(semantic): a refused collection element, tuple element or lambda body reports the seam's own diagnostic at its span and the container never re-reports; an all-None literal consults its slot first (#1759, #1760, #1761)`
2. **Walrus slot + R-V** — `TypeChecker.Expressions.cs` (`CheckWalrusExpression`)
   - `EnterStore(StorePosition.Walrus, …)`; shared `RebindingVersionType`.
   - Commit: `fix(semantic): a walrus pushes its target's declared slot as the expected type and reads as its rebinding version (#1763, R-V)`
3. **Drain the matrix** — `Semantic/StoreConversionMatrixTests.cs`, new fixtures `errors/element_store_reports_seam_1759`, `expressions/walrus_value_typed_rv` (+ `.expected`)
   - Commit: `test(semantic): drain 12 KnownRed store cells (#1759 #1760 #1761 #1763); element-host and walrus R-V cells`

### Phase 4: One emittable-constant classifier (#1762, #1769, R-R)

**Goal:** three constant-ness formulas become one classifier with per-site admission tables; every default the validator admits is one the emitter prints; the last 5 KnownRed rows drain.

**Acceptance:**
- Matrix: new `ParameterDefaultConstantMatrixTests`: kind {literal, negated literal, folded, const ref, enum member, `None` (nullable), `None()` (Optional), `Some(const)`, `Ok(const)`, `Err(const)`, tuple, nested tuple, list, dict, call, constructor, lambda, conditional-of-constants} × host {`def`, method, `__init__`, lambda, decorator argument} → runs with printed value / SPY0401 (+ the Optional steer text for `Some`) / SPY0400 / SPY0447-family for decorators; SPY0908 in no cell; totality anchored.
- `KnownRedCells` → empty; `KnownRedCellCount` → 0; the skipped theory has no skip.
- Emitter: `GenerateParameterDefault` is the ONLY default-printing path (grep `WithDefault(` in `CodeGen/`: **6 hits @ dff55b2cd is the positive control; exactly the helper's one hit after**) [CORRECTED: the narrower pattern `WithDefault(EqualsValueClause(GenerateExpression(` misses the two multi-line sites at `Constructors.cs:433` and `Calls.cs:181`]; lambda `None()` default prints `default`. [VERIFIED 2026-09-05: NOT met literally — the helper returns the clause, so five caller sites still spell `WithDefault(GenerateParameterDefault(` and the sixth is the late-bound `x_lb = null` sentinel; enforced structurally by `EmitterParameterDefaultSeamConformanceTests` instead of the grep]
- Guard mutation: re-admit `TupleLiteral` in the `ParameterDefault` table → the tuple cell red by ICE (CS1736); drop the lambda arm of `DefaultParameterValidator` → the lambda `Some` cell red by ICE.
- Rule 2: `IsCompileTimeLiteral` deleted from the emitter; `CodeGenInfo.IsCompileTimeConstant` computed by the classifier (module-const cell: `const A: int = 100 + 100` still emits `const`).

#### Tasks

1. **Classifier + tables** — new `Semantic/Validation/ConstantDefaultClassifier.cs`; `DefaultParameterValidator.cs` (def + `VisitLambdaExpression` — **there is no lambda visit today** (`VisitFunctionDef` only, `:32`); override `AstVisitor.VisitLambdaExpression` (`AstVisitor.cs:438`, virtual; `VisitExpression → DefaultVisit` descends into call arguments, so lambdas inside bodies are reached) [ADDED: missing step]), `DecoratorValidator.cs`, `CodeGenInfoComputer.cs`
   - Decision 9; roster the classifier's switch in `DispatchSiteInventoryTests` (`guarded-by:ParameterDefaultConstantMatrixTests`) and retire the two `IsCompileTimeConstant` rows.
   - Commit: `refactor(semantic): one emittable-constant classifier with per-site admission tables replaces three constant-ness formulas; Some/Ok/Err and tuple defaults are SPY0401 with the None()/??= steer (R-R, #1762, #1769)`
2. **One emitter default helper** — the **six** `WithDefault(` sites @ dff55b2cd [CORRECTED: was "four"]: `RoslynEmitter.TypeDeclarations.cs:178,232`, `RoslynEmitter.Expressions.Access.Calls.cs:181` (lambda local function — prints `GenerateExpression(param.DefaultValue)` verbatim today, which is why `LambdaParameterDefault×NoneCallIntoOptional` ICEs while the `def` twin runs), `RoslynEmitter.ClassMembers.Constructors.cs:299,433`, `RoslynEmitter.ClassMembers.Dataclass.cs:191`; delete the `IsCompileTimeLiteral` fallback call at `Statements.Assignments.cs:1139` **and its definition at `RoslynEmitter.Statements.cs:855`** [CORRECTED: the definition is not in Assignments.cs]
   - Commit: `refactor(codegen): one GenerateParameterDefault for def/lambda/constructor/dataclass defaults — None()/None on an Optional slot prints default everywhere; emitter constant re-derivation deleted (#1762)`
3. **Matrix + drain** — new `Semantic/ParameterDefaultConstantMatrixTests.cs`; `StoreConversionMatrixTests.cs`; fixture `errors/parameter_default_some_refused_1762`
   - Commit: `test(semantic): parameter-default constant matrix (kind × host); StoreConversionMatrixTests KnownRed empty (#1762, #1769)`

### Phase 5: User generic union case constructors infer, never open (#1770 user half)

**Goal:** `Union.Case(args)` on a generic user union takes its type arguments from the slot or the arguments; no diagnostic renders `T`; `Box.Empty()` without a slot is SPY0227.

**Acceptance:**
- `UnsubstitutedTypeParameterGuardTests` gains the qualified-union route: cells {print, untyped local, `bool` slot, two-parameter union, list-literal arg, nested-call arg, lambda arg, `Empty()` no slot, mistyped with slot, wrong arity} — each runs or names only CLOSED types; the `Empty()` cell is SPY0227 not CS0305; positive control: the `Box[U]` in-scope cell still names `U`.
- Executing: `b = Box.Full(1); print(b)`, `p = Pair.Both(1, "s")`, `Box.Full(mk())` print their C# `ToString` (compare with the slot-present twin's output).
- Guard mutation: return the open `GenericType` when the slot is absent (today's behavior) → the print cell renders `T` and `Box.Empty()` returns to CS0305 → red. The emitter read is `GetExpressionType(call)` at `Access.cs:452-465`; recording only `SetInferredTypeArguments` is inert on this route (Decision 10) [CORRECTED: recorded ≠ applied].

#### Tasks

1. **Inference at the case-constructor route** — `TypeChecker.Expressions.Access.Calls.cs` (`CheckUnionCaseConstruction`)
   - Decision 10.
   - Commit: `fix(semantic): a generic user union's qualified case constructor infers its type arguments from the slot or the arguments and never types open — Box.Full(1) rendered 'T', Box.Empty() ICEd CS0305 (#1770)`
2. **Guard cells + fixture** — `Semantic/UnsubstitutedTypeParameterGuardTests.cs`, fixture `unions/generic_case_constructor_inference_1770`
   - Commit: `test(semantic): qualified-union route joins the unsubstituted-T guard (#1770)`

### Phase 6: Promotion consumers — `in`/`not in` and `sum` (#1750 R-U, #1749)

**Goal:** the needle of `in`/`not in` is a store into the element slot; `sum` binds every integer width; both are cells of the numeric matrix's consumer axis.

**Acceptance:**
- `NarrowWidthArithmeticMatrixTests` gains a generated `Consumers` group: consumer {`sum`, `in`, `not in`} × width (8 integer spellings) × shape {const in range, const out of range, same-width var, narrower-signed var, signed var vs unsigned, non-numeric needle} × container {list, set, dict keys} — run with the printed value (`sum` prints the C# arithmetic width's value; `in` prints `True`/`False`) or SPY0222 by name with the cast steer; SPY0908 in no cell; totality anchored to literals (`3 × 8 × 6 × 3` minus declared N/A).
- Non-numeric/Optional/nullable/user-`__contains__` needle cells: SPY0222 with the appropriate steer; `1 in list[float]` and `n: int8 in list[int32]` keep running; str container with a `str` needle runs, with an `int32` needle is SPY0222.
- `sum` result-type cells: `r: uint64 = sum(list[uint64])` → 6; `r: int8 = sum(list[int8])` → SPY0220 naming `int32` (the arithmetic width); `sum(xs, 10)` start forms per width. (`b: bool = sum(list[int8])` is SPY0220 naming `int32` today — mistyped, not `Unknown`, so the overload family lands on a typed arm.)
- Positive controls (argument-position twins, all SPY0220 by name @ dff55b2cd): `xs.count(n)`, `xs.remove(n)`, `xs.append(n)`, `s.add(n)`, `d[n]` with `n: int32` over `uint64` — the `in` cell's verdict must equal theirs [ADDED: adequacy].
- Guard mutations: (a) skip the needle check → the `int32 in list[uint64]` cell red by ICE; (b) delete the `Sum(IEnumerable<ulong>)` overload → the `uint64` cell red (SPY0354).
- #1748 cross-link: post the measured `first(ys)` cell (`list[int8]` into an invariant `list[int]` parameter → CS1503) as a comment on #1748 with `@ sha`.
- Stdlib docs regenerated after the Core change (diff read).

#### Tasks

1. **Core `Sum` family** — `src/Sharpy.Core/Sum.cs` (+ `Sharpy.Core.Tests`)
   - Decision 12; checked accumulation for `uint`/`ulong`; XML docs; `netstandard2.1` C# 9.
   - Commit: `feat(core): sum over every integer width — sbyte/byte/short/ushort → int, uint → uint, ulong → ulong, with start forms (#1749)`
2. **Needle seam** — `TypeChecker.Expressions.Operators.cs` (**a new** `In`/`NotIn` arm in `CheckBinaryOp` and the comparison-chain link — both routes call one `ClassifyMembership`) [CORRECTED: no such arm exists @ dff55b2cd — `TypeInferenceService.InferBinaryOpTypeUncached:137-139` returns `Bool` for `In`/`NotIn` with "validation is separate", and the only membership check is the container-side `ProtocolValidator.ValidateMembership` (`:200`, SPY0320, validation stage); the needle arm runs only when an element type resolves, so a container without `__contains__` stays exactly one SPY0320], `PromotionRefusalSteer` reuse
   - Element slot: builtin containers via `InferIterableElementType` (dict → keys, str → str), user types via `__contains__`'s parameter type, CLR `Contains` parameter when resolvable, otherwise oblivious. Refusal: SPY0222 `Type '{needle}' does not support operator 'in' with operand of type '{container}'` + cast/unwrap/`maybe` steer.
   - Commit: `fix(semantic): the needle of in/not in is an argument into the container's element slot — a signed variable vs unsigned elements, an out-of-range constant, a str needle over ints and an Optional needle all ICEd CS1503 (#1750, R-U)`
3. **Consumer axis** — `Semantic/NarrowWidthArithmeticMatrixTests.cs`, `docs/design/gap-discovery-contracts.md` (row update), `#1748` comment
   - Commit: `test(semantic): sum and in/not in join the numeric matrix's consumer axis under the totality count (#1749, #1750)`

### Phase 7: LiteralString is a `str` at every use; the serializer is total (#1766, #1741 binary cell, #1751)

**Goal:** every value-use route sees a `LiteralString` as `str` through one view; `split` on a `LiteralString` prints a `list[str]`; a warm build round-trips every `SemanticType`.

**Acceptance:**
- New `LiteralStringSurfaceMatrixTests`: receiver {`str`, `LiteralString`} × operation {`== "a"`, `!= ""`, `< "b"`, `"a" < x < "c"`, `x + "b"` into `str`, `x + "b"` into `LiteralString` (str column N/A), `x + s`, `x * 2`, `len`, `x[0]`, `x[0:1]`, `for c in x`, `"a" in x`, `if x:`, `not x`, `f"{x}"`, `str(x)`, `x.upper()`, `x.split(",")`, `x.replace("a","b",1)`, `x.format(1)`, dict key, `x in list[str]`, `isinstance(x, str)`, `sorted([x, y])`, `match x: case "a"`, `x == y` both LiteralString} → the two columns print identical stdout; refusal twin `x - "a"` refuses identically (SPY0222) in both; the R-P control `x += s` stays SPY0220 on the LiteralString column only.
- `TypeCodecRegistryTotalityTests`: every concrete `SemanticType` subclass (reflection) round-trips a sample instance through `Serialize`/`Deserialize` or is in the exclusion set `{ConstructorReferenceType}` [CORRECTED: `SelfType` is a live warm ICE — a `-> Self` method in a two-file `--incremental` project ICEs SPY0909 on the second build @ dff55b2cd; it must round-trip] with a reason that is itself asserted (the `MK = C` export refusal SPY0342 is the positive control); the exclusion set is asserted equal to exactly that one name.
- `ColdWarmBuildDifferentialTests` gains a `LiteralString` signature/return/field program **and a `def clone(self) -> Self` method program** (cold == warm output) [ADDED: adequacy].
- Guard mutations: (a) revert `RecordStaticExtensionDispatch`'s view → the `split` cell red (`a b` ≠ `['a', 'b']`); (b) revert the binary-operator view → `== "a"` red; (c) unregister `LiteralStringType` → totality guard red and the differential cell red (SPY0909); (d) unregister `SelfType` → totality guard red and the `-> Self` differential cell red (SPY0909) [ADDED: adequacy].
- Regen: spy-stdlib and spy-test C# (LiteralString uses in stdlib sources, if any) — diff read.

#### Tasks

1. **`OperandView` + literal-derived identifier reads** — `TypeChecker.Utilities.cs` (helper), `Expressions.Operators.cs` (`CheckBinaryOp`, `CheckComparisonChain`, unary), `Utilities.cs` (`ClassifyTruthiness`), `Expressions.Access.cs` (`ResolveBuiltinTypeInfo`, `RecordStaticExtensionDispatch`, `ComputeIndexAccessLowering`), `Expressions.Literals.cs` (slice), `Expressions.cs` (`CheckIdentifier` → `SetLiteralDerived`), `TypeInferenceService.cs` (`InferBinaryOpTypeUncached`, iterable element, index result), `Validation/ProtocolValidator.cs` (`HasProtocol`), `Validation/OperatorValidator.cs` (both `Str` sites)
   - Completeness scan recorded in the commit body: `grep -rn "== SemanticType.Str\b\|SemanticType.Str ==\|Name: BuiltinNames.Str\|Name: \"str\"" src/Sharpy.Compiler/Semantic` — every hit is either a use route now behind the view, a store-seam site (unchanged), or listed with a reason. Anchor: **32 hits in 14 files @ dff55b2cd** (`StoreConversion.cs`, `TypeInferenceService.cs`, `Statements.cs`, `Expressions.Access.cs`, `SemanticType.cs`, `TypeUtils.cs`, `Expressions.Access.Calls.cs`, `Utilities.cs`, `Expressions.Operators.cs`, `Expressions.Literals.cs`, `Registry/BuiltinReturnTypeInference.cs`, `Validation/EnumRulesValidator.cs`, `Validation/OperatorValidator.cs`, `Validation/ProtocolValidator.cs`; the task above names 8 of them) — the count is the scan's positive control [ADDED: missing step].
   - Commit: `fix(semantic): a LiteralString is a str at every value-use route through one OperandView — operators, protocols, truthiness, slicing, and the static-extension dispatch that let x.split(",") bind System.String.Split (#1766, #1741 binary cell)`
2. **Serializer** — `Project/SymbolSerializer.cs` (`Register<LiteralStringType>` **and `Register<SelfType>`** [CORRECTED])
   - Commit: `fix(project): SymbolSerializer round-trips LiteralStringType and SelfType — an incremental build with any LiteralString or Self signature ICEd SPY0909 (#1751)`
3. **Guards** — new `Semantic/LiteralStringSurfaceMatrixTests.cs`, new `Project/TypeCodecRegistryTotalityTests.cs`, `Project/ColdWarmBuildDifferentialTests.cs` cell, fixture `types/literal_string_use_surface_1766`
   - Commit: `test(semantic,project): LiteralString twin-surface matrix; SemanticType codec totality by reflection; cold/warm LiteralString cell (#1766, #1751)`

### Phase 8: The mutator ring (#1754)

**Goal:** `list.extend`, the four `set` mutators and `dict.update` accept every source the ring can lower, and agree with their augmented twins.

**Acceptance:**
- New `MutatorIterableArgumentMatrixTests`: mutator {`list.extend`, `set.update`, `set.intersection_update`, `set.difference_update`, `set.symmetric_difference_update`, `dict.update` (pairs)} × RHS shape {list, tuple literal, tuple var, str (str elements), dict (keys / pairs), range, comprehension, set, narrowed Optional tuple} → printed value equals python3's; paired with the augmented twin (`+=` for list, `|=`/`&=`/`-=`/`^=` for set with set RHS only, `|=` for dict) asserting identical output where Python has a twin; mistyped control (`("a","b")` into `list[int]`) stays SPY0220 in both routes; N/A: `set.update(a, b)` (no `params` form), generator-expression argument (parser).
- Guard mutation: remove `list.extend` from the table → the list × tuple cell red (SPY0220).
- Dispatch inventory: `GetMemberIterableKeyPositions` rostered `guarded-by:MutatorIterableArgumentMatrixTests` — **the table must be a `switch` with a default arm**: `DispatchSiteScan` enumerates only `SwitchStatementSyntax`/`SwitchExpressionSyntax` (`DispatchSiteScan.cs:130,140`), and the method is a two-line `if` today (`Calls.cs:4327-4334`, `str.join` only), so an `if`-shaped table leaves the roster row dangling [CORRECTED: roster precondition]. [VERIFIED 2026-09-05: INAPPLICABLE — the table is a `switch` with a default arm since 101d7f916, but its scrutinee is the member-name `string`, outside the inventory's `Node`/`IrNode` universe; guarded by `MutatorIterableArgumentMatrixTests` (mutation: `extend` arm removed → red 4). Recorded in gap-discovery-contracts.md]

#### Tasks

1. **Table** — `TypeChecker.Expressions.Access.Calls.cs` (`GetMemberIterableKeyPositions`)
   - Receiver `GenericType{Name: list|set|dict}` × member; `dict.update` position 0 only when the argument is not a `dict`.
   - Commit: `fix(semantic): list.extend, set.update/intersection_update/difference_update/symmetric_difference_update and dict.update join the iterable-projection ring — tuple/str/dict-keys arguments were refused while the augmented twins ran (#1754)`
2. **Matrix** — new `Semantic/MutatorIterableArgumentMatrixTests.cs`, `Infrastructure/DispatchSiteInventoryTests.cs`
   - Commit: `test(semantic): mutator × RHS-shape matrix paired with the augmented twin (#1754)`

### Phase 9: Close-out

**Goal:** the whole-solution gate, the sweeps, the regen gates and the rosters are green at one sha; every issue closes on its acceptance bullet.

**Acceptance:**
- `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"` across all projects @ final sha: `passed/failed/skipped @ sha (measured)`; `FrontEndParityTests`, `InteropConformance`, `MetamorphicCorpus`, `DifferentialExecution` each run.
- `check_*` staleness scripts exit 0 @ final sha (four exit codes in the commit body).
- `docs/design/gap-discovery-contracts.md` rows updated: store conversion (KnownRed 0, FixtureAllowlist empty, scope axis), numeric (consumer axis), new rows for LiteralString surface, codec totality, mutator ring; `DispatchSiteInventoryTests` rows drained/added.
- Issues to file at close-out: tuple membership (`2 in (1, 2)` SPY0320), `sum(list[bool])`, `set.update(*others)`, generator expression as a call argument, `xs.index(n)` reporting SPY0354 where `xs.count(n)` reports SPY0220 for the same mistyped needle (overloaded-member code divergence, measured @ dff55b2cd) [ADDED: missing step]; comment on #1748 (Phase 6).
- Tag 0.18.0 only after this phase (owner's step, not the plan's).

#### Tasks

1. **Roster + docs** — `docs/design/gap-discovery-contracts.md`, `.github/copilot-instructions.md` (if a new validator/classifier changes the registry listing)
   - Commit: `docs(design): gap-discovery roster for plan C — store scope axis, promotion consumers, LiteralString surface, codec totality, mutator ring`
2. **Gate, sweeps, regen, report** — no source change; the report lists each acceptance bullet with `what ran @ sha`.

## Testing Strategy

- **New fixtures** (`Integration/TestFixtures/`): `errors/nested_block_store_refused_1768.spy` + `.error` (three lines, `@line:col`), `type_system/narrowed_payload_store_survives_1768.spy` + `.expected` (prints 15, 6, 5), `errors/element_store_reports_seam_1759` (`.error` with the SPY0604 steer text, positive control line `xs: list[int | None] = [None]` in `expressions/all_none_literal_takes_slot_1760.expected`), `errors/lambda_none_body_refused_1761`, `errors/parameter_default_some_refused_1762` (`.error` names the `??= Some` steer; control `def f(x: int? = None())` runs in `functions/optional_default_none_call.expected`), `expressions/walrus_value_typed_rv.expected`, `errors/coalesce_assign_cross_family_1767`, `unions/generic_case_constructor_inference_1770.expected`, `expressions/membership_needle_rule_1750.expected` + `errors/membership_needle_refused_1750`, `builtins/sum_every_width_1749.expected`, `types/literal_string_use_surface_1766.expected`, `collections/mutator_iterable_arguments_1754.expected`. Every `.error` fixture has a positive-control `.expected` twin that must keep passing.
- **Edge cells outside the issues** (all enumerated in Current State): nested-block stores for every value shape; `??=` constants/float32/conditional/`int8 | None`; lambda-as-argument body; set/dict/nested/return/argument element hosts; R-V reads; `Ok`/`Err`/`__init__`/nested-tuple defaults; narrowing survival; rebinding value-type nullable read; `Pair.Both`, `Box.Empty()`; non-numeric/Optional/nullable/user-dunder needles; 8 `sum` widths + start forms; the 27 LiteralString operations incl. `split`; the four set mutators + dict pairs.
- **Discriminating outputs:** `split` (`['a', 'b']` vs `a b`); survival cells print a payload arithmetic result (impossible with the fact killed); `sum` prints per-width values; the multi-file wrap cell prints 15 only if `MergeFrom` carries the fact.
- **Spec deliverables of equal weight:** the Phase 0 sentences with executed examples (`function_default_parameters.md`, `null_coalescing_assignment.md`, `tagged_unions_optional.md`, `type_narrowing.md`, `walrus_operator.md`, `membership_operators.md`, `builtin_functions.md`, `literal_string.md`, `tagged_unions.md`).
- **Blast radius & regen timing:** whole-solution gate after Phase 1 (the scope-axis refusals reach stdlib spy sources and ~90 fixtures), after Phase 7, and at close-out; regen after Phase 1, 6, 7 and at close-out with the staleness CHECK scripts as evidence; `FrontEndParityTests` after Phase 4 (CodeGenInfo change) and Phase 7.

## Issues to Close

- #1768 — identifier store into a narrowed target unchecked — Phase 1 Tasks 1–6; **close criterion:** the mistyped-store product green with SPY0908 in no cell @ sha, the `if True: d = "s"` cell SPY0220, mutation (a) recorded red/green.
- #1755 — Optional implicit-operator reliance, Classes B and C — Phase 1 Task 5 + Phase 2 Task 2; **close criterion:** `FixtureAllowlist` empty (stale-entry assertion red before removal, green after) @ sha; survival cells print.
- #1767 — `??=` cross-family ICE — Phase 2; **close criterion:** the 3×3(+shapes) table green, the two ICE cells SPY0220 with steers, `int8?` constant cell runs @ sha.
- #1759 — element/body refusal re-reported — Phase 3 Task 1; **close criterion:** 5 KnownRed rows drained; each element host cell reports exactly one SPY0604/SPY0229 at the element span @ sha.
- #1760 — all-`None` literal ignores the slot — Phase 3 Task 1; **close criterion:** 2 KnownRed rows drained; `[None]` into `list[int | None]` still runs (positive control) @ sha.
- #1761 — `lambda: None` ICE — Phase 3 Task 1; **close criterion:** 2 KnownRed rows drained; argument-position cell SPY0229; `() -> int | None` control runs @ sha.
- #1763 — walrus does not push its slot — Phase 3 Task 2; **close criterion:** 3 KnownRed rows drained; R-V read cells print @ sha; mutation (b) recorded.
- #1762 — `Some(42)` parameter default ICE — Phase 4; **close criterion:** matrix green, `Some`/`Ok`/`Err`/lambda cells SPY0401 with the `None()`/`??= Some` steer, `KnownRedCellCount == 0` @ sha; mutation recorded.
- #1769 — tuple-literal default ICE — Phase 4; **close criterion:** tuple/nested-tuple cells SPY0401 across all hosts; the re-admit-`TupleLiteral` mutation red by ICE, restored green @ sha.
- #1770 (user-generic half) — Phase 5; **close criterion:** guard cells green, `Box.Empty()` SPY0227, no diagnostic renders `T` @ sha; the builtin half stays open for Batch 3 (note in the close comment).
- #1750 — `in` mixed signedness ICE — Phase 6 Task 2–3; **close criterion:** consumer-axis `in` cells green (constants convert, variables refused by name), non-numeric/Optional needle cells SPY0222 @ sha.
- #1749 — `sum` over unsigned — Phase 6 Tasks 1, 3; **close criterion:** all 8 widths run with printed values, `uint64` prints 6, stdlib docs regenerated @ sha.
- #1766 — LiteralString has no operators/protocols — Phase 7 Tasks 1, 3; **close criterion:** the twin matrix green including `split`; mutation (a) and (b) recorded @ sha. (#1741 keeps its Batch 8 placement; its binary-`+` cell is covered by the `x + "b"` into `LiteralString` cell — say so on #1741.)
- #1751 — incremental build ICE on LiteralString signatures — Phase 7 Tasks 2, 3; **close criterion:** warm build of the two-file project succeeds **for both the `LiteralString` and the `-> Self` programs** [CORRECTED], codec totality guard green with the one-name exclusion set asserted, both unregister mutations red @ sha.
- #1754 — mutators never receive a projected argument — Phase 8; **close criterion:** the mutator × shape matrix green with python3-matching output and augmented-twin agreement; table-row mutation red @ sha.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS (with the three adequacy additions below applied inline — the author should confirm the closure cell and the `SelfType` cell in the matrices)
**Verified on:** 2026-09-04 @ dff55b2cd (tree clean; Debug CLI built 16:59 after the 16:51 HEAD commit; every `run` below used that binary)
**Plan file:** `.claude/plans/plan-757fbb.md`

### Corrections Made

Decision-changing:
- **Decision 13 / Phase 7 / #1751 close criterion** — exclusion set `{SelfType, ConstructorReferenceType}` → `{ConstructorReferenceType}`; `SelfType` must be registered. Probe: two-file project, `lib.spy` has `class C: … def clone(self) -> Self`; `sharpyc project x.spyproj --incremental` twice → second build `SPY0909 Unhandled SemanticType in SerializeType: SelfType`. A module-level `MK = C` is refused cold (SPY0301 + SPY0342), so `ConstructorReferenceType` cannot reach the serializer — that refusal is the exclusion's positive control. The cold/warm differential gains a `-> Self` cell and mutation (d).
- **Decision 10 / Phase 5 mutation** — "records `SetInferredTypeArguments`" → "returns the substituted closed `GenericType` as the call's type". The emitter's generic-union case route (`RoslynEmitter.Expressions.Access.cs:452-465`) reads `GetExpressionType(call)` and prints type arguments only when none is `Unknown`; `GetInferredTypeArguments` is read only at `:287`, `:349`, `:1187` (builtin calls). Recording the vector alone is an unread fact.
- **Row 1 / Decision 1 / Phase 1 matrix** — scope axis gains the closure store (nested `def` body assigning to the enclosing function's name): `d: int = 1; def inner() -> None: d = "s"` → SPY0908/CS0029 @ dff55b2cd. `try`/`with`/`else` bodies (measured in Current State, absent from the Phase 1 axis) added; `for` target, `except … as`, `match case` binders declared N/A (fresh block-scoped name — each prints the outer value after the block).
- **Phase 0 Task 3** — the R-T spec edit is anchored at `type_narrowing.md:42` §"Stores Use the Declared Type", which today states "never against the narrowed type … of an enclosing `assert`/`if`"; its executed example must stay true under the declared-slot fallback.

Reference fixes:
- `check_spy_stdlib_staleness.sh` → `check_spy_staleness.sh`; "four `check_*` scripts" → two check scripts + two generate-and-diff gates (`/push` SKILL.md §1.5), Phase 0 and the generated-artifacts bullet.
- `TypeCodecRegistry` "18 kinds @ :1294-1520" → 17 `Register<` calls at `:1302-1516` (class `:1270-1625`).
- Emitter default sites "four" → six `WithDefault(` sites (`TypeDeclarations.cs:178,232`, `Constructors.cs:299,433`, `Calls.cs:181`, `Dataclass.cs:191`); the acceptance grep re-anchored to `WithDefault(` (6 → 1).
- `IsCompileTimeLiteral` — defined at `RoslynEmitter.Statements.cs:855`; `Assignments.cs:1139` is the fallback call.
- Phase 6 Task 2 — no `In`/`NotIn` arm exists in `CheckBinaryOp`; `TypeInferenceService.InferBinaryOpTypeUncached:137-139` returns `Bool`, and the only membership check is `ProtocolValidator.ValidateMembership` (`:200`, container side, SPY0320).
- Phase 4 Task 1 — `DefaultParameterValidator` has no lambda visit today (`VisitFunctionDef` only); `AstVisitor.VisitLambdaExpression` is virtual and reachable.
- Numeric harness families — `MixedFamilies` = {arith, cmp, eq, bitwise}; `**` is in `Ops`/`PowerCases`; min/max is `MinMaxCases`.
- "~90 fixtures" → 7 sites in 7 files under `src/` (regex `if X is None:` + `X = …`); wider class unmeasured by me, the Phase 1 gate measures it.
- `ExpressionRebindingPredecessor` takes two arguments and only filters; the enclosing-scope rule is the `Lookup(false) ?? Lookup(true)` chain at `Expressions.cs:695-696`.
- `null_coalescing_assignment.md:28` → `:26-27`.
- Phase 7 regen: no `.spy` source uses `LiteralString` (only a generated test *name*); expected no-diff, gates still run.
- Phase 8 roster: `DispatchSiteScan` enumerates only `switch` sites (`DispatchSiteScan.cs:130,140`); `GetMemberIterableKeyPositions` is an `if` today (`Calls.cs:4327-4334`).
- Phase 9 files: `xs.index(n)` SPY0354 vs `xs.count(n)` SPY0220 for the same mistyped needle.

### Adequacy
- **Contract:** named — four rows, each with a one-sentence contract (Row 1 "one seam at every store position, whatever scope declared the target"; Row 2 "every promotion consumer decides a pair the same way"; Row 3 "`LiteralString` is a `str` at every use route; the warm build round-trips every `SemanticType`"; Row 4 "a position that accepts an iterable accepts every source the ring can lower").
- **Matrix:** larger than repro lists — 346 probes in `.claude/tmp/plan-757fbb-probes/` (222+74+32+18 `.spy`, ledgers present, re-run of 10 discriminating cells + 13 sibling cells agrees). Cells the verifier found the plan did not name (now inserted): closure store (Row 1), `-> Self` warm ICE (Row 3), `try`/`with`/`else` in the Phase 1 axis, the argument-position twins as Row 2 positive controls.
- **Cure:** seam + harness per row (identifier-store seam across scopes + `StoreTargetMatrixTests` product; needle-as-argument + `sum` overload family under the numeric totality count; `OperandView` + twin matrix + reflection totality guard; one position table + mutator matrix).
- **Harness:** named with allowlist deltas — `StoreConversionMatrixTests` KnownRed 17 → 0 (#1759 5, #1760 2, #1761 2, #1762 5, #1763 3 — counted @ dff55b2cd), `FixtureAllowlist` 3 rows → empty (names verified), `StoreTargetMatrixTests` (axes verified: target 6 × source 3 × shape 2, no mistyped column), `NarrowWidthArithmeticMatrixTests` (consumer arrays verified), `UnsubstitutedTypeParameterGuardTests` (`class Box[U]` positive control at `:97`), `ColdWarmBuildDifferentialTests` (no `LiteralString`/`Self` cell today), `DispatchSiteInventoryTests` rows `RecordIterableArgumentMarks` (`:237`), `DecoratorValidator.IsCompileTimeConstant` (`:112`), `DefaultParameterValidator.IsCompileTimeConstant` (`:114`) all present. New: `ParameterDefaultConstantMatrixTests`, `LiteralStringSurfaceMatrixTests`, `TypeCodecRegistryTotalityTests`, `MutatorIterableArgumentMatrixTests`.
- **Rulings:** R-R (#1762 comment 5546241566), R-T (#1755 comment 5546241731), R-U (#1750 comment 5546236147) — all owner-authored 2026-09-04, content matches the plan's use; R-V and the sequencing ruling present in the charter addendum (`remediation-round-2026-08-30-batching.md:500-501`); R-S in #1770's body. No ruled question is re-asked.

### Falsifiability
- `StoreTargetMatrixTests` mistyped product (Phase 1): mutations (a)–(d) named; exemption not subject; positive control = same-scope twins + R-T direction cells (`1768e_opt_narrowed_none_call_store`, `1768l_opt_narrowed_some_store` RUN today in the ledger); by-direction probe specified (nested bare into `T?` worked → refused is the ruled R-G language).
- `StorePositionReachTests` `CoalesceAssign` table (Phase 2): mutation named (route back through `InferAugmentedAssignmentType`); positive control = `x: int8? ??= 7` SPY0222 today (ledger); direction specified.
- KnownRed drain (Phase 3): mutations (a)(b)(c) named; "exactly one diagnostic" is the container re-report detector; positive control `[None]` into `list[int | None]` runs today.
- `ParameterDefaultConstantMatrixTests` (Phase 4): mutations named (re-admit `TupleLiteral` → CS1736; drop lambda arm → CS1736); positive control `def f(x: int? = None())` runs; absence grep re-anchored (6 → 1).
- `UnsubstitutedTypeParameterGuardTests` union route (Phase 5): mutation corrected to "return the open `GenericType`"; positive control `Box[U]` cell exists.
- Numeric `Consumers` group (Phase 6): mutations named; positive controls added (`count`/`remove`/`append`/`add`/dict index SPY0220 by name today); `b: bool = sum(list[int8])` → SPY0220 naming `int32` (mistyped, so the overload arm fires).
- `LiteralStringSurfaceMatrixTests` (Phase 7): mutations (a)(b) named; discriminating output verified (`x.split(",")` prints `a b` vs `['a', 'b']` @ dff55b2cd); R-P control `x += s` SPY0220 today (ledger).
- `TypeCodecRegistryTotalityTests` (Phase 7): exemption WAS the subject (`SelfType` in the exclusion set with a live ICE) — corrected; mutations (c)(d) named; positive control for the remaining exclusion specified (SPY0342).
- `MutatorIterableArgumentMatrixTests` (Phase 8): mutation named; every python3 output claimed was executed (`extend`/`update` × tuple/str/dict-keys/pairs, `|=`/`+=` twins, `set |= tuple` TypeError).

### Warnings
- Phase 1 Decision 4 (`Kill` keeps `RemoveNone` on augmented stores): `Kill(facts, statement)` at `NarrowingFlowAnalysis.cs:592` kills on any `Assignment` regardless of operator — the exemption must test `assignment.Operator`, and the soundness comment must say why `??=` on a narrowed name is a no-op.
- Phase 3 Decision 7: keep `xs: list[object] = [None]` as a cell (today's SPY0227 steer recommends exactly that annotation; under the new rule it must run or refuse deliberately).
- Phase 6: the needle arm and `ProtocolValidator.ValidateMembership` are two stages; a container with no `__contains__` must still produce exactly one SPY0320, and a resolvable element type must produce exactly one SPY0222 — assert the count in the matrix.
- Phase 6 `sum`: overload identity must beat the current `list[int8] → IEnumerable<int>` admission (#1748's class) in `IsMoreSpecificOverload`; the `first(ys)` cell posted to #1748 is the evidence that the admission itself is untouched here.
- Phase 8: the `guarded-by` roster row binds only if the position table is a `switch` with a default arm (see the corrected acceptance bullet).
- Blast radius: the closure cell means `Sharpy.Stdlib` spy sources with nested-def stores are reachable; the Phase 1 regen diff is the measurement.

### Missing Steps Added
- Phase 0 Task 1: the four `/push` gate commands, verbatim.
- Phase 0 Task 3: the `type_narrowing.md:42` section anchor.
- Phase 1: closure + `try`/`with`/`else` scope cells; N/A binder cells.
- Phase 4 Task 1: the lambda-visit override does not exist today.
- Phase 6: argument-position positive controls; the `b: bool = sum(...)` untyped-vs-mistyped probe result.
- Phase 7: `Register<SelfType>`, the `-> Self` cold/warm cell, mutation (d), the 32-hit/14-file scan anchor, the no-`LiteralString`-in-stdlib note.
- Phase 9: file the `xs.index` vs `xs.count` code divergence.

### Unchecked Claims
- The 346-probe ledger was measured by the plan's author @ dff55b2cd; I re-ran 23 cells (10 discriminating + 13 siblings) and read the ledger for the rest — not every row was re-executed by me.
- "Roslyn refuses the covariant value-type conversion" for `sum(list[int8])` — confirmed by the CS1503 text; the checker's admission path (`IsArgumentAssignable` → `ClassifyStore(ArgumentPositional)`) was read, not traced under a debugger.
- The wider nested-block/closure bare-store count across stdlib spy sources is unmeasured by me (would take the Phase 1 regen + gate, ~25 min).
- Whether `CheckStoreQuietly` at `Lambdas.cs:140` is the only quiet path an element/body refusal takes before the container re-reports — the plan's Decision 7 rewrites both admit helpers, which covers it if the scan in Phase 3 Task 1 lists every `CheckStoreQuietly` caller (add that grep to the commit body).
