# Function Types

Function types represent the signature of callable values, including lambdas, method references, and delegate instances. They are used in type annotations for parameters, return types, fields, and type aliases.

## Syntax

The function type syntax uses an arrow notation:

```
(ParamType1, ParamType2, ...) -> ReturnType
```

## Examples

```python
# No parameters, returns int
counter: () -> int

# Single parameter
processor: (str) -> int

# Multiple parameters
calculator: (int, int) -> int

# Returns None (void function)
callback: (str) -> None

# Nullable function type
handler: ((Event) -> None)?

# Function returning a function
factory: (str) -> ((int) -> bool)

# Generic function types (in type aliases)
type Callback[T] = (T) -> None
type Predicate[T] = (T) -> bool
type Transform[T, U] = (T) -> U
```

## Parameter Names

> **Not yet implemented.** The parser currently only supports unnamed parameter types in function type annotations (e.g., `(int, str) -> bool`). The named parameter syntax shown below (e.g., `(count: int, message: str) -> bool`) is planned but not yet supported. Attempting to use named parameters in function type annotations will result in a parse error.

Parameter names are **optional** in function type annotations. When provided, they serve as documentation only and are not part of the type signature:

```python
# Without parameter names (currently the only supported form)
handler: (int, str) -> bool

# With parameter names (for documentation) — NOT YET IMPLEMENTED
handler: (count: int, message: str) -> bool

# Both forms are equivalent types
# The names do not affect type compatibility
```

**Note:** Parameter names in function types do not create named parameter requirements at call sites. They are purely for readability and documentation.

```python
# NOT YET IMPLEMENTED — named parameters in function type aliases
type EventHandler = (sender: object, args: EventArgs) -> None

# All of these work - names are not enforced
def my_handler(s: object, a: EventArgs) -> None:
    pass

def another_handler(obj: object, event_args: EventArgs) -> None:
    pass

h: EventHandler = my_handler      # OK
h = another_handler               # OK
```

## Function Types with None Return Type

Function types that indicate a function with no return value, i.e. `-> None`
must have the return type annotation `-> None` indicated. While it is true
that function definitions may omit this return type annotation if it is
`-> None`, function types of this sort on the other hand require it
for parsing/syntactic reasons.

```python
type SomeFuncType = (int, str) -> None  # OK
type AnotherFuncType = (int, str)       # ERROR
```

## No Optional Parameters in Function Type Annotations

Function type **annotations** cannot specify optional parameters (parameters with default values). All parameters in a function type annotation are required. Note that lambda expressions *can* have default parameters — this restriction applies only to the type annotation syntax:

```python
# ❌ Invalid - cannot specify defaults in function type annotations
type BadCallback = (x: int, y: int = 0) -> int

# ✅ Valid - all parameters required
type GoodCallback = (int, int) -> int

# To accept functions with optional params, use the required-only signature
def process(callback: (int) -> int) -> int:
    return callback(42)

# Functions with more parameters than required cannot be assigned
def add(x: int, y: int = 0) -> int:
    return x + y

process(add)  # ERROR: (int, int) -> int is not assignable to (int) -> int

# But you can wrap them in a lambda
process(lambda x: add(x))  # OK
```

**Rationale:** Function type annotations describe a calling convention -- what the caller must provide. Since the caller cannot know about default values, function type annotations represent the minimal required signature. This aligns with C# delegate semantics where all parameters are required.

> **Note:** This restriction applies to the *type annotation syntax* `(int, int) -> int`, not to lambda definitions themselves. Lambdas can have default parameter values in their definitions -- see [Lambda Expressions](lambdas.md).

## Function Type Compatibility

A function type `A` is assignable to function type `B` if:
1. They have the same number of parameters
2. Parameter types are compatible in **either direction** (A's param assignable to B's, or B's param assignable to A's)
3. Return types are covariant (A's return type assignable to B's)

> **Design note:** Parameter compatibility uses bidirectional assignability rather than strict contravariance. This is a deliberate choice that simplifies common callback patterns while remaining sound for the cases Sharpy supports (no mutable function-type containers that would expose the unsoundness). Strict contravariant checking is enforced at **declaration sites** via `VarianceValidator` for interface and delegate type parameters -- see [Generic Variance](generic_variance.md).

```python
# Covariance in return types
type AnimalFactory = () -> Animal
type DogFactory = () -> Dog

dog_factory: DogFactory = lambda: Dog()
animal_factory: AnimalFactory = dog_factory  # OK: Dog is subtype of Animal

# Bidirectional parameter compatibility
type AnimalHandler = (Animal) -> None
type DogHandler = (Dog) -> None

animal_handler: AnimalHandler = lambda a: print(a)
dog_handler: DogHandler = animal_handler  # OK: Animal assignable to Dog's position (bidirectional)
```

## Using Function Types

**As parameter types:**

```python
def apply(value: int, transform: (int) -> int) -> int:
    return transform(value)

result = apply(5, lambda x: x * 2)  # 10
```

**As return types:**

```python
def make_multiplier(factor: int) -> (int) -> int:
    return lambda x: x * factor

doubler = make_multiplier(2)
print(doubler(5))  # 10
```

**As field types:**

```python
class Button:
    on_click: ((Button) -> None)?

    def __init__(self):
        self.on_click = None

    def click(self) -> None:
        if self.on_click is not None:
            self.on_click(self)
```

**In collections:**

```python
handlers: list[(Event) -> None] = []
transforms: dict[str, (int) -> int] = {}
```

## Constructor References

A bare type name used as a value — `f = int`, `f = dict`, `f = MyClass` — is a **constructor reference**. It is a legitimate value, but like a C# *method group* it has no natural type of its own: `int`, `str`, `float` and `bool` each name an overload set; `list`, `dict` and `set` are generic; and a user class may declare several constructors. Nothing in the reference itself says which signature was meant, so Sharpy takes the signature from the context, in one of three ways.

**1. Pinned against an expected function type.** Wherever a signature is available — an annotated target, a declared return type, or the parameter it is passed to — the reference binds that signature:

```python
g: (str) -> int = int
print(g("42"))                       # 42

h: () -> dict[str, int] = dict
d = h()
d["a"] = 1

def make_parser() -> (str) -> int:
    return int                       # the declared return type pins it

def apply(fn: (str) -> int, s: str) -> int:
    return fn(s)

print(apply(int, "5"))               # the parameter type pins it
```

The collection families pin to their empty constructor (`() -> list[int]`) or their copy constructor (`(list[int]) -> list[int]`).

A user class or struct pins against its **declared constructors**:

```python
class Point:
    x: int

    def __init__(self, x: int):
        self.x = x

mk: (int) -> Point = Point
print(mk(7).x)                       # 7

print(list(map(Point, [1, 2, 3])))   # the class name as a factory, like map(int, xs)
```

A class with no declared `__init__` offers exactly the zero-argument shape, and a **generic** class takes its type arguments from the target exactly as the collections do — from the target, never from the reference:

```python
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

mb: (int) -> Box[int] = Box         # the BARE name; the target supplies T
print(mb(3).value)                  # 3
```

Writing the type arguments on the reference instead (`f = Box[int]`) is a *type* reference, not a value, and is refused with SPY0339.

**2. A call-only alias.** A binding with no signature available aliases the type, and each call through it resolves exactly like a call of the type itself — Python's factory-alias pattern:

```python
f = int
print(f("3"))                        # 3

d_maker = dict
d: dict[str, int] = d_maker()        # the annotated target infers K and V, as dict() would

p_maker = Point
p = p_maker(5)                       # a user class aliases the same way

b_maker = Box
b = b_maker(9)                       # a generic class too; the call infers Box[int]
```

Reassigning re-aliases, and each call site binds its own reaching binding. The alias itself has no runtime representation: it emits no C#, exactly as a C# method group is not a value until it is converted.

Because the alias has no runtime value, it is resolved where it is **read**, against the binding reaching that read at compile time — not when the call runs. A closure that captures an alias is therefore fixed to the binding in effect where its body was checked, and rebinding the name afterwards does not change it. This is a deliberate divergence from Python, where the closure would re-read the name at call time:

```python
f = float
g: (str) -> float = lambda s: f(s)   # pinned to float, here
f = int
print(g("42"))                       # 42.0 in Sharpy; Python prints 42
```

There is nothing to capture and nothing a later rebinding could update, so late binding is not available at any reasonable cost — Axiom 1 over Axiom 2. Catalogued in [`docs/deviations.yaml`](../deviations.yaml) as `constructor-alias-eager-capture`.

**3. Otherwise, an error (SPY0342).** A reference that is neither pinned nor a call-only alias has no signature and no way to acquire one, so it is refused with guidance rather than compiled into something arbitrary:

```python
xs = [int, str]                      # SPY0342 — a list element supplies no target type
f = int if c else str                # SPY0342 — a conditional is not an alias
```

Annotate the target, call the type directly, or wrap it in a lambda that fixes the signature yourself (`g = lambda s: int(s)`).

Writing a type name where it names a **type** rather than a value is unaffected, for builtin and user names alike: a static-member receiver (`int.parse(s)`, `dict.fromkeys(ks)`, `Point.of(v)`), a type-test type argument (`isinstance(x, int)`, `isinstance(x, Point)`), and a type argument (`Box[int]`, `Box[Point]`) are all type positions, not constructor references.

*Implementation*
- *✅ Native — the conversion families emit the `Sharpy.Builtins.X` method group, so C#'s own method-group conversion binds the overload against the pinned delegate type; the collection and user-type families emit a constructor lambda at a reference and `new T(args)` at an alias call.*
- *✅ User classes and structs ([#1211](https://github.com/antonsynd/sharpy/issues/1211)), pinning against their declared constructors, including generic classes whose type arguments come from the target. Interfaces, enums, unions and abstract classes are not constructible and are not constructor references.*
- *⚠️ A generic type reference that carries its own type arguments (`f = Box[int]`) is a type reference, not a value, and stays refused with SPY0339.*
- *⚠️ A user class with several constructor overloads passed as a direct call argument that does not pin still reaches SPY0908 rather than a deliberate diagnostic — see [#1249](https://github.com/antonsynd/sharpy/issues/1249).*
- *⚠️ An alias rebound inside a conditional resolves against the binding that reached the `if`, silently constructing the wrong type — see [#1248](https://github.com/antonsynd/sharpy/issues/1248). Straight-line rebinding is correct. This is a defect, not the deliberate divergence above.*

## C# Mapping

Function types map to C# delegate types:

| Sharpy | C# |
|--------|-----|
| `() -> None` | `Action` |
| `(T) -> None` | `Action<T>` |
| `(T1, T2) -> None` | `Action<T1, T2>` |
| `() -> R` | `Func<R>` |
| `(T) -> R` | `Func<T, R>` |
| `(T1, T2) -> R` | `Func<T1, T2, R>` |

*Implementation*
- *✅ Native - Maps to `System.Action<>` and `System.Func<>` delegates.*

## Delegates vs Function Types

Function types (`(T) -> R`) and [delegates](delegates.md) (`delegate F(x: T) -> R`) both represent callable signatures, but serve different purposes:

- **Function types** are anonymous and map to `Func<>`/`Action<>`. Use them for internal callbacks, higher-order function parameters, and `type` aliases.
- **Delegates** are named C# types. Use them when you need variance annotations (`in`/`out`), event handler types, or a distinct named type for .NET interop.

```python
# Function type via type alias — preferred for internal use
type Transform[T, U] = (T) -> U

# Delegate — use when variance or events require it
delegate Producer[out T]() -> T
```

When in doubt, start with a function type. Promote to a `delegate` only when you need a feature that function types cannot provide. See [Delegates — When to use delegates](delegates.md#when-to-use-delegates) and [Type Aliases](type_aliases.md).
