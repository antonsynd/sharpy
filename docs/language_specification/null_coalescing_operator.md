# Null-Coalescing Operator

The `??` operator provides a default value when the left operand is absent. It works with both `T?` (`Optional[T]`) and `T | None` (C# nullable):

```python
# With T? (Optional)
name: str? = get_name()
display = name ?? "Anonymous"

# With T | None (C# nullable)
raw: str | None = dotnet_api()
display = raw ?? "Anonymous"

# Chaining
result = first ?? second ?? default_value
```

This contrasts with the `or` operator which tests for truthiness (via `__true__()` and `__false__()` dunders if defined, or `__bool__()` otherwise), rather than absence:

```python
name = "" ?? "Anonymous"    # name = ""
name = "" or "Anonymous"    # name = "Anonymous"

name = None ?? "Anonymous"  # name = "Anonymous"
name = None or "Anonymous"  # name = "Anonymous"
```

*Implementation*
- *✅ Native - For `T | None`, maps to C# `??` operator.*
- *🔄 Lowered - For `T?` (`Optional[T]`), compiler generates `match` on `Some`/`None()`.*

## Optional (Tagged Union)

The `Optional[T]` tagged union (written as `T?`) works with null coalescing, with its empty case (`None()`) being treated similarly to bare `None`:

```python
maybe_int: int? = Some(5)
val = maybe_int ?? 0  # val = 5

maybe_int = None()
val = maybe_int ?? 0  # val = 0
```
