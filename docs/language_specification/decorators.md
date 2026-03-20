# Decorators

Decorators modify the behavior of functions, members, methods, and classes.

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
@static
@override
@final
@public          # Access modifiers last
@protected
@private
@internal
```

Note that Sharpy does not support any version of class methods equating to Python's `@classmethod` decorator. However, it does support something like Python's `@staticmethod`, which is named `@static` in Sharpy. See [static_methods.md](static_methods.md) and [class_methods.md](class_methods.md) respectively for details.

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
| `@static` | `static` | Class-level method, no `self` parameter. Can be omitted if the first parameter is not `self`. It is a compile-time error to use it on a method with `self` as the first parameter. |
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

    # Also valid, `@static` is implied when the method
    # does not have `self` as the first parameter.
    def add(x: int, y: int) -> int:
        return x + y

    # WRONG: Cannot use `@static` on a method that has
    # `self` as the first parameter, as that makes it an
    # instance method.
    @static
    def reverse_add(self, x: int, y: int) -> int:
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

Classes can be marked `@abstract` to indicate they cannot be instantiated directly and may contain abstract members. A class with any abstract members must be marked `@abstract`.

**Streamlined Abstract Method Syntax:**

Methods in an `@abstract` class with an ellipsis (`...`) body are automatically treated as abstract - no explicit `@abstract` decorator is needed on the method. You can also use the inline ellipsis syntax for maximum brevity:

```python
@abstract
class Shape:
    name: str

    def __init__(self, name: str):
        self.name = name

    # Implicit abstract - ellipsis body in @abstract class
    def area(self) -> float: ...
    def perimeter(self) -> float: ...

    # Non-abstract methods have real implementations
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

The explicit `@abstract` decorator on methods is still supported but optional when using ellipsis body in an `@abstract` class.

**Abstract method body conventions:**
- `...` (ellipsis) = abstract, no implementation — requires `@abstract` class or interface context
- `pass` = concrete empty body (default implementation)
- Body-less (no colon, no body) = **deprecated** (emits SPY0464 warning), use `...` instead

```python
@abstract
class Shape:
    # Preferred forms:
    def area(self) -> float: ...           # Implicit abstract (preferred)

    @abstract
    def perimeter(self) -> float: ...      # Explicit abstract with ellipsis body

    @abstract
    def volume(self) -> float              # DEPRECATED: body-less form (SPY0464 warning)
```

**Note:** Ellipsis body in a *non-abstract* class generates a `NotImplementedException` stub instead of an abstract method:

```python
class TodoService:
    def not_done_yet(self) -> int: ...     # Generates: throw new NotImplementedException()
```

*Implementation: ✅ Native - Direct mapping to C# keywords.*


## Custom Decorators / .NET Attributes

Unknown decorators (those not in the set of built-in modifier decorators) are emitted as C# attributes, enabling full .NET interop. Built-in modifier decorators (`@virtual`, `@static`, `@abstract`, `@override`, `@final`, `@public`, `@protected`, `@private`, `@internal`) continue to map to C# modifier keywords and **must not** have arguments.

### Syntax

```
decorator ::= '@' qualified_name [ '(' [ arguments ] ')' ] NEWLINE
```

Decorators support:
- **Positional arguments**: `@obsolete("Use new_method instead")`
- **Keyword (named) arguments**: `@dll_import("user32.dll", entry_point="MessageBox")`
- **Empty parentheses**: `@my_attr()`
- **Dotted names**: `@system.serializable`, `@system.obsolete("old")`

### Argument Restrictions

Decorator arguments must be **compile-time constants**, matching C# attribute argument restrictions:
- String, int, float, bool literals
- `None` (maps to `null`)
- Enum member access (e.g., `StringComparison.ordinal`)
- `type(X)` (maps to `typeof(X)` in C#)

Non-constant expressions (e.g., `1 + 2`, variable references, function calls other than `type()`) are rejected at compile time with SPY0425.

### Name Mangling

Decorator names follow the same snake_case → PascalCase mangling as other identifiers:

| Sharpy | C# Output |
|--------|-----------|
| `@obsolete("msg")` | `[Obsolete("msg")]` |
| `@dll_import("lib")` | `[DllImport("lib")]` |
| `@conditional("DEBUG")` | `[Conditional("DEBUG")]` |
| `@default_value(42)` | `[DefaultValue(42)]` |
| `@system.serializable` | `[System.Serializable]` |
| `@system.obsolete("old")` | `[System.Obsolete("old")]` |

Keyword argument names are also PascalCase-mangled: `entry_point="Func"` → `EntryPoint = "Func"`.

### Known vs. Unknown Decorators

| Category | Behavior | Arguments |
|----------|----------|-----------|
| Built-in modifier (`@virtual`, `@static`, etc.) | Maps to C# keyword modifier | **Not allowed** (SPY0322) |
| Unknown | Emitted as C# `[Attribute]` | Allowed (compile-time constants only) |

### Examples

```python
# Simple attribute with argument
@obsolete("Use bar() instead")
def foo() -> None:
    pass

# Dotted attribute name
@system.serializable
class Config:
    pass

# Multiple arguments with keyword
@dll_import("user32.dll", entry_point="MessageBox")
def message_box() -> None: ...

# Combining modifier and attribute decorators
@virtual
@obsolete("Will be removed in v2")
def legacy_method(self) -> None:
    pass

# type() maps to typeof() in attribute arguments
@custom_attr(type(int))
def typed_method() -> None:
    pass
```

*Implementation: ✅ Emitted as C# attributes via Roslyn SyntaxFactory.*

## Flexible Argument Decorators

> **Dropped** — `@kwargs` and `@dynamic_kwargs` were removed from the roadmap. Compiler-understood transforming decorators violate the "no magic" principle, and `@dynamic_kwargs` conflicts with Axiom 3 (type safety). Named arguments with default values and user-defined option structs provide equivalent functionality without invisible code generation. See [SRP-0001](../rejected_proposals/SRP-0001-kwargs-decorator.md) and [SRP-0002](../rejected_proposals/SRP-0002-dynamic-kwargs-decorator.md) for full rationale.
