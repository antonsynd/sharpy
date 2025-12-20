## Interfaces **[v0.1.2]**

Interfaces define contracts that types must satisfy.

```python
interface IDrawable:
    """Interface for drawable objects."""

    def draw(self) -> None:
        ...

    def get_bounds(self) -> tuple[double, double, double, double]:
        ...

# Implementation
class Circle(IDrawable):
    radius: double
    x: double
    y: double

    def __init__(self, x: double, y: double, radius: double):
        self.x = x
        self.y = y
        self.radius = radius

    def draw(self) -> None:
        print(f"Drawing circle at ({self.x}, {self.y})")

    def get_bounds(self) -> tuple[double, double, double, double]:
        return (self.x - self.radius, self.y - self.radius,
                self.radius * 2, self.radius * 2)
```

**Interface Rules:**
- All methods are implicitly abstract unless they have a body that is not `...` (ellipsis), excluding docstrings, whitespace, and comments.
- Methods with an actual body (even just a `pass` statement) become the default implementation for that method.
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
- `...` (ellipsis) → abstract, no implementation
- `pass` → empty body, valid only for `-> None` methods as a default implementation
- For non-void methods, either use `...` (abstract) or provide a return statement

*Implementation: ✅ Native - Direct mapping to C# `interface`.*

### Generic Interfaces **[v0.1.3]**

```python
interface IContainer[T]:
    def add(self, item: T) -> None: ...
    def get(self, index: int) -> T: ...
    def count(self) -> int: ...
```

### Interface Inheritance **[v0.1.2]**

Interfaces can extend other interfaces:

```python
interface ISerializable:
    """Base interface for serialization."""
    def serialize(self) -> str: ...

interface IJSONSerializable(ISerializable):
    """Extends ISerializable with JSON-specific methods."""
    def to_json(self) -> str: ...
    def from_json(self, json: str) -> None: ...

class User(IJSONSerializable):
    """Must implement all methods from both interfaces."""
    username: str

    def serialize(self) -> str:
        return self.username

    def to_json(self) -> str:
        return f'{{"username": "{self.username}"}}'

    def from_json(self, json: str) -> None:
        pass  # Parse and update
```

### Default Method Implementations

Interfaces can provide default implementations for methods. Implementing types inherit the default unless they provide their own implementation:

```python
interface ILogger:
    def log(self, message: str) -> None:
        """Log a message. Must be implemented."""
        ...

    def log_info(self, message: str) -> None:
        """Log an info message. Has default implementation."""
        self.log(f"[INFO] {message}")

    def log_error(self, message: str) -> None:
        """Log an error message. Has default implementation."""
        self.log(f"[ERROR] {message}")

class ConsoleLogger(ILogger):
    # Must implement abstract method
    def log(self, message: str) -> None:
        print(message)

    # Inherits log_info and log_error defaults
    # Can optionally override them

class FileLogger(ILogger):
    path: str

    def __init__(self, path: str):
        self.path = path

    def log(self, message: str) -> None:
        # Write to file
        pass

    # Override default to add timestamp
    def log_error(self, message: str) -> None:
        self.log(f"[ERROR {datetime.now()}] {message}")
```

**Calling Other Interface Methods:**

Default implementations can call other methods defined in the same interface (including methods inherited by the interface through parent interfaces, without using `super()`):

```python
interface IValidator:
    def validate(self, value: str) -> bool:
        """Core validation logic. Must be implemented."""
        ...

    def is_valid(self, value: str) -> bool:
        """Check validity, returning boolean."""
        return self.validate(value)

    def validate_or_raise(self, value: str) -> None:
        """Validate and raise if invalid."""
        if not self.validate(value):
            raise ValueError(f"Invalid value: {value}")

    def validate_all(self, values: list[str]) -> bool:
        """Validate multiple values."""
        for value in values:
            if not self.validate(value):
                return False
        return True
```

### Conflict Resolution: Base Class vs Interface

When a class inherits the same method signature from both a base class and an interface, Sharpy follows C# resolution rules:

**Rule: Base class takes precedence over interface default implementations.**

```python
interface IGreeter:
    def greet(self) -> str:
        return "Hello from interface"

class BaseGreeter:
    def greet(self) -> str:
        return "Hello from base class"

class MyGreeter(BaseGreeter, IGreeter):
    # No override needed - inherits from BaseGreeter
    pass

g = MyGreeter()
print(g.greet())  # "Hello from base class"
```

**Accessing Interface Implementation via Casting:**

To explicitly call the interface's default implementation, cast to the interface type:

```python
g = MyGreeter()

# Base class method
print(g.greet())                    # "Hello from base class"

# Attempt to call interface default (via cast)
greeter: IGreeter = g
print(greeter.greet())              # "Hello from base class" - still base class!

# To truly access interface default, must use explicit interface implementation
```

**Explicit Interface Implementation:**

When you need different behavior when accessed through the interface versus directly:

```python
class MyGreeter(BaseGreeter, IGreeter):
    # Regular method (used when called on MyGreeter)
    def greet(self) -> str:
        return "Hello from MyGreeter"

    # Explicit interface implementation (used when called through IGreeter)
    def IGreeter.greet(self) -> str:
        return "Hello from IGreeter implementation"

g = MyGreeter()
print(g.greet())                    # "Hello from MyGreeter"

igreeter: IGreeter = g
print(igreeter.greet())             # "Hello from IGreeter implementation"
```

### When to Use Interfaces vs Abstract Classes

With default implementations available in interfaces, the choice between interfaces and abstract classes may seem unclear. Here are the key distinctions:

| Feature | Interface | Abstract Class |
|---------|-----------|----------------|
| Fields (state) | ❌ Cannot have fields | ✅ Can have fields |
| Multiple inheritance | ✅ A class can implement multiple interfaces | ❌ A class can only extend one class |
| Constructors | ❌ No constructors | ✅ Can have constructors |
| Access modifiers on members | ❌ All members implicitly public | ✅ Can have protected/private members |
| Default implementations | ✅ Supported (C# 8.0+) | ✅ Supported |

**Guidelines:**

- **Use interfaces** when defining a contract ("what can this do?") without requiring shared state
- **Use abstract classes** when you need shared state (fields) or protected members across a family of related types
- **Use interfaces** when a type needs to satisfy multiple contracts
- **Use abstract classes** for "is-a" relationships with shared implementation

```python
# Interface: defines capability without state
interface ISerializable:
    def serialize(self) -> str: ...

# Abstract class: shared state and partial implementation
class Entity:
    id: int                    # Shared field
    created_at: datetime       # Shared field

    def __init__(self, id: int):
        self.id = id
        self.created_at = datetime.now()

    @abstract
    def validate(self) -> bool:
        ...                    # Subclasses must implement
```

**Multiple Interface Conflicts:**

When multiple interfaces provide defaults for the same method, the implementing class must provide its own implementation:

```python
interface IA:
    def method(self) -> str:
        return "A"

interface IB:
    def method(self) -> str:
        return "B"

class C(IA, IB):
    # ❌ ERROR if omitted: ambiguous default implementations
    # ✅ Must provide explicit implementation
    def method(self) -> str:
        return "C"
```

*Implementation: ✅ Native - Direct mapping to C# default interface methods (C# 8.0+) and explicit interface implementation.*

### Dunder Methods in Interfaces

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

**Standard Library Dunder Interfaces:**

| Interface | Dunders | Purpose |
|-----------|---------|---------|
| `IContextManager` | `__enter__`, `__exit__` | Context manager protocol |
| `IIterable[T]` | `__iter__` | Iteration protocol |
| `IIterator[T]` | `__next__` | Iterator protocol |
| `ISized` | `__len__` | Length protocol |
| `IContainer[T]` | `__contains__` | Membership protocol |
| `IHashable` | `__hash__`, `__eq__` | Hashable protocol |
| `IIndexable[K, V]` | `__getitem__`, `__setitem__` | Indexing protocol |
| `IComparable[T]` | `__lt__`, `__le__`, `__gt__`, `__ge__` | Ordering protocol |

*Implementation: Compiler validates that dunder declarations only appear in whitelisted standard library interfaces.*

---
