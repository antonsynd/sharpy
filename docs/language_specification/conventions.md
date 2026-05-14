# Conventions Used in Example Code

This document outlines stylistic conventions used in the language
specification. They are not requirements on parsing/syntax though they may
implicitly reference parsing/syntax constraints.

## Transpilation Implementation Legend

Throughout this document, implementation notes use these indicators:

| Status | Meaning |
|--------|---------|
| ✅ **Native** | Maps directly to C#. Minimum output target is C# 9.0 (for `netstandard2.1` compatibility); the compiler can emit newer C# features when targeting `net10.0` (C# 14). |
| 🔄 **Lowered** | Requires compiler transformation |
| ❌ **Future+** | Requires C# 11+ / .NET 7+; deferred |

---

## Function Return Type Annotations

Functions that have no return value, e.g. return type annotation `-> None` have
the return type annotation omitted for brevity. This also applies to dunder
methods, including `__init__` (the constructor function).

Example:

```python
def noop():  # Implicitly `-> None`
    pass

class Foobar:
    def __init__(self):  # Implicitly `-> None`
        pass
```

The exceptions to this convention are:

1) When the prose needs to explain that functions that have no return value
can optionally have a return type annotation of `-> None`, but do not need it
as functions with no return type annotation are implicitly `-> None` (C# `void`
return type).
2) Function type syntax, e.g. `(int, str) -> None`, must always specify the
return type, even if it returns nothing, due to parsing/syntactic constraints.

As a note, `lambda` keyword lambdas never indicate a return type (nor argument
types), e.g.: `lambda x, y: x + y`. Arrow lambdas, however, always have typed
parameters and may optionally include a return type annotation, e.g.:
`(x: int, y: int) -> x + y` or `(x: int) -> int: x + 1`.
