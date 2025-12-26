# F-Strings (Formatted String Literals)

```python
name = "Alice"
age = 30
msg = f"My name is {name} and I'm {age} years old"

# Expressions in f-strings
calculation = f"Result: {x * 2}"

# Format specifiers
pi = 3.14159
formatted = f"Pi: {pi:.2f}"  # "Pi: 3.14"

# Multi-line f-strings
report = f"""
Name: {name}
Age: {age}
Status: Active
"""
```

## Implicit String Conversion

Non-string expressions in f-strings are automatically converted to strings via `str()` (which calls `__str__` or `.ToString()`):

```python
x = 42
point = Point(10, 20)

f"Value: {x}"           # Implicitly calls str(42)
f"Location: {point}"    # Implicitly calls str(point) -> point.__str__() or point.ToString()
```

This matches both Python's f-string behavior and C#'s string interpolation.

## F-String Nesting Rules

Sharpy supports nested f-strings, matching Python 3.12+ behavior. The lexer uses a mode stack to track nested interpolation contexts.

**Nested f-strings:**
```python
# Nested f-string with different quote types
name = "Alice"
msg = f"Hello, {f'dear {name}'}!"  # "Hello, dear Alice!"

# Multiple nesting levels (use alternating quote styles)
result = f"A{f'B{f\"C\"}B'}A"  # "ABCBA"
```

**Literal braces:**

Use doubled braces to include literal `{` or `}` in f-strings:

```python
f"Set: {{{1, 2, 3}}}"     # "Set: {1, 2, 3}"
f"Empty dict: {{}}"        # "Empty dict: {}"
```

**Dictionary literals in f-strings:**

Dictionary literals must be wrapped in parentheses to avoid ambiguity with format specifiers:

```python
# ❌ Ambiguous - looks like format spec
f"result: {{'key': value}}"    # ERROR

# ✅ Use parentheses
f"result: {({'key': value})}"  # OK: prints dict

# ✅ Or use a variable
d = {'key': value}
f"result: {d}"                  # OK
```

**Format specifiers with expressions:**

Format specifiers can contain expressions, including nested f-strings:

```python
precision = 3
f"{pi:.{precision}f}"      # Dynamic precision: "3.142"

width = 10
f"{name:>{width}}"         # Right-align in 10 chars
```

## Lexer State Machine for F-Strings

The lexer maintains a stack of modes to handle f-string parsing:

1. **Normal mode**: Regular tokenization
2. **F-string mode**: Inside f-string, scanning for `{` or end quote
3. **Interpolation mode**: Inside `{...}`, regular expression parsing with brace counting

**State transitions:**

| Current State | Input | Action |
|---------------|-------|--------|
| Normal | `f"` | Push F-string mode |
| F-string | `{` (not `{{`) | Push Interpolation mode |
| F-string | `}` | Error (unbalanced) |
| F-string | `"` | Pop F-string mode |
| Interpolation | `{` | Increment brace count |
| Interpolation | `}` (count > 0) | Decrement brace count |
| Interpolation | `}` (count = 0) | Pop Interpolation mode |
| Interpolation | `f"` | Push nested F-string mode |

**Nesting depth limit:** The lexer should support at least 3 levels of f-string nesting. Deeper nesting is rarely needed and may be limited for implementation simplicity.

*Implementation*
- *✅ Native - Maps to C# interpolated strings `$"..."`.*
- *Nested f-strings require lexer mode stack.*
- *C# interpolated strings support similar nesting via `$"outer {$"inner"} outer"`.*
