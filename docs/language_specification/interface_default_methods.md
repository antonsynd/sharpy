# Interface Default Methods

Interfaces can provide default implementations for methods. Implementing types inherit the default unless they provide their own implementation.

## Basic Default Implementations

```python
interface ILogger:
    def log(self, message: str):
        """Log a message. Must be implemented."""
        ...

    def log_info(self, message: str):
        """Log an info message. Has default implementation."""
        self.log(f"[INFO] {message}")

    def log_error(self, message: str):
        """Log an error message. Has default implementation."""
        self.log(f"[ERROR] {message}")

class ConsoleLogger(ILogger):
    # Must implement abstract method
    def log(self, message: str):
        print(message)

    # Inherits log_info and log_error defaults
    # Can optionally override them

class FileLogger(ILogger):
    path: str

    def __init__(self, path: str):
        self.path = path

    def log(self, message: str):
        # Write to file
        pass

    # Override default to add timestamp
    def log_error(self, message: str):
        self.log(f"[ERROR {datetime.now()}] {message}")
```

## Calling Other Interface Methods

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

## Conflict Resolution: Base Class vs Interface

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

## Multiple Interface Conflicts

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

## When to Use Interfaces vs Abstract Classes

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

*Implementation*
- *✅ Native - Direct mapping to C# default interface methods (C# 8.0+) and explicit interface implementation.*

## See Also

- [Interfaces](interfaces.md) - Interface basics, generic interfaces, and inheritance
- [Inheritance](inheritance.md) - Class inheritance and `super()`
- [Decorators](decorators.md) - `@abstract`, `@virtual`, `@override`
