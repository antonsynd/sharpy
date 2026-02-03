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
| `\xHH` | Character with hex value HH |
| `\uHHHH` | Unicode 16-bit |
| `\UHHHHHHHH` | Unicode 32-bit |

*Implementation*
- *✅ Native - Single quotes become double quotes; escape sequences map directly.*

## Raw Strings

```python
# Raw strings (backslashes not escaped)
path = r"C:\Users\Alice\Documents"
regex = r"\d+\.\d+"
```

*Implementation*
- *✅ Native - Maps to C# verbatim strings `@"..."`.*
