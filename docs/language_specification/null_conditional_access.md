# Null-Conditional Access

Sharpy borrows null-conditional access `?.` from C#, which short-circuits
field/property/method access if the object is absent, causing the entire expression to return
an absent value instead of continuing with the evaluation.

It works with both `T?` (`Optional[T]`) and `T | None` (C# nullable):

```python
# With T | None (C# nullable)
result = obj?.method()       # Returns None if obj is None
value = obj?.field           # Returns None if obj is None
nested = obj?.field?.nested  # Chains null checks

# With T? (Optional)
name: str? = get_name()
upper = name?.upper()        # Returns str? (None() if name is None())
```

## Return Types

The return type depends on the operand type:

- `x?.foo` where `x: T | None` → returns `U | None` (C# nullable)
- `x?.foo` where `x: T?` → returns `U?` (Optional)

```python
# C# nullable propagates C# nullable
raw: str | None = dotnet_api()
length = raw?.len()              # length is int | None

# Optional propagates Optional
safe: str? = get_name()
length = safe?.len()             # length is int?
```

*Implementation*
- *✅ Native - For `T | None`, maps to C# `?.` operator (C# 6.0+).*
- *🔄 Lowered - For `T?` (`Optional[T]`), compiler generates `match` on `Some`/`None()`.*

## Optional (Tagged Union)

The `Optional[T]` tagged union (written as `T?`) works with null-conditional access, with its empty case (`None()`) being treated similarly to bare `None`:

```python
maybe_str: str? = Some("HELLO")
val = maybe_str?.lower()  # val is str? = Some("hello")

maybe_str = None()
maybe_val = maybe_str?.len()  # maybe_val is int? = None()
```

In this situation, the return type is `U?` where `U` is the expected type of the entire expression if it had evaluated.

## Nullable Flattening

When `?.` accesses a member whose type is already nullable or optional, the compiler
**flattens** the result to avoid double-wrapping. Without flattening, `x?.get_label()`
where `get_label()` returns `str?` would produce `str??` (an optional of an optional),
which is not a valid type.

The rule: if the accessed member already returns `T?` (Optional) or `T | None` (C#
nullable), the `?.` operator does **not** add another layer of wrapping. The result type
is the member's own type, unchanged.

```python
class Inner:
    label: str?
    def __init__(self, label: str?):
        self.label = label
    def get_label(self) -> str?:
        return self.label

class Outer:
    inner: Inner?
    def __init__(self, inner: Inner?):
        self.inner = inner
    def get_inner(self) -> Inner?:
        return self.inner

o: Outer? = Some(Outer(Some(Inner(Some("hello")))))

# Method returning str? via ?. — result is str?, NOT str??
r1: str? = o?.get_inner()?.get_label()

# Field of type str? via ?. — result is str?, NOT str??
r2: str? = o?.get_inner()?.label

# Non-optional member (int) via ?. — wrapped once to int?
r3: int? = o?.get_inner()?.value   # value: int → int?
```

This applies uniformly to fields, properties, and method return types, for both
`T?` (Optional) and `T | None` (C# nullable) operand types.
