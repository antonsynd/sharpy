# Skipped Dogfood Run

**Timestamp:** 2026-02-19T00:52:06.518281
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

Import resolution errors:
error[SPY0301]: Module 'types' has no exported symbol 'Status' (in main.spy)
  --> /tmp/tmpgq9z0ubl/main.spy:2:37
    |
  2 | from types import Point, Dimension, Status
    |                                     ^^^^^^
    |

error[SPY0301]: Module 'types' has no exported symbol 'Status' (in geometry.spy)
  --> /tmp/tmpgq9z0ubl/geometry.spy:2:9
    |
  2 | from types import Point, Dimension, Status
    |         ^^^^^^
    |

Type errors:
error[SPY0200]: Undefined identifier 'Status'
  --> /tmp/tmpgq9z0ubl/main.spy:20:9
    |
 20 |     s = Status.ACTIVE
    |         ^^^^^^
    |

error[SPY0200]: Undefined identifier 'Status'
  --> /tmp/tmpgq9z0ubl/main.spy:43:19
    |
 43 |     test_status = Status.COMPLETED
    |                   ^^^^^^
    |

error[SPY0200]: Undefined identifier 'Status'
  --> /tmp/tmpgq9z0ubl/main.spy:44:23
    |
 44 |     if test_status == Status.PENDING:
    |                       ^^^^^^
    |

error[SPY0200]: Undefined identifier 'Status'
  --> /tmp/tmpgq9z0ubl/main.spy:46:25
    |
 46 |     elif test_status == Status.ACTIVE:
    |                         ^^^^^^
    |

error[SPY0200]: Undefined identifier 'Status'
  --> /tmp/tmpgq9z0ubl/main.spy:48:25
    |
 48 |     elif test_status == Status.COMPLETED:
    |                         ^^^^^^
    |


**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### types.spy

```python
# Types module with structs and enums
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def distance_sq(self) -> int:
        return self.x * self.x + self.y * self.y

    def translate(self, dx: int, dy: int) -> Point:
        return Point(self.x + dx, self.y + dy)

struct Dimension:
    width: int
    height: int

    def __init__(self, w: int, h: int):
        self.width = w
        self.height = h

    def area(self) -> int:
        return self.width * self.height

    def is_square(self) -> bool:
        return self.width == self.height

enum Status:
    PENDING = 0
    ACTIVE = 1
    COMPLETED = 2
    FAILED = 3
```

### geometry.spy

```python
# Geometry module with interfaces and base class
from types import Status

interface IShape:
    def area(self) -> float:
        ...

@abstract
class ShapeBase(IShape):
    name: str

    def __init__(self, name: str):
        self.name = name

    def get_name(self) -> str:
        return self.name

    @virtual
    def describe(self) -> str:
        return f"Shape: {self.name}"

class Rectangle(ShapeBase):
    width: int
    height: int

    def __init__(self, w: int, h: int):
        super().__init__("Rectangle")
        self.width = w
        self.height = h

    @override
    def area(self) -> float:
        return self.width * self.height

    @override
    def describe(self) -> str:
        return f"Rectangle {self.width}x{self.height}"

class Circle(ShapeBase):
    radius: int

    def __init__(self, r: int):
        super().__init__("Circle")
        self.radius = r

    @override
    def area(self) -> float:
        return 3.14159 * self.radius * self.radius
```

### main.spy

```python
# Entry point - tests cross-module classes, structs, enums
from types import Point, Dimension, Status
from geometry import Rectangle, Circle, IShape, ShapeBase

def main():
    # Test struct instantiation and methods
    p1 = Point(3, 4)
    p2 = Point(10, 20)
    print(p1.x)
    print(p1.y)
    print(p2.x)

    dim = Dimension(5, 5)
    print(dim.width)
    print(dim.height)
    print(dim.area())
    print(dim.is_square())

    # Test enum usage
    s = Status.ACTIVE
    print(s)

    # Test interface polymorphism
    r = Rectangle(4, 5)
    c = Circle(3)
    shapes: list[IShape] = [r, c]
    for shape in shapes:
        print(shape.area())

    # Test inheritance with super()
    rect = Rectangle(2, 3)
    print(rect.get_name())
    print(rect.describe())

    # Test polymorphic collection
    total: float = 0.0
    analyzer_shapes: list[IShape] = [Rectangle(2, 3), Circle(5), Rectangle(4, 4)]
    for shape in analyzer_shapes:
        total = total + shape.area()
    print(total)

    # Test if/elif chains for enum mapping
    test_status = Status.COMPLETED
    if test_status == Status.PENDING:
        print("waiting")
    elif test_status == Status.ACTIVE:
        print("running")
    elif test_status == Status.COMPLETED:
        print("done")
    else:
        print("error")
    print("end")

# EXPECTED OUTPUT:
# 3
# 4
# 10
# 5
# 5
# 25
# True
# Active
# 20.0
# 28.27431
# Rectangle
# Rectangle 2x3
# 110.53975
# done
# end
```

## Timing

- Generation: 582.38s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
