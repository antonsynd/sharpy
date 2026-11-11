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

- `self.__init__(...)`, like `super().__init__(...)`, must be the **first statement** in the constructor body (a docstring may precede it). The call runs as C#'s constructor initializer, before every statement of the body, so a statement written ahead of it is refused (SPY0292: move the call first), as is a `self.__init__(...)` inside control flow
- Only one `self.__init__()` call is allowed per constructor
- The compiler detects this pattern and transforms it to C#'s `: this(...)` syntax
- After the chained constructor returns, execution continues with the rest of the body (if any)

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
