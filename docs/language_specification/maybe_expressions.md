# Maybe Expressions

The `maybe` expression is the **bridge from .NET interop to safe Sharpy code**. It converts a `T | None` (C# nullable) value into a `T?` (`Optional[T]`) value.

If the expression is `None`, the result is `Nothing`. Otherwise, the result is `Some(value)`.

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
