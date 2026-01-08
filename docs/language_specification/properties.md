# Properties

Properties provide controlled access to object state with support for computed values, validation, and fine-grained access control. Sharpy properties map cleanly to C# properties while maintaining Pythonic readability.

## Property Forms

Sharpy supports two property forms:

| Form | Use Case | Syntax Pattern |
|------|----------|----------------|
| Auto-property | Simple storage with compiler-generated backing field | `property [get\|set\|init]? name: T [= value]` |
| Function-style property | Custom logic, user-provided backing field | `property (get\|set) name(self, ...) -> T:` |

**Key Distinction:**
- **Auto-properties** generate a backing field automatically (opaque to the user)
- **Function-style properties** require the user to provide their own backing field (or compute the value)

## Auto-Properties

Auto-properties generate a backing field and accessors automatically:

```python
class Person:
    # Read-write (default, has both get and set)
    property name: str = "Unknown"
    property age: int              # Zero-initialized (value type)

    # Read-only getter (must have default value OR be set in constructor)
    property get id: int = 0
    property get uuid: str         # Must be set in __init__

    # Init-only (readable, but can only be set at declaration or in constructor)
    property init created_at: datetime   # Must be set in __init__
    property init email: str = "unknown@example.com"

    # Write-only (rare, typically combined with public getter)
    property set password_hash: str

    def __init__(self, name: str, age: int, id: int, uuid: str, email: str, password: str):
        self.name = name
        self.age = age
        self.id = id             # OK: can set read-only in constructor
        self.uuid = uuid         # Required: no default value
        self.created_at = datetime.now()  # Required: init property, no default
        self.email = email       # OK: overrides default
        self.password_hash = hash_password(password)

# After construction:
p = Person("Alice", 30, 1, "abc-123", "alice@example.com", "secret")
p.name = "Bob"           # OK: read-write
p.id = 2                 # ERROR: read-only property (no setter)
p.email = "new@test.com" # ERROR: init-only, cannot set after construction
print(p.password_hash)   # ERROR: write-only property (no getter)
```

**Auto-Property Modifiers:**

The auto-property modifiers (or lack thereof) are mutually exclusive; for a given property
named X, only one of the following are possible within a given class/struct/interface.

| Syntax | Accessors | Readable | Settable in `__init__` | Settable after |
|--------|-----------|----------|------------------------|----------------|
| `property name: T` | get + set | ✅ | ✅ | ✅ |
| `property get name: T` | get only | ✅ | ✅ | ❌ |
| `property set name: T` | set only | ❌ | ✅ | ✅ |
| `property init name: T` | get + init | ✅ | ✅ | ❌ |

**Difference between `property get` and `property init`:**
- `property get name: T` — getter-only; can have a default value or be set in constructor, then immutable
- `property init name: T` — getter + init-only setter; **must** be set at declaration or in every constructor (no zero-initialization); immutable after construction

**Auto-Property Initialization Rules:**

| Modifier | Default Value | Zero-Init (value types) | Must set in `__init__` |
|----------|---------------|-------------------------|------------------------|
| `property` | Optional | ✅ Yes | If no default (ref types) |
| `property get` | Optional | ✅ Yes | If no default (ref types) |
| `property set` | Optional | ✅ Yes | No |
| `property init` | Optional | ❌ No | If no default |

```python
class Example:
    property name: str           # Must be assigned in __init__ (reference type)
    property count: int          # Zero-initialized to 0 (value type)
    property label: str = ""     # Default value provided
    property get id: int = 0     # Read-only with default
    property init token: str     # MUST be set in __init__ (no zero-init allowed)

    def __init__(self, name: str, token: str):
        self.name = name         # Required: no default for reference type
        self.token = token       # Required: init property without default
        # self.count not assigned - will be 0 (value type default)
```

*Implementation: ✅ Native*
```csharp
public string Name { get; set; }
public int Count { get; set; }
public string Label { get; set; } = "";
public int Id { get; } = 0;
public string Token { get; init; }
```

## Function-Style Properties

For properties requiring custom logic (validation, transformation, computation), use function-style syntax with custom backing fields or computed values.

```python
class Rectangle:
    width: float
    height: float

    def __init__(self, width: float, height: float):
        self.width = width
        self.height = height

    # Computed property (no backing field needed)
    property get area(self) -> float:
        return self.width * self.height
```

**Key Points:**
- Custom logic for validation, transformation, or computation
- You must provide your own backing field (auto-properties don't work with function-style)
- Can have both getter and setter with different access modifiers
- Static properties use `@static` decorator with no `self` parameter

For complete details on function-style properties, static properties, mixed access modifiers, and naming conflicts, see [Properties - Function Style](properties_function_style.md).

## Inheritance and Interfaces

Properties participate in inheritance using `@virtual`, `@abstract`, `@override`, and `@final` decorators:

```python
class Shape:
    @abstract
    property get area(self) -> float:
        ...

    @virtual
    property get name(self) -> str:
        return "Shape"

class Circle(Shape):
    @override
    property get area(self) -> float:
        return 3.14159 * self.radius ** 2
```

**Key Points:**
- Abstract properties must be overridden
- Virtual properties can optionally be overridden
- Override decorator is required
- Supports covariant return types (C# 9.0+)
- Interfaces can declare property requirements

For complete details on virtual, abstract, and override properties, covariant return types, interface properties, and explicit interface implementation, see [Properties - Inheritance](properties_inheritance.md).

## Property Syntax Summary

**Auto-properties (compiler-generated backing field):**

| Syntax | Accessors | C# Equivalent |
|--------|-----------|---------------|
| `property name: T` | get + set | `T Name { get; set; }` |
| `property name: T = val` | get + set | `T Name { get; set; } = val` |
| `property get name: T` | get | `T Name { get; }` |
| `property get name: T = val` | get | `T Name { get; } = val` |
| `property set name: T` | set | `T Name { set; }` |
| `property init name: T` | get + init | `T Name { get; init; }` |
| `property init name: T = val` | get + init | `T Name { get; init; } = val` |

**Function-style properties (user-provided backing field or computed):**

| Syntax | C# Equivalent |
|--------|---------------|
| `property get name(self) -> T:` | `T Name { get { ... } }` |
| `property set name(self, value: T):` | `T Name { set { ... } }` |
| `@static property get name() -> T:` | `static T Name { get { ... } }` |
| `@static property set name(value: T):` | `static T Name { set { ... } }` |
| `property get IFace.name(self) -> T:` | `T IFace.Name { get { ... } }` |

**Valid accessor combinations:**

| Accessors | Result | Readable | Writable in `__init__` | Writable after |
|-----------|--------|----------|------------------------|----------------|
| get | Read-only | ✅ | ✅ (auto) / ❌ (func) | ❌ |
| set | Write-only | ❌ | ✅ | ✅ |
| get + set | Read-write | ✅ | ✅ | ✅ |
| init | Init-only (auto only) | ✅ | ✅ | ❌ |
| get + init | Read + init (auto only) | ✅ | ✅ | ❌ |

**Decorator placement:**

```python
@static
@virtual
property get name(self) -> str:
    return "value"

@override
property get name(self) -> str:
    return self._name

@private
property set name(self, value: str):
    self._name = value
```

## See Also

- [Properties - Function Style](properties_function_style.md) - Custom property logic, validation, static properties, and access modifiers
- [Properties - Inheritance](properties_inheritance.md) - Virtual, abstract, override properties, interfaces, and explicit interface implementation
- [Classes](classes.md) - Class definition basics
- [Decorators](decorators.md) - Access modifiers and other decorators
- [Interfaces](interfaces.md) - Interface definition and requirements
