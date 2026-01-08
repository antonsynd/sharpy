# Decorators

Decorators modify the behavior of functions, methods, and classes.

**Decorator Ordering:**

When multiple decorators are applied, they are processed bottom-up (closest to the definition first), matching Python semantics:

```python
@A
@B
def foo():
    ...
# Equivalent to: foo = A(B(foo))
```

For Sharpy's built-in decorators (`@virtual`, `@override`, `@abstract`, `@final`, etc.), the order typically doesn't matter since they're metadata flags rather than transforming decorators. However, it's conventional to place them in a consistent order:

```python
# Recommended ordering (when applicable)
@virtual         # Inheritance behavior
@override
@final
@protected       # Access modifiers last
@private
@internal
```

Note that Sharpy does not support any version of static or class methods equating to Python's `@staticmethod` and `@classmethod` decorators. See [static_methods.md](static_methods.md) and [class_methods.md](class_methods.md) respectively for details.

## Access Modifiers

| Decorator | C# Equivalent | Visibility |
|-----------|---------------|------------|
| `@public` (can be omitted, it is the default) | `public` | Everyone |
| `@protected` or `_name` | `protected` | Class and derived |
| `@private` or `__name` | `private` | Declaring class only |
| `@internal` | `internal` | Same assembly |

**Assembly Boundaries for `@internal`:**

In Sharpy, an assembly corresponds to a compiled project. Assembly boundaries are defined by:

- A `.spyproj` project file defines a single assembly
- All `.spy` files in the same project compile to the same assembly
- Each referenced project becomes a separate assembly

`@internal` members are accessible from any file within the same project but not from other projects that reference it.

```python
# In mylib/internal_utils.spy (part of mylib.spyproj)
@internal
def helper_function() -> None:
    pass

# In mylib/public_api.spy (same project) - OK
from mylib.internal_utils import helper_function  # ✅ Same assembly

# In app/main.spy (different project referencing mylib) - ERROR
from mylib.internal_utils import helper_function  # ❌ Different assembly
```

```python
class Example:
    @private
    def internal_method(self) -> None:
        pass

    # Naming convention also works
    def _protected_method(self) -> None:
        pass

    def __private_method(self) -> None:
        pass
```

*Implementation: ✅ Native - Direct mapping to C# access modifiers.*

## Method Modifiers

| Decorator | C# Equivalent | Notes |
|-----------|---------------|-------|
| `@static` | `static` | Class-level method, no `self` parameter |
| `@override` | `override` | Override virtual/abstract base method |
| `@virtual` | `virtual` | Method can be overridden by subclasses |
| `@abstract` | `abstract` | Must be overridden, no implementation |
| `@final` (method) | `sealed override` | Prevents further overriding |
| `@final` (class) | `sealed class` | Prevents inheritance |
| `@abstract` (class) | `abstract class` | Cannot be instantiated, may contain abstract members |

```python
class Calculator:
    @static
    def add(x: int, y: int) -> int:
        return x + y

    @virtual
    def compute(self, x: int) -> int:
        return x * 2

    @override
    def __str__(self) -> str:
        return "Calculator"

class ScientificCalculator(Calculator):
    @override
    def compute(self, x: int) -> int:
        return x ** 2

    @final
    @override
    def __str__(self) -> str:
        return "ScientificCalculator"

@final
class CannotBeExtended:
    """This class cannot be subclassed."""
    pass

# Usage
result = Calculator.add(5, 3)        # Static method call
calc = ScientificCalculator()
calc.compute(4)                      # Returns 16 (overridden method)
```

**Note:** Sharpy uses `@final` rather than C#'s `sealed` keyword to align with Python's `typing.final` decorator and Java's `final` keyword. The compiled output uses C#'s `sealed` keyword.

**Abstract Classes:**

Classes can be marked `@abstract` to indicate they cannot be instantiated directly and may contain abstract members. A class with any abstract members must be marked `@abstract`:

```python
@abstract
class Shape:
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstract
    def area(self) -> float:
        ...  # Must be implemented by subclasses

    @abstract
    def perimeter(self) -> float:
        ...  # Must be implemented by subclasses

    # Non-abstract methods are allowed
    def describe(self) -> str:
        return f"{self.name} with area {self.area()}"

class Circle(Shape):
    radius: float

    def __init__(self, radius: float):
        super().__init__("Circle")
        self.radius = radius

    @override
    def area(self) -> float:
        return 3.14159 * self.radius ** 2

    @override
    def perimeter(self) -> float:
        return 2 * 3.14159 * self.radius

# Usage
# shape = Shape("test")    # ERROR: Cannot instantiate abstract class
circle = Circle(5.0)       # OK
print(circle.describe())   # "Circle with area 78.53975"
```

*Implementation: ✅ Native - Direct mapping to C# keywords.*
