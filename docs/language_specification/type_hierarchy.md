# Type Hierarchy and Object Model

## Universal Base Type

The `object` type (mapping to `System.Object`) is the universal base type for all Sharpy types. All primitives (`int`, `str`, `bool`, etc.) and all Sharpy-defined types are assignable to `object`:

```python
# object accepts any value
x: object = 42
x = "hello"
x = [1, 2, 3]
x = MyClass()

# Useful for heterogeneous collections
items: list[object] = [1, "hello", True, SomeClass()]

# Function accepting any type
def process(value: object) -> str:
    return str(value)
```

## Type Hierarchy

- `object` in type annotations maps to `System.Object`
- Sharpy-defined classes implicitly inherit from `Sharpy.Core.Object` (which itself inherits from `System.Object`)
- `Sharpy.Core.Object` provides default implementations for `__eq__`, `__ne__`, `__hash__`, `__str__`, and `__repr__` that delegate to the .NET equivalents (`Equals()`, `GetHashCode()`, `ToString()`)
- The base class insertion happens during transpilation—user code simply writes `class MyClass:` and the compiler inserts the inheritance
- Primitives (`int`, `str`, `bool`, etc.) are assignable to `object` via boxing
- Structs are assignable to `object` via boxing
- `None` is assignable to `object?` but not to `object`

*Implementation*
- *🔄 Lowered - Sharpy-defined classes inherit from `Sharpy.Core.Object`; `object` type annotations map to `System.Object`.*
