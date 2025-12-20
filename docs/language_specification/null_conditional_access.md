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

**Implementation**
✅ Native - Maps to C# `?.` operator (C# 6.0+).
