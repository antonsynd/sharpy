# String Literals

```python
# Single-quoted strings
name = 'Alice'
greeting = 'Hello, World!'

# Double-quoted strings
message = "Hello, World!"
quote = "She said, 'Hello'"

# Multi-line strings (triple-quoted)
multi = """
This is a
multi-line string
"""
```

## Escape Sequences

| Escape | Meaning |
|--------|---------|
| `\\` | Backslash |
| `\'` | Single quote |
| `\"` | Double quote |
| `\n` | Newline |
| `\r` | Carriage return |
| `\t` | Tab |
| `\b` | Backspace |
| `\f` | Form feed |
| `\0` | Null character |
| `\ooo` | Character with octal value OOO (0–377) |
| `\xHH` | Character with hex value HH |
| `\uHHHH` | Unicode 16-bit |
| `\UHHHHHHHH` | Unicode 32-bit |

*Implementation*
- *✅ Single quotes become double quotes; escape sequences map directly to C# string literals.*

## Raw Strings

```python
# Raw strings (backslashes not escaped)
path = r"C:\Users\Alice\Documents"
regex = r"\d+\.\d+"
```

*Implementation*
- *✅ Native - Maps to C# verbatim strings `@"..."`.*

## String Type

All string literals (regular, raw, multi-line) produce `System.String` values (Sharpy's `str` type):

```python
s: str = "hello"           # System.String
r: str = r"C:\path"        # System.String (verbatim)
m: str = """multi"""       # System.String
```

> **Historical note:** Sharpy previously supported native string literals (`n"..."`) to produce `System.String` instead of `Sharpy.Str`. Since `str` now maps directly to `System.String`, native string literals are no longer needed and have been removed. See [SRP-0007](../rejected_proposals/SRP-0007-str-wrapper-type.md).

## No Implicit Concatenation

Python joins adjacent string literals at parse time — `"hello " "world"` is the single string
`hello world`. **Sharpy refuses the form** (SPY0103, *"Expected end of statement, got String"*), in
every position: statement, expression, and the docstring slot.

```python
s: str = "hello " "world"       # ERROR SPY0103
s: str = "hello " + "world"     # explicit join
name: str = "world"
s: str = f"hello {name}"        # interpolation
```

This is a deliberate refusal, not an unimplemented feature (#1269). The cost of the convenience is
the missing-comma footgun: a dropped comma in a list of strings silently concatenates two elements
into one, and the list comes out short with no diagnostic anywhere.

```python
xs = ["a", "b" "c"]     # Python: ['a', 'bc'] — two elements, silently
```

Axiom precedence: type safety (3) > Python syntax (2). Catalogued in `docs/deviations.yaml` as
`no-implicit-string-concat`.

