# Identity Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `is` | Identity comparison | `object.ReferenceEquals()` |
| `is not` | Negated identity | `!object.ReferenceEquals()` |
| `is None` | None check | `== null` |
| `is not None` | Non-None check | `!= null` |

## Value-Type Boxing Warning

Using `is` or `is not` with value types (e.g., `int`, `bool`, `float`, structs) emits a
compile-time warning (**SPY0465**) because identity comparison on value types is meaningless
in .NET: each operand is boxed into a separate heap object, so the result is always `False`.
Use `==` or `!=` for value equality instead.

```python
x: int = 1
y: int = 1
x is y    # SPY0465 warning — always False due to boxing
x == y    # correct — value equality
```

*Implementation*
- *✅ Native for None checks; 🔄 Lowered for general identity.*
- *⚠️ SPY0465 warning when both operands are value types.*
