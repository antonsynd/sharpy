# Operator Precedence **[v0.1.0]**

Operators listed from highest to lowest precedence:

| Precedence | Operators | Description |
|------------|-----------|-------------|
| 1 | `()`, `[]`, `.`, `?.` | Grouping, indexing, member access |
| 2 | `to` | Type coercion |
| 3 | `**` | Exponentiation (right-associative) |
| 4 | `+x`, `-x`, `~x` | Unary operators |
| 5 | `*`, `/`, `//`, `%` | Multiplicative |
| 6 | `+`, `-` | Additive |
| 7 | `<<`, `>>` | Bitwise shifts |
| 8 | `&` | Bitwise AND |
| 9 | `^` | Bitwise XOR |
| 10 | `\|` | Bitwise OR |
| 11 | `in`, `not in`, `is`, `is not`, `<`, `<=`, `>`, `>=`, `!=`, `==` | Comparisons |
| 12 | `not` | Logical NOT |
| 13 | `and` | Logical AND |
| 14 | `or` | Logical OR |
| 15 | `??` | Null coalescing |
| 16 | `x if c else y` | Conditional expression |
| 17 | `lambda` | Lambda expression |
