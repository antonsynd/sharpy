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

*Implementation*
- *✅ Native - `None` → `null` for `T | None`; `None()` → `Optional<T>.None` for `T?`.*
