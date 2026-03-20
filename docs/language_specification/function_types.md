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

Parameter names are **optional** in function type annotations. When provided, they serve as documentation only and are not part of the type signature:

```python
# Without parameter names (preferred for brevity)
handler: (int, str) -> bool

# With parameter names (for documentation)
handler: (count: int, message: str) -> bool

# Both forms are equivalent types
# The names do not affect type compatibility
```

**Note:** Parameter names in function types do not create named parameter requirements at call sites. They are purely for readability and documentation.

```python
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
# ❌ Invalid - cannot specify defaults in function types
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

**Rationale:** Function types describe a calling convention—what the caller must provide. Since the caller cannot know about default values, function types represent the minimal required signature. This aligns with C# delegate semantics where all parameters are required.

## Function Type Compatibility

A function type `A` is assignable to function type `B` if:
1. They have the same number of parameters
2. Parameter types are contravariant (B's parameter types assignable to A's)
3. Return types are covariant (A's return type assignable to B's)

```python
# Covariance in return types
type AnimalFactory = () -> Animal
type DogFactory = () -> Dog

dog_factory: DogFactory = lambda: Dog()
animal_factory: AnimalFactory = dog_factory  # OK: Dog is subtype of Animal

# Contravariance in parameter types
type AnimalHandler = (Animal) -> None
type DogHandler = (Dog) -> None

animal_handler: AnimalHandler = lambda a: print(a)
# dog_handler: DogHandler = animal_handler  # OK: Animal handler can accept Dog
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
