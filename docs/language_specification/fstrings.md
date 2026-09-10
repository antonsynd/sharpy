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

## Interpolation Hole Type

Each interpolation hole in an f-string is typed as an `object` slot. This means any expression is accepted in a hole, including conditional expressions with unrelated branch types:

```python
c = True
print(f"{Dog() if c else Cat()}")  # OK — both branches admitted to object
```

## Hole Delimiter Rules

Inside an f-string interpolation hole (`{…}`), a `:` at brace depth 1 starts a **format
specifier**. However, when the `:` is inside parentheses or brackets (paren depth > 0), it is
part of the expression — not a format spec delimiter. This allows parenthesized expressions that
contain `:` to appear in holes:

```python
def f() -> str:
    return "hello"

def main() -> None:
    # Walrus — parenthesized to avoid format-spec parse
    print(f"{(s := f())} {s}")

    # Lambda — parenthesized call
    print(f"{(lambda: 42)()}")

    # Slice — parenthesized indexing
    xs: list[int] = [1, 2, 3, 4]
    print(f"{xs[0:2]}")
```

```
hello hello
42
[1, 2]
```

Without parentheses, `{s := f()}` would parse `:` as the format spec start. The same
applies to `{lambda: 42}` (parsed as `lambda` with format spec `42}`) and bare slices.

A bare `{x:=10}` (no parentheses) is intentionally a **format specifier** — it formats `x`
with the spec `=10`, matching Python's behavior. Only the parenthesized form is a walrus.

*Implementation*
- *✅ Native - Maps to C# interpolated strings `$"..."`.*
- *Nested f-strings require lexer mode stack.*
- *C# interpolated strings support similar nesting via `$"outer {$"inner"} outer"`.*
- *Each hole pushes `StorePosition.FStringHole` with slot `object`.*
