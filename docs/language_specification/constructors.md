# Constructors

## Constructor Overloading

```python
class Point:
    x: float
    y: float

    def __init__(self):
        self.x = 0.0
        self.y = 0.0

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def __init__(self, other: Point):
        self.x = other.x
        self.y = other.y
```

## Constructor Chaining

One constructor can delegate to another using `self.__init__(...)` as the first statement. This maps to C#'s `: this(...)` syntax:

```python
class Point:
    x: float
    y: float

    def __init__(self):
        self.__init__(0.0, 0.0)  # Chains to the two-parameter constructor

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def __init__(self, xy: float):
        self.__init__(xy, xy)    # Chains to the two-parameter constructor
```

**Rules for Constructor Chaining:**

A *chaining call* is `self.__init__(...)` (another constructor of the same class) or `super().__init__(...)` (the base class's constructor, see [Inheritance](inheritance.md)). The compiler turns it into C#'s constructor initializer, `: this(...)` or `: base(...)`, and C# runs the initializer **before every statement of the body**. So that the program runs in the order it is written, the same rules hold for both calls:

- The chaining call must be the **first statement** of `__init__`. Only a docstring may precede it.
- **Any** statement written ahead of it is refused with SPY0292, because it would run after the call. That includes statements with no effect: `pass`, `...`, a bare annotation such as `k: int`, and a nested `def`.
- The call must be a statement of its own. It may not be under control flow (`if`, `for`, `while`, `try`, `with`, `match`), and it may not be part of an expression (a lambda, a conditional expression, the value of an assignment). Each of these is SPY0292.
- At most one chaining call is allowed per constructor. A second call of the same kind is SPY0292. Using both `self.__init__(...)` and `super().__init__(...)` in one constructor is SPY0351.
- After the chained constructor returns, the rest of the body runs in source order.

Python runs statements written ahead of `super().__init__(...)` first. Sharpy refuses them instead of reordering them (see `constructor-chaining-call-first` in `docs/deviations.yaml`):

<!-- spec-sweep: error SPY0292 -->
```python
class Base:
    size: int

    def __init__(self, size: int):
        self.size = size

class Child(Base):
    label: str

    def __init__(self, items: list[int], label: str):
        self.label = label
        n = len(items)
        super().__init__(n)  # ERROR SPY0292: two statements are written ahead of the call
```

Move the call first. Moving it keeps the program's meaning only when the statements ahead of it do not touch what the chained constructor reads or writes. If both write the same field, the other write now wins, so decide which value you mean. If an argument needs a value computed ahead of the call, compute it inside the argument expression, or in a `@static` helper:

<!-- spec-sweep: output -->
```python
class Base:
    size: int

    def __init__(self, size: int):
        self.size = size

class Child(Base):
    label: str

    def __init__(self, items: list[int], label: str):
        super().__init__(len(items))
        self.label = label

def main() -> None:
    c = Child([1, 2], "a")
    print(c.size, c.label)   # 2 a
```

```python
class Rectangle:
    x: float
    y: float
    width: float
    height: float

    def __init__(self, width: float, height: float):
        self.__init__(0.0, 0.0, width, height)  # Chain first
        print("Created rectangle")              # Then other statements

    def __init__(self, x: float, y: float, width: float, height: float):
        self.x = x
        self.y = y
        self.width = width
        self.height = height
```

*Implementation*
- *✅ Native - `__init__()` maps directly to C# constructor methods.*
- *🔄 Lowered - `self.__init__(...)` as first statement transforms to `: this(...)` in C#.*
