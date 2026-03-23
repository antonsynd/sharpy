# Classes

## Basic Class Definition

```python
class Person:
    """A person with a name and age."""

    # Field declarations (required)
    name: str
    age: int

    # Constructor
    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    # Instance method
    def greet(self) -> str:
        return f"Hello, I'm {self.name}"

    def celebrate_birthday(self):
        self.age += 1
```

**Rules:**
- All instance fields must be declared at class level with type annotations or an assignment of a default value where the type can be inferred
- The `self` parameter is required for instance methods
- The `self` parameter is not type-annotated and cannot be annotated
- Sharpy supports the `Self` type (inspired by PEP 673) for methods that return the enclosing class type:

```python
class Builder:
    name = ""

    def with_name(self, name: str) -> Self:
        self.name = name
        return self
```

- `Self` resolves to the concrete enclosing class type at compile time
- `Self` can be used as a return type or parameter type in methods
- `Self` is valid only inside class, struct, or interface bodies (SPY0384 outside, SPY0385 in static methods)
- `Self` can also be used as a parameter type for same-type operations:

```python
class Vector:
    x: int
    y: int

    def add(self, other: Self) -> Self:
        return Vector(self.x + other.x, self.y + other.y)
```

- `__init__` return type is implicitly `None` and can be omitted or explicitly declared

```python
class Person:
    name: str

    # Both forms are valid and equivalent:
    def __init__(self, name: str):           # Implicit None return
        self.name = name

    def __init__(self, name: str) -> None:   # Explicit None return
        self.name = name
```

**Note:** The rule "return type can be omitted for `-> None` functions" applies universally to all functions and methods, not just `__init__`. This is consistent with C# where `void` methods simply have no return type in the signature:

```python
class Counter:
    value: int = 0

    def increment(self):        # Implicit -> None
        self.value += 1

    def reset(self) -> None:    # Explicit -> None (both valid)
        self.value = 0
```

*Implementation*
- *✅ Native - Direct mapping to C# class.*
