# Function and Method Overloading

Sharpy supports defining multiple functions or methods with the same name, provided their parameter signatures differ. This follows C# overloading semantics.

For the authoritative overload resolution rules (applicability and betterness, applicable to both functions and methods), see [Overload Resolution](overload_resolution.md).

## Module-Level Function Overloading

Multiple `def` statements with the same name can appear at module level if they have different parameter signatures (arity or types). Unlike Python, which replaces previous definitions, Sharpy creates overloads — consistent with class method behavior.

```python
def describe(x: int) -> str:
    return "int:" + str(x)

def describe(x: str) -> str:
    return "str:" + x

def describe(x: int, verbose: bool) -> str:
    return f"int({x}, verbose={verbose})"
```

```python
print(describe(42))           # "int:42"
print(describe("hello"))      # "str:hello"
print(describe(42, True))     # "int(42, verbose=True)"
```

Module-level overloads are importable across files:

```python
# main.spy
from lib import describe

print(describe(42))       # Resolves to describe(int) in lib
print(describe("hello"))  # Resolves to describe(str) in lib
```

*Implementation*
- *✅ Native - C# supports method overloading directly. Each overload emits as a separate static method in the module class.*

## Basic Method Overloading

Methods can be overloaded by parameter count (arity) or parameter types:

```python
class Formatter:
    def format(self, value: int) -> str:
        return "int:" + str(value)

    def format(self, value: str) -> str:
        return "str:" + value

    def format(self, value: int, width: int) -> str:
        return str(value).rjust(width)
```

```python
f = Formatter()
print(f.format(42))        # "int:42"
print(f.format("hello"))   # "str:hello"
print(f.format(42, 10))    # "        42"
```

*Implementation*
- *✅ Native - C# supports method overloading directly.*

## Constructor Overloading

Multiple `__init__` methods can be defined with different signatures. Constructor chaining is supported via `self.__init__()`:

```python
class Point:
    x: int
    y: int
    z: int

    def __init__(self, x: int, y: int, z: int):
        self.x = x
        self.y = y
        self.z = z

    def __init__(self, x: int, y: int):
        self.__init__(x, y, 0)

    def __init__(self, x: int):
        self.__init__(x, 0, 0)
```

```python
p1 = Point(1, 2, 3)  # x=1, y=2, z=3
p2 = Point(4, 5)     # x=4, y=5, z=0
p3 = Point(6)        # x=6, y=0, z=0
```

**Constructor chaining rules:**
- `self.__init__(...)` in a constructor body chains to another constructor of the same class
- `super().__init__(...)` chains to the base class constructor
- Chaining calls are lowered to C# constructor initializers (`: this(...)` and `: base(...)`)

*Implementation*
- *🔄 Lowered - `self.__init__(...)` → C# `: this(...)` constructor initializer*
- *🔄 Lowered - `super().__init__(...)` → C# `: base(...)` constructor initializer*

## Overloading with Default Parameters

Be cautious when combining overloads with default parameters, as this can create ambiguity:

```python
class Logger:
    def log(self, msg: str) -> None:
        print(msg)

    def log(self, msg: str, level: int) -> None:
        print(f"[{level}] {msg}")
```

```python
logger = Logger()
logger.log("hello")       # Calls log(str) — exact match
logger.log("hello", 2)    # Calls log(str, int)
```

**Avoid** overloads that differ only in having additional defaulted parameters:

```python
# ❌ Ambiguous — both match log("hello")
class BadLogger:
    def log(self, msg: str) -> None: ...
    def log(self, msg: str, level: int = 0) -> None: ...
```

## Operator Method Overloading

Dunder methods for operators can be overloaded to accept different operand types:

```python
class Vector:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

    def __eq__(self, other: Vector) -> bool:
        return self.x == other.x and self.y == other.y
```

*Implementation*
- *✅ Native - Each dunder overload maps to a separate C# operator overload.*

## Modifiers Are Part of the Signature

An overload's identity is its whole parameter annotation: `int`, `int?`, `int | None`, `int!str` and `list[int?]` are five different signatures, at every host — a module `def`, a method, an `__init__`, an imported `def`. Resolution among them has one winner:

- A bare value is the identity match for `int`. It never selects an Optional slot (`1` is not an `int?`), so next to `int | None` it selects the nullable overload.
- `Some(v)` and `None()` select the `T?` overload; a bare `None` selects `T | None`.
- `Ok(v)` / `Err(e)` select the `T!E` overload, and the constructor is typed by the slot it selected — `Ok(1)` passed to `int!str` is a `Result[int, str]`, not a Result with an open error type.
- `None()` where no overload takes an Optional is refused by the constructor's own diagnostic (SPY0244), naming every slot the candidates offer.

```python
def f(x: int) -> str:
    return "int"
def f(x: int?) -> str:
    return "opt"

def g(x: int?) -> str:
    return "opt"
def g(x: int | None) -> str:
    return "nullable"

def h(x: int) -> str:
    return "int"
def h(x: int!str) -> str:
    return "result"

print(f(1))          # int
print(f(Some(1)))    # opt
print(f(None()))     # opt
print(g(1))          # nullable — strict Optional: 1 is not an int?
print(g(None))       # nullable
print(g(Some(1)))    # opt
print(g(None()))     # opt
print(h(Ok(1)))      # result
print(h(Err("e")))   # result
```

```python
def k(x: int) -> str:
    return "int"
def k(x: int | None) -> str:
    return "nullable"

k(None())    # ❌ SPY0244: 'None()' can only construct Optional types, not 'int32' or 'int32 | None'
k(Some(1))   # ❌ SPY0220: Cannot pass argument of type 'int32?' to parameter of type 'int32' or 'int32 | None'
```

Two spellings that map to one C# parameter type are ONE signature and are refused at the declaration (SPY0701): `float` and `double` are both C# `double`; `str` and `str | None` are both C# `string`. `int` and `int?` map to `int` and `Sharpy.Optional<int>`, so they are distinct.

*Implementation*
- *✅ Native — each overload is its own C# method. A bare value bound to a `T | None` slot of an overloaded callee is emitted cast to that slot (`(int?)1`): C# admits `T → Optional<T>` implicitly and would otherwise find `f(int?)` and `f(Optional<int>)` ambiguous where Sharpy's strict Optional had already chosen.*

## Restrictions

- **Cannot differ only by return type.** Overloads must differ in parameter count or types. Two methods with identical parameter signatures but different return types are rejected.

  ```python
  # ❌ ERROR: Duplicate method signature
  class Bad:
      def process(self) -> int: ...
      def process(self) -> str: ...
  ```

- **`self` is excluded from signature comparison.** The implicit `self` parameter is not considered when comparing overload signatures.

- **Overloads must be in the same class.** A method in a subclass with the same name and signature as a base class method is an override, not an overload (use `@override`).

## Diagnostics

| Code | Level | Description |
|------|-------|-------------|
| SPY0353 | Error | Ambiguous overload — multiple overloads match equally well |
| SPY0354 | Error | No matching overload — the candidates disagree (arity, or different failing arguments). A call every candidate rejects at the SAME argument reports SPY0220 instead; see [Overload Resolution](overload_resolution.md#refusal-shape-the-argument-not-the-overload-set) |
| SPY0355 | Error | Duplicate method signature — two overloads have identical parameter signatures |
| SPY0701 | Error | Duplicate CLR-mapped signature — two spellings that are one C# parameter type (`float`/`double`, `str`/`str \| None`) |
| SPY0244 | Error | `None()` passed to an overload set none of whose slots is an Optional |

## See Also

- [Function Parameters](function_parameters.md) — General overload resolution rules and named argument interaction
- [Constructors](constructors.md) — Constructor definition and chaining
- [Operator Overloading](operator_overloading.md) — Dunder methods for operator overloading
