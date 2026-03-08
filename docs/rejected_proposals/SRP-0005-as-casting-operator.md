# SRP-0005: `as` Casting Operator

| Field | Value |
|-------|-------|
| **Status** | Rejected |
| **Date** | 2026-03-08 |
| **Phase** | — |
| **Author** | — |
| **Rejection reason** | Ambiguity with 4 other `as` contexts; anti-pattern "multiple ways to do same thing" |

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

## See Also

- [Type Casting spec](../language_specification/type_casting.md) — the `to` operator specification
