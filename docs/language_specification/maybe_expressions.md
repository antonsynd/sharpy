## Maybe expressions **[v0.2.0]**

Optionals can be implicitly created via `maybe` expressions.
A `maybe` expression wraps the value of the expression in
`Optional[T]` where `T` is the type of the expression.
If the expression is `None`, then the result
holds its `Nothing` case.

```python
d: dict[str, int] = {"y": 5}
x = maybe d.get("x")  # x is of type Optional[int]
```

It is a type-checking error if the expression does not return
a nullable type (`T?`).

```python
# ✅ Valid - dict.get() returns T?
d: dict[str, int] = {}
x = maybe d.get("key")       # OK: get() returns int?

# ✅ Valid - explicitly nullable
value: int? = get_optional_value()
y = maybe value              # OK: value is int?

# ❌ Invalid - expression is not nullable
s: str = "hello"
z = maybe s.upper()          # ERROR: upper() returns str, not str?

n: int = 42
w = maybe n                  # ERROR: n is int, not int?
```

**Precedence Rules:**

Like `try`, the `maybe` expression has low precedence:

```python
x = maybe d.get("key") ?? 0    # Parsed as: (maybe d.get("key")) ?? 0
                               # ERROR: Optional[int] ?? int doesn't work directly

# Use the Optional's methods instead
x = (maybe d.get("key")).unwrap_or(0)
```

---

