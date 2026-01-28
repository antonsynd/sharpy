# None Literal

`None` represents the absence of a value and corresponds to `null` in C#. It is used with `T | None` types for .NET interop:

```python
value: str | None = None
```

> **Note:** For Sharpy-native optionals, use `Nothing` with `T?` (which is `Optional[T]`). See [Optional Type](tagged_unions_optional.md).

*Implementation*
- *✅ Native - `None` → `null`.*
