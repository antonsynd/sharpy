# Method Overloading

Sharpy supports defining multiple methods with the same name in a class, provided their parameter signatures differ. This follows C# method overloading semantics.

For general overload resolution rules (applicable to both functions and methods), see [Function Parameters — Overload Resolution Rules](function_parameters.md#overload-resolution-rules).

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
| SPY0354 | Error | No matching overload — no overload matches the argument types |
| SPY0355 | Error | Duplicate method signature — two overloads have identical parameter signatures |

## See Also

- [Function Parameters](function_parameters.md) — General overload resolution rules and named argument interaction
- [Constructors](constructors.md) — Constructor definition and chaining
- [Operator Overloading](operator_overloading.md) — Dunder methods for operator overloading
