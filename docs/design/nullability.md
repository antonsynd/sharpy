# Nullability: `T`, `T | None` and `T?`

> **Status:** Principle — ruled by the owner 2026-11-12 (the rules below, approved as drafted; the
> two sub-rulings on truthiness and on `x == None` for a `T?` the same day). **Not yet implemented**:
> [nullability-ledger.tsv](nullability-ledger.tsv) records today's behavior against each rule and is
> checked on every test run.
> Tracker: #2674. Cells already filed: #2644, #2635, #2675 (`?.` on a `T?`), #2676 (empty literals).
> **Companions:** `docs/language_specification/nullable_types.md`, `tagged_unions_optional.md` and
> `logical_operators.md` (rewritten around this file as each rule lands), #2230 (R-EH, platform
> types), [python-fidelity-scope.md](python-fidelity-scope.md),
> [verification-contract.md](verification-contract.md).

## Why this file exists

The specification describes one divide: `T | None` is the .NET-nullable type for interop, `T?`
(`Optional[T]`) is the safe type for Sharpy code. A matrix of 236 programs (Sharpy @ 7db7aec44
against CPython 3.15.2) found that the compiler implements a different divide, between reference
and value types:

| Use | `str \| None`, `K \| None` | `int \| None` | `T?` |
|---|---|---|---|
| Pass or store into a plain `T` | runs silently; `None` lands in the `T` slot | refused | refused |
| Member access, `for`, index | runs; `NullReferenceException` on `None` | member access runs; `InvalidOperationException` on `None` | refused |
| Arithmetic | refused | refused | refused |
| `x < value` | `str`: SPY0908; a user class: `NullReferenceException` | prints `False` on `None` | refused |
| `x in [...]`, `x == None` | runs | refused | refused |

`T | None` also carries more than interop. The Sharpy-source standard library spells every
Python API that returns or accepts `None` with it (`re.match -> MatchResult | None`, 35 parameters
of the form `x: T | None = None`) and does not use `T?`. Unannotated .NET references are a third
case, typed plain `T` today and ruled separately (R-EH, #2230).

## The rules

**`T`** never holds `None`.

- Storage: `None` is refused. A `T | None` value may be stored or passed in, with a warning, and
  raises `TypeError` at that point if it is `None`.
- Receiver and operators: always allowed, no checks.

**`T | None`** is loose: it behaves as `T`, with a warning wherever `None` would fail, and it fails
the way CPython does.

- Storage: holds bare `None` or a bare `T`.
- Receiver (member access, call, index, iteration, `len`): allowed with a warning; on `None` it
  raises CPython's exception (`AttributeError` or `TypeError`), never a `NullReferenceException`.
- Arithmetic, ordering and unary operators: allowed with a warning; `TypeError` on `None`. C#'s
  lifted answers (`null + 1` is `null`, `null < 9` is `false`) are never produced.
- `==`, `!=`, `in`, `is`, `print`, `str()`, f-strings, dict key: always allowed, no warning,
  CPython's answer.
- Truthiness (`if x:`, `bool(x)`, `not x`, `and`/`or`): CPython's answer. `None` is false, and a
  value has its own truthiness, so `[]`, `0` and `""` are false. A value whose type has no falsy
  case (a plain user class, a fixed-arity tuple) is true whenever it is not `None`.
- Narrowing (`is not None`), `??` and `?.` remove the warning.
- The rules are the same for value types (`int | None`) and reference types (`str | None`).

**`T?`** is strict: nothing is allowed until the empty case is handled.

- Storage: built only with `Some(...)` and `None()`. It never converts to or from `T` or
  `T | None` implicitly; `maybe` crosses.
- Receiver, arithmetic, ordering, `==` against a value, `in`, passing as `T`: refused at compile
  time.
- `x == None` and `x != None`: refused, with a steer to `is None` / `is not None`.
- `is None`, truthiness, `bool()`, `print`, `str()`, `??`, `?.`, dict key: allowed. Truthiness is
  "is it `Some`" (`Some(0)` is true), and `bool(x)` agrees with `if x:`.
- Narrowing or matching gives the payload as a plain `T`.

## The ledger

[nullability-ledger.tsv](nullability-ledger.tsv) has one row per cell: slot type, use, value,
CPython 3.15.2's answer, Sharpy's answer, the target under the rules above, and a status. `[...]`
is printed output, `!Name` an exception, `refused SPY…` a compile-time refusal. Python has no
`T?`, so on a `T?` row the `cpython` column is what the same use does on the bare payload (or on
`None`): it is context, and the target comes from the strict rules, not from it. A Sharpy answer
that came with warnings ends `+ warning <codes>`; a warning on a row whose target has none is the
status `gap: spurious warning`.

The ledger is generated and checked by `NullabilityLedgerTests`
(`src/Sharpy.Compiler.Tests/Properties/Differential/`). On every run the test builds each
slot × use × value program, runs it under CPython and under Sharpy, derives the target from the
rules and the status from the two, and requires the result to equal the committed file byte for
byte. A gap that closes and a matching cell that regresses are both red until the row is updated,
so the file cannot drift, and its diff is the record of what moved. The regenerated table is
written to `.claude/tmp/nullability-ledger.proposed.tsv`.

Slots are `int`, `str`, a user class `K` and `list[int]`, each as `T | None` and as `T?`. Values
are a value, `None`, and for the truthiness uses the falsy payload (`0`, `""`, `[]`). The payload
reaches the slot through a local typed `T`, so the cell measures the use and not whether a literal
can be typed from the slot (#2676).

Measured @ a121b0c81: 342 cells, 270 match the target and 72 are gaps.

| Gap | Rows | Where |
|---|---|---|
| refused today, should run | 20 | `T \| None` arithmetic and unary minus; `int \| None` in `in`, `== None` and stores |
| runs, but without the warning | 20 | member access, ordering, `len`, `for`, index and stores on a `T \| None` |
| wrong exception | 9 | `NullReferenceException` (`InvalidOperationException` for `int \| None`) where CPython raises `AttributeError` or `TypeError` |
| wrong value | 10 | `if x:` and `not x` on a falsy payload, an empty `list[int] \| None` included; `bool()` on an empty `T?` |
| no raise | 7 | `None` passed or stored into a plain `T`; `int \| None < 9` prints `False` |
| compiler crash (SPY0908) | 6 | `str \| None` ordering; `K? == K` with an `object`-typed `__eq__`; `?.` on an `int?` |

## What this changes

- `nullable_types.md`: "Do NOT use `T | None` for Sharpy-native optionals" goes; the page states
  the loose rules.
- `logical_operators.md`: the truthiness of a `T | None` is no longer its null check.
- #2644 and #2635 have been ruled one cell at a time ("answers as CPython or is refused"); for a
  `T | None` operand the answer is now "answers as CPython, with a warning where `None` fails".
- plan-4e2867's widened value-ness (D7) turns three running programs into refusals; under these
  rules they would run with a warning. That ruling is still open.
