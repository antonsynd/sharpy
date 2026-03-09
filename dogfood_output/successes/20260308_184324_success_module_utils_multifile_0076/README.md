# Successful Dogfood Run

**Timestamp:** 2026-03-08T18:38:46.430532
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### contracts.spy

```python
# Module defining contracts/interfaces and enums
# Used by other modules for type contracts

enum ShapeType:
    CIRCLE = 1
    RECTANGLE = 2
    TRIANGLE = 3
    POLYGON = 4

struct Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def distance_to(self, other: Point) -> float:
        dx: float = self.x - other.x
        dy: float = self.y - other.y
        return (dx * dx + dy * dy) ** 0.5

```

### shapes.spy

```python
# Module containing shape hierarchy with cross-module inheritance
from contracts import ShapeType, Point

class Shape:
    _shape_type: ShapeType

    def __init__(self, kind: ShapeType):
        self._shape_type = kind

    @virtual
    def get_area(self) -> float:
        return 0.0

    @virtual
    def get_perimeter(self) -> float:
        return 0.0

    @virtual
    def get_name(self) -> str:
        return "Shape"

    @virtual
    def describe(self) -> str:
        return "A shape"

    property get shape_kind(self) -> ShapeType:
        return self._shape_type

class Circle(Shape):
    _center: Point
    _radius: float

    def __init__(self, center: Point, radius: float):
        super().__init__(ShapeType.CIRCLE)
        self._center = center
        self._radius = radius

    @override
    def get_area(self) -> float:
        return 3.14159 * self._radius * self._radius

    @override
    def get_perimeter(self) -> float:
        return 2.0 * 3.14159 * self._radius

    @override
    def get_name(self) -> str:
        return "Circle"

    @override
    def describe(self) -> str:
        return f"A circle with radius {self._radius}"

class Rectangle(Shape):
    _top_left: Point
    _width: float
    _height: float

    def __init__(self, top_left: Point, width: float, height: float):
        super().__init__(ShapeType.RECTANGLE)
        self._top_left = top_left
        self._width = width
        self._height = height

    @override
    def get_area(self) -> float:
        return self._width * self._height

    @override
    def get_perimeter(self) -> float:
        return 2.0 * (self._width + self._height)

    @override
    def get_name(self) -> str:
        return "Rectangle"

    @override
    def describe(self) -> str:
        return f"A rectangle {self._width} x {self._height}"

```

### utils.spy

```python
# Module for utility functions
from contracts import Point

def origin() -> Point:
    return Point(0.0, 0.0)

def translate_point(p: Point, dx: float, dy: float) -> Point:
    return Point(p.x + dx, p.y + dy)

def scale_point(p: Point, factor: float) -> Point:
    return Point(p.x * factor, p.y * factor)

def format_coordinate(p: Point) -> str:
    return f"({p.x}, {p.y})"

def apply_transform(value: float, factor: float) -> float:
    return value * factor

def analyze_shape(shape: Shape) -> str:
    area: float = shape.get_area()
    perim: float = shape.get_perimeter()
    return f"area={area:.2f}, perimeter={perim:.2f}"

def get_type_name(kind: ShapeType) -> str:
    # Enum iteration to find matching name
    if kind == ShapeType.CIRCLE:
        return "Circle"
    elif kind == ShapeType.RECTANGLE:
        return "Rectangle"
    elif kind == ShapeType.TRIANGLE:
        return "Triangle"
    else:
        return "Polygon"

```

### main.spy

```python
# Main entry point demonstrating cross-module features
from contracts import ShapeType, Point
from shapes import Shape, Circle, Rectangle
from utils import origin, translate_point, scale_point, format_coordinate, analyze_shape, get_type_name

def main():
    # Create points using utility functions
    center: Point = origin()
    corner: Point = Point(5.0, 3.0)
    
    # Transform points
    scaled: Point = scale_point(corner, 2.0)
    translated: Point = translate_point(scaled, 1.0, 1.0)
    
    # Print point transformations
    print(format_coordinate(center))
    print(format_coordinate(scaled))
    print(format_coordinate(translated))
    
    # Create shapes
    circle: Circle = Circle(center, 5.0)
    rect: Rectangle = Rectangle(translated, 4.0, 3.0)
    
    # Demonstrate polymorphism via base class
    shapes: list[Shape] = [circle, rect]
    
    for s in shapes:
        result: str = analyze_shape(s)
        print(result)
        print(s.get_name())
        print(s.describe())
    
    # Test get_type_name
    print(get_type_name(ShapeType.CIRCLE))
    print(get_type_name(ShapeType.RECTANGLE))

```

## Timing

- Generation: 232.24s
- Execution: 5.23s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
