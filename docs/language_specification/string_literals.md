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
- *✅ Native - Single quotes become double quotes; escape sequences map directly.*

## Raw Strings

```python
# Raw strings (backslashes not escaped)
path = r"C:\Users\Alice\Documents"
regex = r"\d+\.\d+"
```

*Implementation*
- *✅ Native - Maps to C# verbatim strings `@"..."`.*

## Native String Literals

Native string literals produce a `System.String` value instead of `Sharpy.Str`. Use them when interfacing with .NET APIs that expect `System.String`, or when you need to avoid the `Sharpy.Str` wrapper overhead.

```python
# Single-quoted native strings
path = n'hello'
message = n"Hello, World!"

# Triple-quoted native strings
text = n"""
Multi-line native string
"""
alt = n'''
Also a native string
'''

# Raw native strings (no escape processing)
regex = nr"\d+\.\d+"
win_path = nr"C:\Users\Alice\Documents"
```

### When to Use Native Strings

| Scenario | Use |
|----------|-----|
| Normal Sharpy code | `"hello"` (regular string → `Sharpy.Str`) |
| .NET interop requiring `System.String` | `n"hello"` (native string → `System.String`) |
| Regex patterns for .NET Regex API | `nr"\d+"` (raw native string) |
| Performance-critical code avoiding Str wrapper | `n"hello"` |

### Type Relationship

```python
s: str = "hello"           # Sharpy.Str
ns: str = n"hello"         # System.String
# Implicit conversion allows assignment:
mixed: str = n"native"     # System.String implicitly converts to Str
```

*Implementation*
- *✅ Native - `n"..."` emits a C# `string` literal directly; `nr"..."` emits `@"..."`.*
