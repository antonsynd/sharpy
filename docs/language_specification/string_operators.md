# String Operators

Strings support concatenation and repetition operators:

| Operator | Description | Example | Result |
|----------|-------------|---------|--------|
| `+` | Concatenation | `"Hello" + " " + "World"` | `"Hello World"` |
| `*` | Repetition | `"ab" * 3` | `"ababab"` |
| `*` | Repetition (reversed) | `3 * "ab"` | `"ababab"` |
| `in` | Substring test | `"ell" in "Hello"` | `True` |
| `==` | Equality | `"abc" == "abc"` | `True` |
| `!=` | Inequality | `"abc" != "xyz"` | `True` |
| `<` | Less than | `"abc" < "abd"` | `True` |
| `>` | Greater than | `"abd" > "abc"` | `True` |
| `<=` | Less than or equal | `"abc" <= "abc"` | `True` |
| `>=` | Greater than or equal | `"abc" >= "abc"` | `True` |

```python
# String concatenation
greeting = "Hello" + ", " + "World!"
print(greeting)  # "Hello, World!"

# String with other types requires explicit conversion
value = 42
message = "Value: " + str(value)  # Must convert int to str

# String repetition (both directions work, matching Python)
separator = "-" * 40
print(separator)  # "----------------------------------------"

also_separator = 40 * "-"  # Also valid
print(also_separator)  # "----------------------------------------"

# Substring membership
if "error" in log_message:
    handle_error()
```

## Type Safety

Unlike Python, Sharpy does not allow implicit string concatenation with non-string types:

```python
# ✅ Valid
"Count: " + str(42)
f"Count: {42}"           # F-strings handle conversion

# ❌ Invalid - type error
"Count: " + 42           # ERROR: cannot concatenate str and int
```

*Implementation*
- *✅ Operators defined on `Sharpy.Str` — `+` maps to `Str.operator+`, `*` to `Str.operator*`, comparisons use ordinal comparison via `IComparable<Str>`, `in` maps to `Contains()`.*
