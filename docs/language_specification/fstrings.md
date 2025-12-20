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

*Implementation: ✅ Native - Maps to C# interpolated strings `$"..."`.*
