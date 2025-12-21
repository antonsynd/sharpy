# Maybe expressions

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

Like `try`, the `maybe` expression has very low precedence (lower than `to`, arithmetic, comparisons, and logical operators), meaning it captures the entire following expression:

```python
# maybe captures the full expression
x = maybe get_value() + default    # Parsed as: maybe (get_value() + default)
                                   # Optional wrapping the entire sum

# maybe is lower precedence than `to`, so it wraps safe casts
y = maybe obj to Widget?           # Parsed as: maybe (obj to Widget?)
                                   # Optional[Widget]

# maybe does NOT capture conditional expressions
z = maybe foo() if cond else bar()  # Parsed as: (maybe foo()) if cond else bar()
```

Use parentheses when you need to limit what `maybe` captures:

```python
# Only wrap the dict lookup, then use Optional methods
x = (maybe d.get("key")).unwrap_or(0)
```
