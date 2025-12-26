# Dunder Invocation Rules

Dunder methods (double-underscore methods like `__add__`, `__eq__`) define *how* a type behaves with operators and built-in functions. However, **dunder methods are a definition mechanism only**—users invoke that behavior through operators and built-in functions, not by calling dunders directly.

## Dunders Are Definition-Only

**Explicit dunder invocation by user code is a compile error:**

```python
x = 5
x.__eq__(3)         # ERROR: Cannot invoke dunder methods directly

my_list = [1, 2, 3]
my_list.__len__()   # ERROR: Cannot invoke dunder methods directly

obj = MyClass()
obj.__str__()       # ERROR: Cannot invoke dunder methods directly
```

## Correct Usage

Use operators for operator dunders:

```python
x == y              # ✅ Correct — compiler uses __eq__ internally
x + y               # ✅ Correct — compiler uses __add__ internally
-x                  # ✅ Correct — compiler uses __neg__ internally
x < y               # ✅ Correct — compiler uses __lt__ internally
x[0]                # ✅ Correct — compiler uses __getitem__ internally
```

Use built-in functions for protocol dunders:

```python
len(x)              # ✅ Correct — uses __len__ internally
hash(x)             # ✅ Correct — uses __hash__ internally
str(x)              # ✅ Correct — uses __str__ internally
```

## Rationale

- **Uniform syntax**: `str(x)` and `x == y` work on any type, whether primitive or Sharpy-defined
- **.NET interop**: Primitives from .NET (`int`, `str`, `bool`) don't have dunder methods—the compiler handles dispatch
- **Zero overhead**: No wrapper types or boxing required for polymorphic dispatch
- **Consistency**: Same syntax works whether the type defines a dunder or uses native behavior

## Summary Table

| Context | Allowed? |
|---------|----------|
| User code calling `x.__dunder__()` | ❌ Compile error |
| Inside dunder method, calling `self.__other_dunder__()` | ✅ Allowed |
| Inside dunder method, calling `super().__dunder__()` | ✅ Allowed |
| Inside dunder method, calling `other_obj.__dunder__()` | ❌ Use operator/built-in |
| Inside regular method, calling `self.__dunder__()` | ❌ Use built-in function |

*Implementation: The compiler emits different code based on static type:*
- *For primitives: direct C# operator or method call*
- *For Sharpy types with dunder: call to the generated method*
- *For built-in functions: type-appropriate dispatch (e.g., `len()` calls `.Count` or `__len__`)*

## Dunder Inheritance and Internal Calls

While user code cannot call dunders directly, there are specific contexts where dunder calls are permitted.

### Dunder Inheritance

Dunder methods are inherited like any other method:

```python
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    def __str__(self) -> str:
        return f"Animal({self.name})"

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    # Inherits __str__ from Animal

dog = Dog("Buddy")
print(str(dog))  # Output: Animal(Buddy)
```

### Overriding Dunders

Dunder methods can be overridden using `@override`:

```python
class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    @override
    def __str__(self) -> str:
        return f"Dog({self.name})"

dog = Dog("Buddy")
print(str(dog))  # Output: Dog(Buddy)
```

**Note:** The `@override` decorator is **required** when overriding inherited dunder methods, just like any other virtual method. All inheritable dunder methods from base classes are implicitly `@virtual`.

```python
class MyClass:
    value: int

    def __init__(self, value: int):
        self.value = value

    # Must use @override since __str__ is inherited from System.Object (`ToString()`)
    @override
    def __str__(self) -> str:
        return f"MyClass({self.value})"

    # Same for __eq__, __hash__, etc.
    @override
    def __eq__(self, other: object) -> bool:
        if not isinstance(other, MyClass):
            return False
        return self.value == other.value

    @override
    def __hash__(self) -> int:
        return hash(self.value)
```

This matches C# where overriding `ToString()`, `Equals()`, and `GetHashCode()` from `System.Object` requires the `override` keyword.

### Base Class Dunder Calls

Within a dunder method, you may call the base class implementation via `super()`:

```python
class Child(Parent):
    @override
    def __str__(self) -> str:
        return super().__str__() + " (child)"  # ✅ OK

    @override
    def __eq__(self, other: object) -> bool:
        if not super().__eq__(other):           # ✅ OK
            return False
        # Additional checks...
        return True
```

### Cross-Dunder Calls for Synthesis

Within a dunder method, you may call other dunders on `self` for synthesizing related operations:

```python
class Ordered:
    value: int

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Ordered):
            return False
        return self.value == other.value

    def __lt__(self, other: Ordered) -> bool:
        return self.value < other.value

    def __le__(self, other: Ordered) -> bool:
        return self.__lt__(other) or self.__eq__(other)  # ✅ OK

    def __ge__(self, other: Ordered) -> bool:
        return not self.__lt__(other)                    # ✅ OK

    def __ne__(self, other: object) -> bool:
        return not self.__eq__(other)                    # ✅ OK

    def __gt__(self, other: Ordered) -> bool:
        return not self.__le__(other)                    # ✅ OK
```

### Restrictions

Dunder calls on `self` or `super()` are **only** permitted:
- Within a dunder method body
- As immediate call expressions (cannot be captured or passed)

```python
class Example:
    def __str__(self) -> str:
        func = self.__eq__              # ❌ ERROR: Cannot capture dunder
        return str(self.__hash__())     # ✅ OK: Immediate call, cross-dunder

    def regular_method(self):
        self.__str__()                  # ❌ ERROR: Not inside a dunder
        print(str(self))                # ✅ OK: Use built-in function

    def __eq__(self, other: object) -> bool:
        return other.__eq__(self)       # ❌ ERROR: Not self or super()
```

### Child Objects Use Built-in Functions

For calling dunder-like behavior on other objects (including fields), use operators or built-in functions:

```python
class Node:
    left: Node?
    right: Node?
    value: int

    def __str__(self) -> str:
        left_str = str(self.left) if self.left is not None else "None"
        right_str = str(self.right) if self.right is not None else "None"
        return f"Node({self.value}, {left_str}, {right_str})"
        # NOT: self.left.__str__()  # ❌ Would be error anyway

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Node):
            return False
        return self.value == other.value  # ✅ Use == operator
        # NOT: self.value.__eq__(other.value)  # ❌ Error
```

### Summary Table

| Call Site | `self.__dunder__()` | `super().__dunder__()` | `other.__dunder__()` |
|-----------|--------------------|-----------------------|---------------------|
| Inside dunder method | ✅ Immediate only | ✅ Immediate only | ❌ Use operator/built-in |
| Outside dunder method | ❌ Error | ❌ Error | ❌ Use operator/built-in |

## See Also

- [Operator Overloading](operator_overloading.md) - Defining custom operators via dunders
- [Dunder Methods](dunder_methods.md) - Complete dunder method reference
- [Built-in Functions](builtin_functions.md) - Functions that dispatch to dunders
