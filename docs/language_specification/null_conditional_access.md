# Null-Conditional Access

Sharpy borrows null-conditional access `?.` from C#, which short-circuits
field/property/method access if the object that provides the
field/property/method is `None`, which causes the entire expression to return
`None` instead of continuing with the evaluation.

```python
# Short-circuits if None
result = obj?.method()       # Returns None if obj is None
value = obj?.field           # Returns None if obj is None
nested = obj?.field?.nested  # Chains null checks
```

*Implementation*
- *✅ Native - Maps to C# `?.` operator (C# 6.0+).*

## Optional (Tagged Union)

The `Optional[T]` tagged union works with null-conditional access, with its `Nothing` case being treated similarly to `None`:

```python
maybe_str: Optional[str] = Optional.Some("HELLO")
val = maybe_str?.lower()  # val = Optional[str].Some("hello")

maybe_str = Optional.Nothing
maybe_val = maybe_str?.len()  # val = Optional[str].Nothing
```

In this situation, the return type is `Optional[T]` where `T` is the expected type of the entire expression if it had evaluated.
