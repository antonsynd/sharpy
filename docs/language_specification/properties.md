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

For properties requiring custom logic (validation, transformation, computation), use function-style syntax. The user must provide their own backing field or compute the value. You cannot combine get/set/init auto-properties with custom logic get/set/init, since the backing field for the auto-property cannot be accessed from the custom logic.

### Function-Style Getter

```python
class Rectangle:
    width: double
    height: double

    def __init__(self, width: double, height: double):
        self.width = width
        self.height = height

    # Computed property (no backing field needed)
    property get area(self) -> double:
        return self.width * self.height

    property get perimeter(self) -> double:
        return 2 * (self.width + self.height)

    property get is_square(self) -> bool:
        return self.width == self.height

    # Multi-statement bodies work naturally
    property get diagonal(self) -> double:
        w_sq = self.width ** 2
        h_sq = self.height ** 2
        return (w_sq + h_sq) ** 0.5

    # Can reference other properties
    property get description(self) -> str:
        shape = "square" if self.is_square else "rectangle"
        return f"A {shape} with area {self.area}"
```

*Implementation: ✅ Native*
```csharp
public double Area => Width * Height;
public double Perimeter => 2 * (Width + Height);
public bool IsSquare => Width == Height;

public double Diagonal {
    get {
        var wSq = Width * Width;
        var hSq = Height * Height;
        return Math.Sqrt(wSq + hSq);
    }
}
```

### Function-Style Setter

```python
class Temperature:
    _celsius: double = 0.0

    # Function-style setter with validation
    property set celsius(self, value: double):
        if value < -273.15:
            raise ValueError("Temperature below absolute zero")
        self._celsius = value

    # Cannot combine with auto getter
    property get celsius: double # ERROR: no auto backing field with function-style!
```

It is possible to have both a function-style getter and setter. However, function-style getter/setters cannot coexist with an auto-property for the same property name since there is no way to retrieve the backing field.

**Important:** Function-style accessors do **not** generate a backing field. You must provide your own storage.

**Type Consistency:** The type must be the same across all accessors (get/set/init) for a property.

**No Function-Style `init`:** There is no `property init name(self, value: T):` form because init-only semantics require compiler support for constructor-only assignment, which doesn't compose well with user-defined logic.

## Mixed Access Modifiers

Getters and setters can have different visibility:

```python
class Counter:
    _value: int = 0

    # Public getter
    property get value(self) -> int:
        return self._value

    # Private setter (only accessible within the class)
    @private
    property set value(self, v: int):
        self._value = v

    # Public methods can use the private setter
    def increment(self):
        self.value += 1

    def reset(self):
        self.value = 0

# Usage
c = Counter()
print(c.value)    # OK: public getter
c.increment()     # OK: internal modification via public method
c.value = 10      # ERROR: setter is private
```

**With Auto-Properties:**

```python
class User:
    property get name: str           # Public getter
    @private
    property set name: str           # Private setter

    def __init__(self, name: str):
        self.name = name             # OK: inside class
```

**Common Access Patterns:**

| Pattern | Getter | Setter | Use Case |
|---------|--------|--------|----------|
| Read-write | (default) | (default) | Mutable public state |
| Read-only | (default) | (none) | Computed or immutable |
| Observable | (default) | `@private` | External read, internal write |
| Protected write | (default) | `@protected` | Subclass modification |
| Internal write | (default) | `@internal` | Assembly-internal modification |

*Implementation: ✅ Native*
```csharp
public int Value {
    get => _value;
    private set => _value = value;
}
```

## Static Properties

Use `@static` decorator for class-level properties. Static properties take no `self` parameter:

```python
class AppConfig:
    _debug_mode: bool = False
    _instance_count: int = 0

    # Static auto-properties
    @static
    property version: str = "1.0.0"

    @static
    property get build_number: int = 42

    # Static function-style getter (no self parameter)
    @static
    property get is_debug_enabled() -> bool:
        return AppConfig._debug_mode

    # Static function-style setter (no self parameter)
    @static
    property set debug_mode(value: bool):
        AppConfig._debug_mode = value

    @static
    property get debug_mode() -> bool:
        return AppConfig._debug_mode

# Usage
print(AppConfig.version)           # "1.0.0"
AppConfig.debug_mode = True
print(AppConfig.is_debug_enabled)  # True
```

**Static Property Rules:**
- Auto: Use `@static` decorator with `property [get|set|init] name: T`
- Function-style: Use `@static` decorator with `property get name() -> T:` or `property set name(value: T):` (no `self`)
- Access the class by name within the body

*Implementation: ✅ Native*
```csharp
public static string Version { get; set; } = "1.0.0";
public static int BuildNumber { get; } = 42;
public static bool IsDebugEnabled => _debugMode;
public static bool DebugMode {
    get => _debugMode;
    set => _debugMode = value;
}
```

## Virtual, Abstract, and Override Properties

Properties participate in inheritance using the standard decorators:

```python
class Shape:
    # Abstract property (must be overridden)
    @abstract
    property get area(self) -> double:
        ...

    # Virtual property (can be overridden)
    @virtual
    property get name(self) -> str:
        return "Shape"

class Circle(Shape):
    property get radius: double

    def __init__(self, radius: double):
        self.radius = radius

    # Override abstract property
    @override
    property get area(self) -> double:
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

**Inheritance Rules:**
- `@abstract` properties must use `...` as the body and must be overridden
- `@virtual` properties can optionally be overridden by subclasses
- `@override` is required when overriding a base class property
- `@final` prevents further overriding in subclasses
- A subclass can override any accessor it has visibility to
- The overriding accessor's visibility cannot be more restrictive than the base

**Covariant Return Types:**

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

*Implementation: ✅ Native*
```csharp
public abstract double Area { get; }
public virtual string Name => "Shape";

public override double Area => 3.14159 * Radius * Radius;
public override string Name => "Circle";

public sealed override string Name => "Unit Circle";
```

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

**Interface Property Requirements:**

| Interface Declares | Implementer Must Provide |
|--------------------|--------------------------|
| `property get x: T` | At least a getter |
| `property set x: T` | At least a setter |
| `property x: T` | Both getter and setter |
| `property get x(self) -> T: ...` | A getter (auto or function-style) |
| `property set x(self, value: T): ...` | A setter (auto or function-style) |

**Auto-Properties in Interfaces:**

For interface auto-properties, no body means abstract (must be implemented). A default value makes it optional:

```python
interface IIdentifiable:
    property get id: int       # Abstract - implementer must provide getter

interface IConfigurable:
    property name: str = ""    # Default value - implementer can override or use default
    property enabled: bool = True
```

This matches C# interface property semantics where properties without a body are abstract requirements.

**Explicit Interface Implementation:**

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

## Property and Method Name Conflicts

A property and a method cannot share the same name within a class:

```python
class Example:
    _value: int = 0

    # ✅ OK - property
    property get value(self) -> int:
        return self._value

    # ❌ ERROR - method cannot have same name as property
    def value(self) -> int:
        return self._value
```

**Compiler Error:**

```
error: 'value' is already defined as a property in this class
  --> example.spy:10:5
   |
10 |     def value(self) -> int:
   |         ^^^^^ method name conflicts with property on line 6
```

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
