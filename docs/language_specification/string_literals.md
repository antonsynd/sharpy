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
