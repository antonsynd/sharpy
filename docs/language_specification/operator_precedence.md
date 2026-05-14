# Operator Precedence

Operators listed from highest to lowest precedence:

| Precedence | Operators | Description | Associativity |
|------------|-----------|-------------|---------------|
| 1 | `()`, `[]`, `.`, `?.` | Grouping, indexing, member access | Left-to-right |
| 2 | `**` | Exponentiation | **Right-to-left** |
| 3 | `await x` | Await expression | Right-to-left (prefix) |
| 4 | `+x`, `-x`, `~x` | Unary operators | Right-to-left (unary) |
| 5 | `*`, `/`, `//`, `%` | Multiplicative | Left-to-right |
| 6 | `+`, `-` | Additive | Left-to-right |
| 7 | `<<`, `>>` | Bitwise shifts | Left-to-right |
| 8 | `&` | Bitwise AND | Left-to-right |
| 9 | `^` | Bitwise XOR | Left-to-right |
| 10 | `\|` | Bitwise OR | Left-to-right |
| 11 | `\|>` | Pipe operator | Left-to-right |
| 12 | `to` | Type cast | Left-to-right |
| 13 | `in`, `not in`, `is`, `is not`, `<`, `<=`, `>`, `>=`, `!=`, `==` | Comparisons | **Chained** (see below) |
| 14 | `not` | Logical NOT | Right-to-left (unary) |
| 15 | `and` | Logical AND | Left-to-right |
| 16 | `or` | Logical OR | Left-to-right |
| 17 | `??` | Null coalescing | Left-to-right |
| 18 | `try`, `maybe` | Result/Optional wrapping expressions | Right-to-left (prefix) |
| 19 | `x if c else y` | Conditional expression | Right-to-left |
| 20 | `lambda`, `(params) ->` | Lambda / Arrow lambda expression | Right-to-left |
| 21 | `:=` | Walrus (assignment expression) | Right-to-left |

## Associativity Details

**Right-associative operators:**
```python
# Exponentiation chains right-to-left
2 ** 3 ** 2    # = 2 ** (3 ** 2) = 2 ** 9 = 512

# Conditional chains right-to-left
a if x else b if y else c    # = a if x else (b if y else c)
```

**Comparison chaining:**

Comparison operators are neither left nor right associative. Instead, they form **chains** that are evaluated as conjunctions:

```python
# Chained comparisons
a < b < c      # Equivalent to: (a < b) and (b < c)
a == b == c    # Equivalent to: (a == b) and (b == c)
a < b <= c     # Equivalent to: (a < b) and (b <= c)

# Each operand is evaluated at most once
# If 'b' is a function call, it's called only once:
a < expensive() < c    # temp = expensive(); (a < temp) and (temp < c)
```

**Note:** `is` and `in` operators participate in chaining:
```python
a is b is c           # (a is b) and (b is c)
a in b in c           # (a in b) and (b in c)
a < b in c            # (a < b) and (b in c)
```

## Pipe Operator Precedence

The pipe operator `|>` has lower precedence than arithmetic but higher than type coercion operators, enabling natural data flow:

```python
# Pipe captures the left-hand expression fully
data |> filter(predicate) |> map(transform)  # Chains left-to-right

# Arithmetic happens before piping
x + 1 |> float    # Equivalent to: (x + 1) |> float

# Use parentheses for complex right-hand expressions
items |> (lambda x: x.value)  # Parentheses needed for lambda
```

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
                               # Widget?
```

Use parentheses when you need different grouping:

```python
# Only wrap the function call, then add
x = (try some_func(4)) + 5     # Must unwrap the Result before adding
```

## Type Annotation Precedence

In type annotation contexts, the `!E` (Result shorthand) and `| None` (C# nullable) modifiers have their own precedence:

- `!E` binds **tighter** than `| None`

This is type-level precedence, not expression-level:

```python
# !E binds tighter than | None
int !ValueError | None  →  (int !ValueError) | None  →  Result[int, ValueError] | None

# Use in function signatures
def try_parse(s: str) -> int !ValueError | None:
    # Returns Result[int, ValueError] | None
    ...
```
