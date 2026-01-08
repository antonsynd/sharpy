# Properties - Inheritance

Properties participate in inheritance using the standard decorators: `@virtual`, `@abstract`, `@override`, and `@final`.

## Virtual, Abstract, and Override Properties

```python
class Shape:
    # Abstract property (must be overridden)
    @abstract
    property get area(self) -> float:
        ...

    # Virtual property (can be overridden)
    @virtual
    property get name(self) -> str:
        return "Shape"

class Circle(Shape):
    property get radius: float

    def __init__(self, radius: float):
        self.radius = radius

    # Override abstract property
    @override
    property get area(self) -> float:
        return 3.14159 * self.radius ** 2

    # Override virtual property
    @override
    property get name(self) -> str:
        return "Circle"

@final
class UnitCircle(Circle):
    def __init__(self):
        super().__init__(1.0)

    # Sealed override - cannot be overridden in further subclasses
    @final
    @override
    property get name(self) -> str:
        return "Unit Circle"
```

## Inheritance Rules

- `@abstract` properties must use `...` as the body and must be overridden
- `@virtual` properties can optionally be overridden by subclasses
- `@override` is required when overriding a base class property
- `@final` prevents further overriding in subclasses
- A subclass can override any accessor it has visibility to
- The overriding accessor's visibility cannot be more restrictive than the base

*Implementation: ✅ Native*
```csharp
public abstract float Area { get; }
public virtual string Name => "Shape";

public override float Area => 3.14159 * Radius * Radius;
public override string Name => "Circle";

public sealed override string Name => "Unit Circle";
```

## Covariant Return Types

C# 9.0 supports covariant return types for method overrides. Since properties are essentially methods, property return types can be covariant on override:

```python
class Animal:
    @virtual
    property get friend(self) -> Animal:
        return self._friend

class Dog(Animal):
    @override
    property get friend(self) -> Dog:  # Valid - Dog is subtype of Animal
        return self._dog_friend

class Cat(Animal):
    @override
    property get friend(self) -> Cat:  # Valid - Cat is subtype of Animal
        return self._cat_friend
```

This allows subclasses to return more specific types without requiring unsafe casts at call sites.

## Interface Properties

Interfaces declare property requirements using the same syntax:

```python
interface IIdentifiable:
    # Read-only property requirement (getter only)
    property get id: int

interface INamed:
    # Read-write property requirement (getter + setter)
    property name: str

interface ITimestamped:
    # Function-style requirement (read-only computed)
    property get created_at(self) -> datetime: ...
    property get updated_at(self) -> datetime: ...

class Entity(IIdentifiable, INamed, ITimestamped):
    property get id: int
    property name: str = "Unnamed"
    _created: datetime
    _updated: datetime

    def __init__(self, id: int):
        self.id = id
        self._created = datetime.now()
        self._updated = self._created

    property get created_at(self) -> datetime:
        return self._created

    property get updated_at(self) -> datetime:
        return self._updated
```

## Interface Property Requirements

| Interface Declares | Implementer Must Provide |
|--------------------|--------------------------|
| `property get x: T` | At least a getter |
| `property set x: T` | At least a setter |
| `property x: T` | Both getter and setter |
| `property get x(self) -> T: ...` | A getter (auto or function-style) |
| `property set x(self, value: T): ...` | A setter (auto or function-style) |

## Auto-Properties in Interfaces

For interface auto-properties, no body means abstract (must be implemented). A default value makes it optional:

```python
interface IIdentifiable:
    property get id: int       # Abstract - implementer must provide getter

interface IConfigurable:
    property name: str = ""    # Default value - implementer can override or use default
    property enabled: bool = True
```

This matches C# interface property semantics where properties without a body are abstract requirements.

## Explicit Interface Implementation

When a class needs to provide different behavior when accessed through an interface versus directly:

```python
interface ISecret:
    property get value: str

class SecretHolder(ISecret):
    _secret: str

    def __init__(self, secret: str):
        self._secret = secret

    # Regular property (always accessible)
    property get hint(self) -> str:
        return self._secret[0] + "***"

    # Explicit interface implementation
    # Only accessible when referenced through the interface type
    property get ISecret.value(self) -> str:
        return self._secret

# Usage
holder = SecretHolder("password123")
print(holder.hint)        # "p***"
print(holder.value)       # ERROR: 'value' not accessible on SecretHolder

secret: ISecret = holder
print(secret.value)       # "password123" - accessible via interface
```

*Implementation: ✅ Native*
```csharp
public string Hint => _secret[0] + "***";
string ISecret.Value => _secret;
```

## See Also

- [Properties](properties.md) - Overview of auto-properties
- [Properties - Function Style](properties_function_style.md) - Custom property logic
- [Interfaces](interfaces.md) - Interface definition and implementation
- [Inheritance](inheritance.md) - Class inheritance and super()
- [Decorators](decorators.md) - @virtual, @abstract, @override, @final
