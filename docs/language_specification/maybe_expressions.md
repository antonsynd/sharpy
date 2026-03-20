# Maybe Expressions

The `maybe` expression is the **bridge from .NET interop to safe Sharpy code**. It converts a `T | None` (C# nullable) value into a `T?` (`Optional[T]`) value.

If the expression is `None`, the result is `None()` (empty Optional). Otherwise, the result is `Some(value)`.

```python
raw: str | None = dotnet_api()  # C# nullable
safe: str? = maybe raw           # Convert to Optional[str]
```

## Type Constraint

It is a type-checking error if the expression does not return a `T | None` type. The operand must be a C# nullable — not an `Optional[T]`:

```python
# ✅ Valid - converts C# nullable to Optional
raw: str | None = dotnet_api()
safe: str? = maybe raw           # OK: raw is str | None

# ✅ Valid - dict.get() returns V | None at interop level
d: dict[str, int] = {}
x = maybe d.get("key")          # OK: get() returns int | None

# ❌ Invalid - already an Optional
opt: str? = Some("hello")
x = maybe opt                    # ERROR: opt is str?, not str | None

# ❌ Invalid - expression is not nullable
s: str = "hello"
z = maybe s.upper()              # ERROR: upper() returns str, not str | None

n: int = 42
w = maybe n                      # ERROR: n is int, not int | None
```

## Idempotency

The `maybe` expression is **idempotent**: applying it twice to an `Optional[T]` is a type error on the second application (same as the single-wrapping case above).

```python
opt: str? = Some("hello")
x = maybe opt                    # ERROR: opt is str?, not str | None
```

This is correct behavior — `maybe` requires a C# nullable, and `Optional[T]` is not nullable. If you have an `Optional[T]` already, `maybe` should not be applied to it.

## Precedence Rules

Like `try`, the `maybe` expression has very low precedence (lower than `to`, arithmetic, comparisons, and logical operators), meaning it captures the entire following expression:

```python
# maybe captures the full expression
x = maybe get_value() + default    # Parsed as: maybe (get_value() + default)
                                   # Optional wrapping the entire sum

# maybe is lower precedence than `to`, so it wraps safe casts
y = maybe obj to Widget?           # Parsed as: maybe (obj to Widget?)
                                   # Widget?

# maybe does NOT capture conditional expressions
z = maybe foo() if cond else bar()  # Parsed as: (maybe foo()) if cond else bar()
```

Use parentheses when you need to limit what `maybe` captures:

```python
# Only wrap the dict lookup, then use Optional methods
x = (maybe d.get("key")).unwrap_or(0)
```

## Implementation Notes

The `maybe` expression is a **semantic pass-through** at the C# code generation level. Both `NullableType` (`T | None`) and `OptionalType` (`T?`) map to C# `T?`, so the generated code is identical — the value passes through unchanged.

The type checker enforces the semantic distinction:
1. Validates that the operand is a `NullableType` (not `OptionalType` or a non-nullable type)
2. Returns an `OptionalType` wrapping the underlying type
3. This prevents unsafe nullable values from being used where safe Optionals are expected

```python
# Sharpy source
def convert(raw: str | None) -> str?:
    return maybe raw
```

```csharp
// Generated C# — maybe is a pass-through
public static string? Convert(string? raw)
{
    return raw;
}
```

*Implementation: Semantic pass-through — type checker converts NullableType to OptionalType, code generator emits the operand unchanged.*
