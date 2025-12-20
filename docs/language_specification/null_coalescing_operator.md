# Null-Coalescing Operator **[v0.1.1]**

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

*Implementation: ✅ Native - Maps to C# `??` operator.*
