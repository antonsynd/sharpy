# Nullability: `T`, `T | None` and `T?`

> **Status:** Principle — ruled by the owner 2026-11-12 (the rules below, approved as drafted; the
> two sub-rulings on truthiness and on `x == None` for a `T?` the same day). **Not yet implemented**:
> [nullability-ledger.tsv](nullability-ledger.tsv) records today's behavior against each rule.
> No tracker is filed for this principle yet; the cells already filed are on #2644 and #2635.
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
| Member access, `for`, index | runs; `NullReferenceException` on `None` | refused | refused |
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
CPython 3.15.2's answer (run, not recalled), Sharpy's answer @ 7db7aec44 (`sharpyc run`), the
target under the rules above, and a status. `[...]` is printed output, `!Name` an exception,
`refused SPY…` a compile-time refusal. 241 rows: 188 match the target and 53 are gaps.

| Gap | Rows | Where |
|---|---|---|
| refused today, should run with a warning | 18 | mostly `int \| None`: arithmetic, ordering, `in`, stores |
| runs, but without the warning | 12 | reference `T \| None`: member access, ordering, `len`, stores |
| wrong value | 9 | truthiness of an empty payload (`if x:`, `not x`); `bool()` on an empty `T?` |
| `None` enters a `T` slot unraised, or an ordering prints a value | 5 | pass and store into `T`; `int \| None < 9` |
| wrong exception (`NullReferenceException`) | 5 | member access, `for`, index, user-class ordering |
| compiler crash (SPY0908) | 4 | `str \| None` ordering; `K? == K` with an `object`-typed `__eq__` |

The ledger is a measurement, not a guard: no test reads it yet. The rows for `T | None` over an
`int` member (`bit_length`) are omitted because `int` has no such member in Sharpy.

## What this changes

- `nullable_types.md`: "Do NOT use `T | None` for Sharpy-native optionals" goes; the page states
  the loose rules.
- `logical_operators.md`: the truthiness of a `T | None` is no longer its null check.
- #2644 and #2635 have been ruled one cell at a time ("answers as CPython or is refused"); for a
  `T | None` operand the answer is now "answers as CPython, with a warning where `None` fails".
- plan-4e2867's widened value-ness (D7) turns three running programs into refusals; under these
  rules they would run with a warning. That ruling is still open.
