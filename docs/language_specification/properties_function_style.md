# Function-Style Properties

For properties requiring custom logic (validation, transformation, computation), use function-style syntax.

### Mixed Auto+Custom Properties

You can combine an auto-property declaration with a custom getter, setter, or both. The auto-property declaration defines the type and a backing field (`_name`), while the custom accessor provides the logic:

```python
# Auto-getter generated, custom setter validates
property name: str
property set name(self, value: str):
    if len(value) > 0:
        self._name = value
```

**Semantics:**
- `property name: str` defines the backing field (`_name`) and type
- `property set name(self, value: str):` replaces the auto-setter
- `property get name(self) -> str:` replaces the auto-getter
- If both custom get and set are provided alongside an auto-declaration, the auto-declaration defines the backing field only (no auto-accessors generated)

For fully custom properties with no auto-declaration, provide your own backing field or compute the value.

## Function-Style Getter

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

    property get perimeter(self) -> float:
        return 2 * (self.width + self.height)

    property get is_square(self) -> bool:
        return self.width == self.height

    # Multi-statement bodies work naturally
    property get diagonal(self) -> float:
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
public float Area => Width * Height;
public float Perimeter => 2 * (Width + Height);
public bool IsSquare => Width == Height;

public float Diagonal {
    get {
        var wSq = Width * Width;
        var hSq = Height * Height;
        return Math.Sqrt(wSq + hSq);
    }
}
```

## Function-Style Setter

```python
class Temperature:
    _celsius: float = 0.0

    # Function-style setter with validation
    property set celsius(self, value: float):
        if value < -273.15:
            raise ValueError("Temperature below absolute zero")
        self._celsius = value

    # Cannot combine with auto getter
    property get celsius: float # ERROR: no auto backing field with function-style!
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

## Module-Level Properties

Properties can be declared at module level (outside any class). This allows modules to expose computed values that look like attributes — for example, `os.environ` computes a fresh `dict[str, str]` on each access:

```python
# os.spy (module level, no enclosing class)

# Function-style getter — computed on each access
property get environ() -> dict[str, str]:
    result: dict[str, str] = {}
    # ... populate from process environment ...
    return result

# Getter/setter pair sharing a module-level backing variable
_debug_mode: bool = False

property get debug_mode() -> bool:
    return _debug_mode

property set debug_mode(value: bool):
    _debug_mode = value

# Auto-property form — backing field and accessors generated
property default_timeout: float = 30.0
```

```python
# Usage from another module
import os

print(os.environ["PATH"])
os.debug_mode = True
```

**Semantics:**

- Module-level properties take **no `self` parameter** — they are implicitly static. Writing `self` in a module-level accessor is an error (there is no instance).
- The `@static` decorator is neither required nor allowed; module level implies static.
- **Auto-property form**: `property name: T = default` generates a backing field and both accessors, exactly like the class-level auto-property form.
- **Split accessors are merged**: a separate `property get name() ...` and `property set name(value: T):` for the same name combine into a single property, following the same rules as class-level split accessors (type must match across accessors; a get-only property is read-only).
- Access modifiers (`@private`, `@internal`, ...) apply per accessor, as at class level.

**Code generation:** Module-level properties are emitted as `public static` properties on the module's static class (`Exports`/`Program`), alongside module-level functions and variables:

*Implementation: ✅ Native*
```csharp
public static partial class Exports
{
    public static Sharpy.Dict<string, string> Environ
    {
        get
        {
            // ... computed on each access ...
        }
    }

    public static bool DebugMode
    {
        get => _debugMode;
        set => _debugMode = value;
    }

    public static double DefaultTimeout { get; set; } = 30.0;
}
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

## See Also

- [Properties](properties.md) - Overview of auto-properties and property forms
- [Properties - Inheritance](properties_inheritance.md) - Virtual, abstract, and override properties
- [Decorators](decorators.md) - Access modifiers and other decorators
- [Classes](classes.md) - Class definition basics
