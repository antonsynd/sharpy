# Operator Precedence

Operators listed from highest to lowest precedence:

| Precedence | Operators | Description |
|------------|-----------|-------------|
| 1 | `()`, `[]`, `.`, `?.` | Grouping, indexing, member access |
| 2 | `**` | Exponentiation (right-associative) |
| 3 | `+x`, `-x`, `~x` | Unary operators |
| 4 | `*`, `/`, `//`, `%` | Multiplicative |
| 5 | `+`, `-` | Additive |
| 6 | `<<`, `>>` | Bitwise shifts |
| 7 | `&` | Bitwise AND |
| 8 | `^` | Bitwise XOR |
| 9 | `\|` | Bitwise OR |
| 10 | `to` | Type coercion |
| 11 | `in`, `not in`, `is`, `is not`, `<`, `<=`, `>`, `>=`, `!=`, `==` | Comparisons |
| 12 | `not` | Logical NOT |
| 13 | `and` | Logical AND |
| 14 | `or` | Logical OR |
| 15 | `??` | Null coalescing |
| 16 | `try`, `maybe` | Result/Optional wrapping expressions |
| 17 | `x if c else y` | Conditional expression |
| 18 | `lambda` | Lambda expression |

## Try and Maybe Expressions

The `try` and `maybe` expressions have very low precedence, meaning they capture the entire following expression up to operators with even lower precedence (conditional and lambda).

```python
# try captures the entire arithmetic expression
x = try some_func(4) + 5       # Parsed as: try (some_func(4) + 5)
                               # Both the call and addition are wrapped

# try captures through comparisons
y = try parse_int(s) > 0       # Parsed as: try (parse_int(s) > 0)
                               # Result[bool, Exception]

# try captures through logical operators
z = try validate(a) and check(b)  # Parsed as: try (validate(a) and check(b))

# try does NOT capture conditional expressions
w = try foo() if cond else bar()  # Parsed as: (try foo()) if cond else bar()
                                  # Use parentheses: try (foo() if cond else bar())

# maybe works the same way
opt = maybe get_value() + default  # Parsed as: maybe (get_value() + default)
```

Since `try` and `maybe` have lower precedence than `to`, they naturally wrap type casts:

```python
# try captures the entire cast expression
result = try animal to Dog     # Parsed as: try (animal to Dog)
                               # Result[Dog, InvalidCastException]

# maybe with safe cast
opt = maybe obj to Widget?     # Parsed as: maybe (obj to Widget?)
                               # Optional[Widget]
```

Use parentheses when you need different grouping:

```python
# Only wrap the function call, then add
x = (try some_func(4)) + 5     # Must unwrap the Result before adding
```
