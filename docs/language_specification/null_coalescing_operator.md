# Null-Coalescing Operator

```python
# Provide default for None values
name = user_name ?? "Anonymous"
count = get_count() ?? 0

# Chaining
result = first ?? second ?? default_value
```

This contrasts with the `or` operator which tests for truthiness (via `__true__()` and `__false__()` dunders if defined, or `__bool__()` otherwise), rather than `None`.

```python
name = "" ?? "Anonymous"    # name = ""
name = "" or "Anonymous"    # name = "Anonymous"

name = None ?? "Anonymous"  # name = "Anonymous"
name = None or "Anonymous"  # name = "Anonymous"
```

*Implementation*
- *✅ Native - Maps to C# `??` operator.*

## Optional (Tagged Union)

The `Optional[T]` tagged union works with null coalescing, with its `Nothing` case being treated similarly to `None`:

```python
maybe_int: Optional[int] = Optional.Some(5)
val = maybe_int ?? 0  # val = 5

maybe_int = Optional.Nothing
val = maybe_int ?? 0  # val = 0
```
