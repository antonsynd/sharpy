# SRP-0005: `as` Casting Operator

| Field | Value |
|-------|-------|
| **Status** | Rejected — **superseded by [#1029](https://github.com/antonsynd/sharpy/issues/1029)** (2026-07-15) |
| **Date** | 2026-03-08 |
| **Phase** | — |
| **Author** | — |
| **Rejection reason** | Ambiguity with 4 other `as` contexts; anti-pattern "multiple ways to do same thing" |

> **Superseded (2026-07-15).** This proposal rejected a **bare** `as` cast (`x as T`). The rejection
> stands for that spelling — bare `as` remains reserved for aliasing/capture only. However, #1029
> introduces the **two-token** `as?` / `as!` failable-cast operators, which are *lexically distinct*
> from bare `as` (`As` + adjacent `Question`/`Bang`) and therefore carry none of this proposal's
> ambiguity. See [Superseding design](#superseding-design-1029) below and the
> [type casting spec](../language_specification/type_casting.md#experimental-as--as-failable-casts).

## Summary

Use the `as` keyword as a type casting operator alongside or instead of `to`.

## Proposed Syntax

```python
animal: Animal = get_animal()
dog = animal as Dog         # Type cast
dog = animal as Dog?        # Safe cast (returns None on failure)
```

## Motivation

Python uses `as` in several binding contexts (exception, import, with, match/case). Some languages (C#, Kotlin) use `as` for type casting. The proposal was to add `as` as a synonym or alternative to the `to` operator for casting.

## Rejection Rationale

### 1. Ambiguity with 4 other `as` contexts

The `as` keyword already has four distinct meanings in Sharpy:

| Context | Meaning | Example |
|---------|---------|---------|
| `except ... as name` | Bind caught exception | `except ValueError as e:` |
| `with ... as name` | Bind context manager | `with open(f) as handle:` |
| `import ... as name` | Import alias | `import numpy as np` |
| `match/case ... as name` | Pattern binding | `case Point(x, y) as p:` |

Adding a fifth meaning (type cast) creates genuine parsing ambiguity. In `with` statements, `expr as name` could be either a type cast or a context manager binding. This required a parser hack (`_inhibitPostfixAs`) to disambiguate, adding complexity and fragility.

### 2. Anti-pattern: multiple ways to do the same thing

The `to` operator already provides all casting functionality:

```python
dog = animal to Dog         # Throwing cast
dog = animal to Dog?        # Safe cast
```

Adding `as` as a second spelling violates the "consistency" principle — two syntaxes for the same operation with no semantic difference.

### 3. Parser complexity

The `_inhibitPostfixAs` mechanism required saving and restoring parser state around `with` statement parsing. This is the kind of context-sensitive hack that makes parsers fragile and error-prone. Removing `as` as a cast operator eliminates an entire class of parsing edge cases.

### 4. `to` is sufficient and unambiguous

The `to` keyword has no other meaning in the language, so it never creates parsing ambiguity. It reads naturally as a directional conversion ("convert value *to* Type").

## Alternative

Use the `to` operator, which is the sole casting operator:

```python
dog = animal to Dog         # Throws InvalidCastException if not a Dog
dog = animal to Dog?        # Returns None if not a Dog
```

## Superseding design (#1029)

[#1029](https://github.com/antonsynd/sharpy/issues/1029) revisits casting with `as?` / `as!` — two
distinct operator tokens rather than a bare-`as` cast — and this defeats each rejection point above:

- **Ambiguity (#1, #3):** `as?` / `as!` are `As` immediately followed by an adjacent `Question` / `Bang`
  token. In every alias position (`except`/`with`/`import`/`case`) `as` is followed by an **identifier**,
  never `?`/`!`, so the parser distinguishes them by token adjacency alone — no `_inhibitPostfixAs` hack,
  no parenthesization rule, no context-sensitivity. Bare `x as T` stays a parse error (regression-anchored
  by `errors/as_cast_rejected`).
- **"Multiple ways" (#2):** the design's exit plan is to make `as?`/`as!` the **only** failable-cast
  spelling and **retire `to`** at graduation, so the language converges on one syntax rather than two. A
  migration hint (`SPY0479`, active only while `failable_cast` is enabled) steers code toward the new
  spelling in the interim.
- **Explicitness gain:** moving the failure mode onto the operator (`as!` throws, `as?` yields `None`)
  makes each cast site self-describing, aligning with Axiom 3 / "explicit over magic."

The feature ships **experimental behind the `failable_cast` flag** per the
[feature lifecycle](../design/feature-lifecycle.md); corpus migration and `to` removal are graduation-time
work tracked in [#1096](https://github.com/antonsynd/sharpy/issues/1096).

## See Also

- [Type Casting spec](../language_specification/type_casting.md) — the `to` operator specification and the
  experimental `as?`/`as!` operators
- [#1029](https://github.com/antonsynd/sharpy/issues/1029) — the superseding `as?`/`as!` design
