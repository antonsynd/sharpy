# Function-Style Properties

For properties requiring custom logic (validation, transformation, computation), use function-style syntax.

### Mixed Auto+Custom Properties

You can combine an auto-property declaration with a custom getter, setter, or both. The auto-property declaration defines the type and a backing field (`_name`), while the custom accessor provides the logic:

```python
class User:
    # Auto-getter generated, custom setter validates
    property name: str = ""
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
    # Function-style setter with validation
    property set celsius(self, value: float):
        if value < -273.15:
            raise ValueError("Temperature below absolute zero")
        self._celsius = value

    # The auto half supplies the storage (`_celsius`) — do NOT also declare it
    property celsius: float = 0.0
```

It is possible to have both a function-style getter and setter. A function-style accessor may also be paired with an **auto-property of the same name** supplying the complementary half: the auto half declares the type and the default, and it synthesizes the backing field, which the function-style accessors reach by its underscored snake_case spelling (`self._celsius` for `property celsius`).

Because the auto half owns that field, declaring a field whose name mangles to the same C# identifier is refused (code SPY0407) rather than emitting a duplicate member. Drop the declaration and move its default onto the property.

Split auto halves (`property get value: int` beside `@private property set value: int`) are one auto property, and a C# auto property has one initializer, so two auto halves of one property carry at most one default. A second default is refused (SPY0407) at the later half:

<!-- spec-sweep: error SPY0407 -->
```python
class Dial:
    property get value: int = 5
    @private
    property set value: int = 6    # ERROR SPY0407: two auto halves carry a default; keep one

def main() -> None:
    print(Dial().value)
```

**In an interface body** the auto half is abstract (it declares no storage), so it may be paired only with a stub accessor (`...`) or with a body for the *same* accessor. An abstract half beside a body for the complementary accessor is refused (SPY0617) at the later half, in either order: C# interface accessors are all abstract or all default implementations.

<!-- spec-sweep: error SPY0617 -->
```python
interface IGauge:
    property get level: int

    property set level(self, value: int):   # ERROR SPY0617: the getter is abstract, this setter has a body
        print(value)

def main() -> None:
    print("unreachable")
```

With a stub setter both accessors are abstract, and the implementing class supplies them:

<!-- spec-sweep: output -->
```python
interface IGauge:
    property get level: int                     # abstract getter
    property set level(self, value: int): ...   # abstract setter (a stub)

class Gauge(IGauge):
    property level: int = 3

def main() -> None:
    g: IGauge = Gauge()
    g.level = 7
    print(g.level)        # 7
```

**Important:** Function-style accessors on their own do **not** generate a backing field. Either pair them with an auto-property of the same name, as above, or provide your own storage under a different name.

The auto half's declared accessor is honored: `property get celsius: float` beside a function-style *getter* is a get-only property, and gains no setter.

**Type Consistency:** The type must be the same across all accessors (get/set/init) for a property.

**No Function-Style `init`:** There is no `property init name(self, value: T):` form because init-only semantics require compiler support for constructor-only assignment, which doesn't compose well with user-defined logic.

## Accessor Parameter Lists

A property accessor's parameter list must be expressible as a C# accessor's: `self`, plus exactly
one value parameter for a setter or `init` accessor and none for a getter. That is the whole shape,
and it is fixed by construction — a setter receives the single value C# hands it, and a getter
receives nothing.

The setter's value parameter may be named anything; the name is *mapped* onto C#'s implicit `value`,
not declared as a local:

```python
class Box:
    _value: int = 0

    # ✅ Valid - `v` denotes the incoming value
    property set value(self, v: int):
        self._value = v
```

A setter also returns nothing to C#, so its return annotation, when written, is `None` — the
accessor's own return, never the property's type. The property's type is always the value
parameter's, so this set-only property is an `int` property:

<!-- spec-sweep: output "5" -->
```python
class Gauge:
    # `-> None` is the setter's own return; the property's type is `v`'s: int
    property set level(self, v: int) -> None:
        print(v)

def main() -> None:
    g = Gauge()
    g.level = 5
```

Three shapes leave that constraint, and each is refused with **SPY0496**:

<!-- spec-sweep: error SPY0496 -->
```python
class Broken:
    # ❌ A getter receives no value, so `factor` has nothing to denote —
    #    write a method if it needs an argument
    property get scaled(self, factor: int) -> int:
        return factor

    # ❌ An accessor has no argument list to vary
    property set samples(self, *values: int):
        print(len(values))

    # ❌ A setter returns nothing to C# — its return annotation is `None` or nothing
    property set level(self, v: int) -> int:
        print(v)
```

The rule applies wherever a property is declared: in a class, a struct, an interface, a nested type,
and at [module level](#module-level-properties).

See [Function Variadic Arguments](function_variadic_arguments.md#not-in-a-property-or-event-accessor).

## Mixed Access Modifiers

Getters and setters can have different visibility:

<!-- spec-sweep: output -->
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

def main() -> None:
    c = Counter()
    print(c.value)    # 0
    c.increment()     # the public method modifies it through the private setter
    print(c.value)    # 1
```

Outside the class the setter is not accessible:

<!-- spec-sweep: fragment -->
```python
c.value = 10      # ❌ ERROR: the setter of 'value' is private to Counter
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

<!-- spec-sweep: output -->
```python
class AppConfig:
    @static
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

def main() -> None:
    print(AppConfig.version)           # 1.0.0
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

<!-- spec-sweep: fragment -->
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
- **A module property is a module binding**: its name has one meaning in the module. A module variable of the same name is refused (`SPY0204`, at the variable) in either source order, as a variable beside a def is.

<!-- spec-sweep: error SPY0204 -->
```python
property size: int = 0
size: int = 4             # ERROR SPY0204: Cannot bind 'size': it is already defined as a module property in this scope

def main() -> None:
    print(size)
```

**Code generation:** Module-level properties are emitted as `public static` properties on the module's members class (`<Stem>Module` inside the module's namespace — `OsModule` for `os.spy`; see `module_system.md`), alongside module-level functions and variables:

*Implementation: ✅ Native*
```csharp
public static partial class OsModule
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

## One Name, One Meaning in a Type Body

In a class, struct or interface body a name has one meaning. A second declaration of the name is refused (`SPY0204`) whatever the two kinds are (method, field, `const`, property, event, nested type or `type` alias), in either order. The diagnostic is at the later declaration and names the kind and line of the earlier one. Python rebinds the name to whichever definition runs last. Sharpy refuses, because one name in a .NET type names one member, method overloads apart (deviation `module-binding-collision-refused`).

The only declarations that share a name are the parts of one member:

- a function-style getter and a function-style setter of one property (`property get` + `property set`);
- an auto-property beside a function-style accessor that supplies a complementary half (see [Function-Style Setter](#function-style-setter));
- split auto halves, such as `property get name: str` beside `@private property set name: str` (see [Mixed Access Modifiers](#mixed-access-modifiers));
- methods with different signatures, which are overloads (see [Method Overloading](method_overloading.md)).

A part declared twice, such as two function-style getters, two function-style setters or two auto halves that declare the same accessor (one auto-property written twice), is refused like any other pair. An auto half beside a function-style accessor of the same kind is not a second part: the function-style accessor supplies that half's body (`property get celsius: float` beside a function-style getter is the get-only form above).

An explicit interface implementation (`property IShape.area`) claims no bare name. C# reaches it only through the interface, so it is not a member of the type's own namespace: it may stand beside a field, method, property or event named `area`, and the two are separate C# members. It is refused only beside another `IShape.area` (SPY0204). Explicit members of two interfaces (`IA.area` and `IB.area`) are two members as well.

<!-- spec-sweep: output -->
```python
interface IShape:
    property area: int

class Square(IShape):
    property area: int = 3            # the class's own member
    property IShape.area: int = 4     # IShape's member, reached through the interface

class Tile(IShape):
    def area(self) -> int:            # a method of the same name
        return 9

    property IShape.area: int = 5

def main() -> None:
    sq = Square()
    s: IShape = sq
    print(sq.area)        # 3
    print(s.area)         # 4
    t = Tile()
    s = t
    print(t.area())       # 9
    print(s.area)         # 5
```

<!-- spec-sweep: error SPY0204 -->
```python
interface IShape:
    property area: int

class Square(IShape):
    property IShape.area: int = 4
    property IShape.area: int = 5     # ERROR SPY0204: 'area' is already declared in 'Square' as the explicit implementation of 'IShape.area' (line 5)

def main() -> None:
    print("unreachable")
```

<!-- spec-sweep: error SPY0204 -->
```python
class Example:
    _value: int = 0

    # ✅ OK - property
    property get value(self) -> int:
        return self._value

    # ❌ ERROR - method cannot have same name as property
    def value(self) -> int:
        return self._value

def main() -> None:
    print(Example().value)
```

**Compiler Error:**

```
error[SPY0204]: 'value' is already declared in 'Example' as a property (line 5); one name has one meaning in a type body
  --> example.spy:9:9
    |
  9 |     def value(self) -> int:
    |         ^
    |
```

The rule does not depend on a method being one of the two. A property and an event of one name are refused the same way:

<!-- spec-sweep: error SPY0204 -->
```python
delegate Handler(value: int) -> None

class Sensor:
    property changed: int = 0
    event changed: Handler    # ERROR SPY0204: 'changed' is already declared in 'Sensor' as a property (line 4); one name has one meaning in a type body

def main() -> None:
    print(Sensor().changed)
```

An auto-property and a function-style setter of the same name are one property. The auto half gives the type, the default and the storage, and the function-style half replaces its setter:

<!-- spec-sweep: output -->
```python
class Counter:
    property count: int = 0                # auto half: type, default and storage

    property set count(self, value: int):  # function-style half: the setter
        self._count = max(value, 0)

def main() -> None:
    c = Counter()
    c.count = 5
    print(c.count)        # 5
    c.count = -3
    print(c.count)        # 0
```

## See Also

- [Properties](properties.md) - Overview of auto-properties and property forms
- [Properties - Inheritance](properties_inheritance.md) - Virtual, abstract, and override properties
- [Decorators](decorators.md) - Access modifiers and other decorators
- [Classes](classes.md) - Class definition basics
