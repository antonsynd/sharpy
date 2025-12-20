# Membership Operators **[v0.1.0]**

| Operator | Description |
|----------|-------------|
| `in` | Membership test |
| `not in` | Negated membership |

```python
if item in collection:
    print("Found")
```

## Dispatch Priority

The `in` operator dispatches as follows:
1. For Sharpy types: calls `__contains__` if defined
2. For .NET types: calls `.Contains()` method
3. For strings: calls `.Contains()` for substring test

```python
# Works on Sharpy collections
items = [1, 2, 3]
if 2 in items:           # Calls __contains__
    print("Found")

# Works on .NET collections
from system.collections.generic import HashSet
s = HashSet[int]()
s.add(42)
if 42 in s:              # Calls .Contains()
    print("Found")

# Works on strings (substring test)
if "ell" in "Hello":     # Calls str.Contains()
    print("Found substring")
```

*Implementation: 🔄 Lowered - Maps to `__contains__` for Sharpy types, `.Contains()` for .NET types.*
