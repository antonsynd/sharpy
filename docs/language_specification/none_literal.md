# None Literal

`None` represents the absence of a value. It is valid for both nullable types (`T | None`) and optional types (`T?`):

```python
# Nullable (C# interop)
value: str | None = None

# Optional (Sharpy-native)
x: int? = None
```

For `T | None`, bare `None` emits C# `null`. For `T?` (`Optional[T]`), bare `None` emits C# `default`, producing an empty optional. `None()` remains valid as an explicit alternative for `T?`.

See [Optional Type](tagged_unions_optional.md) for details on `T?`.

*Implementation*
- *✅ Native - `None` → `null` for `T | None`, `default` for `T?`.*
