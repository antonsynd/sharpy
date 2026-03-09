# Successful Dogfood Run

**Timestamp:** 2026-03-08T18:57:06.065626
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### types.spy

```python
# Core type definitions module
# Provides enums, interfaces, and structs used across the system

enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

enum ShapeType:
    CIRCLE = 1
    RECTANGLE = 2
    TRIANGLE = 3

interface Drawable:
    def draw(self) -> str: ...

interface Measurable:
    def measure(self) -> float: ...

struct Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def distance_from_origin(self) -> float:
        return (self.x ** 2.0 + self.y ** 2.0) ** 0.5

```

### shapes.spy

```python
# Shape classes implementing interfaces
from types import Color, ShapeType, Point

class Shape:
    shape_type: ShapeType
    color: Color

    def __init__(self, shape_type: ShapeType, color: Color):
        self.shape_type = shape_type
        self.color = color

    @virtual
    def get_area(self) -> float:
        return 0.0

class Circle(Shape):
    radius: float
    center: Point

    def __init__(self, radius: float, center: Point, color: Color = Color.RED):
        super().__init__(ShapeType.CIRCLE, color)
        self.radius = radius
        self.center = center

    @override
    def get_area(self) -> float:
        return 3.14159 * self.radius * self.radius

class Rectangle(Shape):
    width: float
    height: float

    def __init__(self, width: float, height: float, color: Color):
        super().__init__(ShapeType.RECTANGLE, color)
        self.width = width
        self.height = height

    @override
    def get_area(self) -> float:
        return self.width * self.height

# Separate class implementing Drawable interface
class CanvasDrawable:
    name: str

    def __init__(self, name: str):
        self.name = name

    def draw(self) -> str:
        return f"Drawing: {self.name}"

```

### utils.spy

```python
# Utility functions and helpers
from types import Color, Point

def factorial(n: int) -> int:
    if n <= 1:
        return 1
    return n * factorial(n - 1)

def format_color(c: Color) -> str:
    return c.name

def create_origin_point() -> Point:
    return Point(0.0, 0.0)

# Simple max function for int (concrete type, not generic)
def int_max(a: int, b: int) -> int:
    if a > b:
        return a
    return b

# Simple max function for float (concrete type, not generic)
def float_max(a: float, b: float) -> float:
    if a > b:
        return a
    return b

@static
FACTORIAL_CACHE: dict[int, int] = {}

```

### main.spy

```python
# Main entry point - demonstrates cross-module type usage
from types import Color, ShapeType, Point
from shapes import Circle, Rectangle, CanvasDrawable
from utils import factorial, format_color, create_origin_point, int_max, float_max

def main():
    # Test enum access and method
    c: Color = Color.GREEN
    print(c.name)

    # Test struct creation and method
    p: Point = Point(3.0, 4.0)
    print(p.distance_from_origin())

    # Test class inheritance and method override
    circle: Circle = Circle(5.0, p, Color.BLUE)
    print(circle.get_area())

    # Test another shape
    rect: Rectangle = Rectangle(10.0, 5.0, Color.RED)
    print(rect.get_area())

    # Test interface implementation via duck typing
    drawable: CanvasDrawable = CanvasDrawable("TestObject")
    print(drawable.draw())

    # Test utility function
    print(factorial(5))

    # Test concrete max functions (avoiding generic with function type)
    result: int = int_max(10, 25)
    print(result)

    f_result: float = float_max(3.3, 2.2)
    print(f_result)

```

## Timing

- Generation: 877.79s
- Execution: 5.38s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
