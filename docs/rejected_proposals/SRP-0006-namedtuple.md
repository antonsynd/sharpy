# SRP-0006: `collections.namedtuple`

| Field | Value |
|-------|-------|
| **Status** | Rejected |
| **Date** | 2026-04-07 |
| **Phase** | 11 (v0.2.5) |
| **Author** | — |
| **Rejection reason** | Redundant with existing features; violates type safety axiom |
| **GitHub issue** | [#508](https://github.com/antonsynd/sharpy/issues/508) |

## Summary

Support for `collections.namedtuple` as a stdlib function that creates named tuple types at runtime, mirroring the Python standard library.

## Proposed Syntax

```python
from collections import namedtuple

Point = namedtuple("Point", ["x", "y"])
p = Point(1, 2)
print(p.x)  # 1
```

## Implementation History

This feature was implemented and removed within 1.5 hours:

- **Implemented**: commit `29aef760`
- **Removed**: commit `6dd6b30d`

The rapid reversal confirmed that the feature does not belong in Sharpy.

## Motivation

Python's `collections.namedtuple` is a factory function that creates simple classes with named fields, commonly used for lightweight data containers. It predates Python's `typing.NamedTuple` and dataclasses.

## Rejection Rationale

### 1. Redundant with named tuples and `@dataclass`

Sharpy already has two features that cover the same use cases:

**Named tuples** (type alias syntax):
```python
type Point = tuple[x: float, y: float]
p: Point = (1.0, 2.0)
print(p.x)  # 1.0
```

**Dataclasses**:
```python
@dataclass
class Point:
    x: float
    y: float
```

Adding `namedtuple` would create a third way to define simple data containers. The anti-pattern "multiple ways to do same thing — consistency issue" applies directly.

### 2. Untyped fields violate Axiom 3 (type safety)

`namedtuple` fields are inherently untyped — all fields default to `object`. This violates Axiom 3 (static types), which requires all values to have known types at compile time. Sharpy's named tuple syntax and `@dataclass` both require explicit type annotations, which is the correct approach for a statically-typed language.

### 3. String-based field names are magic

The `namedtuple("Point", ["x", "y"])` API defines field names as runtime strings. This is a design anti-pattern: "magic behavior — unpredictable; prefer explicit." Field names should be identifiers in source code, not string literals processed at runtime.

### 4. Inconsistent C# target

The implementation faced an unresolvable design choice: should `namedtuple` emit a C# record class or a `ValueTuple`? Neither option maps cleanly:

- **Record class**: Heavyweight for what should be a simple tuple; semantics diverge from Python's `namedtuple` (which is a tuple subclass)
- **ValueTuple**: Cannot support named field access in the same way; C# `ValueTuple` field names are erased at runtime

Sharpy's existing named tuple syntax (`type X = tuple[...]`) already resolves this mapping cleanly to `ValueTuple`.

## Alternative

Use the built-in alternatives that are already part of the language:

**For lightweight, immutable data with positional access:**
```python
type Point = tuple[x: float, y: float]
```

**For richer data containers with methods:**
```python
@dataclass
class Point:
    x: float
    y: float
```

Both approaches are fully typed, explicit, and have well-defined C# emission targets.

## See Also

- [GitHub issue #508](https://github.com/antonsynd/sharpy/issues/508) — Original tracking issue
