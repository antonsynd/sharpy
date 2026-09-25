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

<!-- spec-sweep: fragment -->
```python
T           # Non-nullable type
T?          # Optional[T] — safe tagged union (Sharpy-native)
T | None    # C# nullable — .NET interop only
T !E        # Result[T, E] — for return type annotations
```

- `T?` is syntactic sugar for `Optional[T]`. See [Optional Type](tagged_unions_optional.md).
- `T | None` is the **only** valid inline union form. No free unions like `int | str`. See [Nullable Types](nullable_types.md).
- `T !E` is syntactic sugar for `Result[T, E]`, recommended for top-level return types. See [Result Type](tagged_unions_result.md).

## `None` in Annotations

`None` is a value, not a type. It appears in an annotation only as a **return type**: `-> None` says a function returns no value, and that includes the return slot of a function type, `(str) -> None`. Any other annotation — a local, parameter, field, module variable, constant, type argument (`list[None]`), a function type's parameter, or `None?` — could only ever hold `None`, so it is refused with SPY0614. A slot that may hold `None` is spelled `T | None` (or `T?` for an `Optional`), and `object` holds any value.

```python
def log(message: str) -> None:
    print(message)

def main() -> None:
    handler: (str) -> None = log
    count: int | None = None
    handler("count is " + str(count))
```

Output: `count is None`.

<!-- spec-sweep: error SPY0614 -->
```python
def describe(x: None) -> str:   # SPY0614: 'None' is not a type; annotate ... as `T | None`, or use `object`
    return "nothing"
```

*Implementation*
- *🔄 Lowered - `-> None` is C# `void`. A value-position `None` has no C# spelling (`void` is not a value type), so it is refused at semantic analysis rather than reaching the C# compiler.*

## Shorthand Syntax

Sharpy supports shorthand syntax for common collection types. See [Type Annotation Shorthand](type_annotation_shorthand.md) for details.

```python
# Shorthand and canonical forms are equivalent:
items: [int] = [1, 2, 3]           # Shorthand for list[int]
scores: {str: int} = {}            # Shorthand for dict[str, int]
unique: {int} = {1, 2, 3}          # Shorthand for set[int]
point: (int, int) = (10, 20)       # Shorthand for tuple[int, int]
```
