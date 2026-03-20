# Interfaces

Interfaces define contracts that types must satisfy.

```python
interface IDrawable:
    """Interface for drawable objects."""

    # Preferred abstract method syntaxes:

    # 1. Inline ellipsis syntax (preferred)
    def draw(self): ...

    # 2. Block ellipsis syntax
    def get_bounds(self) -> tuple[float, float, float, float]:
        ...

    # 3. Body-less syntax (DEPRECATED - emits SPY0464 warning)
    def resize(self, width: float, height: float)

# Implementation
class Circle(IDrawable):
    radius: float
    x: float
    y: float

    def __init__(self, x: float, y: float, radius: float):
        self.x = x
        self.y = y
        self.radius = radius

    def draw(self):
        print(f"Drawing circle at ({self.x}, {self.y})")

    def get_bounds(self) -> tuple[float, float, float, float]:
        return (self.x - self.radius, self.y - self.radius,
                self.radius * 2, self.radius * 2)

    def resize(self, width: float, height: float):
        self.radius = min(width, height) / 2
```

**Interface Rules:**
- All methods are implicitly abstract unless they have a body that is not `...` (ellipsis).
- **Abstract method syntaxes:**
  1. **Inline ellipsis** (preferred): `def method(self) -> T: ...` — Colon + ellipsis on same line
  2. **Block ellipsis**: Colon, newline, indent, then `...`
  3. **Body-less** (**deprecated**, SPY0464 warning): `def method(self) -> T` — No colon, no body
- **Body conventions:** `...` = abstract (no implementation), `pass` = concrete empty body (default implementation)
- Methods with an actual body (even just a `pass` statement) become the default implementation.
- Implementing types must provide all methods that don't have a default implementation
and can override the implementation of those that do have a default implementation.

```python
interface ISomeInterface:
    # This method is abstract (the use of the ellipsis literal signals this)
    def method(self):
        ...

    # This method has a default implementation with an empty body
    def method2(self):
        pass
```

**Non-Void Methods and Empty Bodies:**

For non-void methods in interfaces (or anywhere), using `pass` alone is a compile error because the method must return a value:

```python
interface IFoo:
    # ✅ OK - abstract method (no implementation required)
    def get_value(self) -> int:
        ...

    # ❌ ERROR - non-void method body must return a value
    def get_other(self) -> int:
        pass  # Compile error: missing return statement

    # ✅ OK - provides a default return value
    def get_default(self) -> int:
        return 0
```

The distinction is:
- No colon/body → abstract, no implementation (interface methods only)
- `...` (ellipsis) → abstract, no implementation
- `pass` → empty body, valid only for `-> None` methods as a default implementation
- For non-void methods, use body-less, `...` (abstract), or provide a return statement

*Implementation*
- *✅ Native - Direct mapping to C# `interface`.*

## Generic Interfaces

```python
interface IContainer[T]:
    def add(self, item: T) -> None: ...
    def get(self, index: int) -> T: ...
    def count(self) -> int: ...
```

## Interface Inheritance

Interfaces can extend other interfaces:

```python
interface ISerializable:
    """Base interface for serialization."""
    def serialize(self) -> str: ...

interface IJSONSerializable(ISerializable):
    """Extends ISerializable with JSON-specific methods."""
    def to_json(self) -> str: ...
    def from_json(self, json: str): ...

class User(IJSONSerializable):
    """Must implement all methods from both interfaces."""
    username: str

    def serialize(self) -> str:
        return self.username

    def to_json(self) -> str:
        return f'{{"username": "{self.username}"}}'

    def from_json(self, json: str):
        pass  # Parse and update
```

## Default Method Implementations

Interfaces can provide default implementations for methods. Implementing types inherit the default unless they provide their own implementation.

For comprehensive coverage of default methods including conflict resolution, comparison with abstract classes, and multiple interface conflicts, see [Interface Default Methods](interface_default_methods.md).

```python
interface ILogger:
    def log(self, message: str):
        """Log a message. Must be implemented."""
        ...

    def log_info(self, message: str):
        """Log an info message. Has default implementation."""
        self.log(f"[INFO] {message}")

class ConsoleLogger(ILogger):
    # Must implement abstract method
    def log(self, message: str):
        print(message)

    # Inherits log_info default
```

*Implementation*
- *✅ Native - Direct mapping to C# default interface methods (C# 8.0+).*
```

*Implementation*
- *✅ Native - Direct mapping to C# default interface methods (C# 8.0+).*

## Dunder Methods in Interfaces

**Standard Library Only:**

Only interfaces defined in the Sharpy standard library can declare dunder methods. User-defined interfaces cannot declare dunders.

```python
# ✅ Standard library interface (Sharpy.Core)
interface IContextManager:
    def __enter__(self) -> object:
        ...

    def __exit__(self, exc_type: Type?, exc_val: Exception?, exc_tb: object?) -> bool:
        ...

# ✅ Standard library interface
interface IHashable:
    def __hash__(self) -> int:
        ...

    def __eq__(self, other: object) -> bool:
        ...

# ❌ ERROR: User-defined interface cannot declare dunders
interface IMyProtocol:
    def __custom__(self) -> int:    # ERROR: dunder methods not allowed
        ...

    def __len__(self) -> int:       # ERROR: dunder methods not allowed
        ...
```

**Rationale:**

1. **Controlled semantics**: Dunder methods have special meaning and compiler integration. Restricting them to the standard library ensures consistent behavior.

2. **Operator dispatch**: The compiler needs to know exactly which dunders exist and what they do. User-defined dunders would break this model.

3. **.NET interop**: Standard library interfaces map to well-known .NET interfaces (e.g., `IEnumerable`).

**Implementing Standard Library Dunder Interfaces:**

User code can implement standard library interfaces that contain dunders:

```python
from sharpy.core import IContextManager

class ManagedResource(IContextManager):
    _handle: int

    def __init__(self):
        self._handle = acquire_resource()

    def __enter__(self) -> ManagedResource:
        return self

    def __exit__(self, exc_type: Type?, exc_val: Exception?, exc_tb: object?) -> bool:
        release_resource(self._handle)
        return False  # Don't suppress exceptions

# Usage
with ManagedResource() as resource:
    use(resource)
```

*Implementation*
- *Compiler validates that dunder declarations only appear in whitelisted standard library interfaces.*

## See Also

- [Generic Variance](generic_variance.md) — Covariance (`out`) and contravariance (`in`) on interface type parameters
- [Inheritance](inheritance.md) — Class inheritance and interface implementation
- [Interface Default Methods](interface_default_methods.md) — Default method implementations
