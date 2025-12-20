# Comments

Comments in Sharpy work as they do in Python. Some examples, but not exhaustive:

```python
# This is a single-line comment

x = 42  # Comment at end of line

# Comments can span multiple lines
# by using # at the start of each line

"""
However, this is a docstring, not a comment.
Docstrings are string literals used for documentation.
"""
```

**Comment Rules:**
- Single-line comments start with `#` and continue to end of line
- `#` inside string literals is not a comment
- No multi-line comment syntax (like `/* */` in C)
- Docstrings (triple-quoted strings) can serve as multi-line documentation,
but are not comments

**C# Implementation**:
✅ Native - Converted to `//` comments in C#.
