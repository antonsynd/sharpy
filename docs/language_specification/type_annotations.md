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

## Nullability and Optional Type Syntax

```python
T           # Non-nullable type
T?          # Optional[T] — safe tagged union (Sharpy-native)
T | None    # C# nullable — .NET interop only
T !E        # Result[T, E] — for return type annotations
```

- `T?` is syntactic sugar for `Optional[T]`. See [Optional Type](tagged_unions_optional.md).
- `T | None` is the **only** valid inline union form. No free unions like `int | str`. See [Nullable Types](nullable_types.md).
- `T !E` is syntactic sugar for `Result[T, E]`, recommended for top-level return types. See [Result Type](tagged_unions_result.md).

## Shorthand Syntax

Sharpy supports shorthand syntax for common collection types. See [Type Annotation Shorthand](type_annotation_shorthand.md) for details.

```python
# Shorthand and canonical forms are equivalent:
items: [int] = [1, 2, 3]           # Shorthand for list[int]
scores: {str: int} = {}            # Shorthand for dict[str, int]
unique: {int} = {1, 2, 3}          # Shorthand for set[int]
point: (int, int) = (10, 20)       # Shorthand for tuple[int, int]
```
