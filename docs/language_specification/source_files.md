# Source Files **[v0.1.0]**

## File Extension and Encoding

- File extension: `.spy`
- Encoding: UTF-8 (required)
- Line endings: LF (`\n`) or CRLF (`\r\n`)
- Byte Order Mark (BOM): Optional but not recommended

*Implementation: ✅ Native - Source encoding handled by .NET's text processing.*

## Line Structure

Sharpy uses indentation to denote code blocks (like Python):

```python
if condition:
    statement1  # 4 spaces indentation
    statement2  # 4 spaces indentation
else:
    statement3  # 4 spaces indentation
```

## Physical Lines vs Logical Lines

**Physical line:** A sequence of characters terminated by end-of-line

**Logical line:** A statement (may span multiple physical lines)

```python
# Single logical line
x = 42

# Logical line split across physical lines (explicit)
total = value1 + \
        value2 + \
        value3

# Implicit line continuation inside brackets
items = [
    1, 2, 3,
    4, 5, 6
]

# Function call with multiple arguments
result = function(
    arg1,
    arg2,
    arg3
)
```

**Line Continuation Rules:**
- Explicit: Use backslash `\` at end of line
- Implicit: Inside `()`, `[]`, `{}` brackets
- Cannot continue in middle of identifier or keyword
- Cannot continue inside single-line strings

## Newline Significance

Newlines are significant in Sharpy and terminate statements:

**Rules:**
1. Newlines are significant and terminate statements
2. Except inside brackets `()`, `[]`, `{}`
3. Except after backslash `\` continuation
4. Newlines inside string literals are literal newlines

```python
# Newline terminates statement
x = 42
y = 10

# Implicit continuation in brackets
result = (
    value1 +
    value2
)

# Newline inside string literal is literal newline
multi = """Line 1
Line 2"""
```

*Implementation: ✅ Native - Continuation is handled at lex time; resulting logical lines transpile normally.*
