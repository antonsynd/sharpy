# Type Annotations

```python
# Simple types
x: int = 42
name: str = "Alice"
flag: bool = True
pi: float = 3.14159

# Type inference (annotation optional when initializer present)
y = 42              # Inferred as int
pi = 3.14159        # Inferred as float
```

*Implementation*
- *✅ Native - Direct mapping to C# type declarations.*

## Shorthand Syntax

Sharpy supports shorthand syntax for common collection types. See [Type Annotation Shorthand](type_annotation_shorthand.md) for details.

```python
# Shorthand and canonical forms are equivalent:
items: [int] = [1, 2, 3]           # Shorthand for list[int]
scores: {str: int} = {}            # Shorthand for dict[str, int]
unique: {int} = {1, 2, 3}          # Shorthand for set[int]
point: (int, int) = (10, 20)       # Shorthand for tuple[int, int]
```
