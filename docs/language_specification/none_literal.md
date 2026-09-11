# None Literal

`None` represents the absence of a value. It is valid for nullable types (`T | None`):

```python
# Nullable (C# interop)
value: str | None = None
```

For `T | None`, bare `None` emits C# `null`.

For `T?` (`Optional[T]`), use `None()` — not bare `None`:

```python
# Optional (Sharpy-native)
x: int? = None()

# x: int? = None   # SPY0604 — bare None belongs to T | None, not T?
```

See [Optional Type](tagged_unions_optional.md) for details on `T?`.

## None in Best-Common-Type (R-AB)

Bare `None` is untyped: it contributes no type to the best-common-type rule. In a slot-less context, this causes an arm-3 refusal:

```python
x = None
# error: cannot infer a type for 'x': 'None' names no type on its own, so 'x' has nothing to be.
#        Annotate the target ('x: T | None = None' for a .NET-nullable reference,
#        or 'x: T? = None()' for a Sharpy optional)

xs = [None, 1]
# error: Cannot infer a type for list element: its operands have no best common type
#        ('int32', 'None') — annotate the target (e.g. 'xs: list[T | None] = ...' for
#        .NET-nullable elements, or 'xs: list[T?] = ...' with None() elements for Sharpy optionals)

z = None if c else "hello"
# error: Cannot infer a type for conditional expression: its operands have no best common type
#        ('str', 'None') — annotate the target (e.g. 'x: T | None = ...' on the target)
```

Every steer is source you can paste. In particular the `None` steer never offers `T? = None`:
a Sharpy optional's only constructors are `Some(v)` and `None()` (R-G, #1720).

Under a slot, `None` is admitted through the store seam as usual:
```python
x: str | None = None          # OK
xs: list[int | None] = [None, 1]  # OK
a: str | None = None if c else "hello"  # OK
```

See [Collection Types — Element type of a literal](collection_types.md#element-type-of-a-literal) and [Expressions — Conditional expression](expressions.md#conditional-expression-ternary).

*Implementation*
- *✅ Native - `None` → `null` for `T | None`; `None()` → `Optional<T>.None` for `T?`.*
