# Successful Dogfood Run

**Timestamp:** 2026-03-07T01:33:29.646054
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### utils.spy

```python
struct Point:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

def distance(p1: Point, p2: Point) -> float:
    dx: float = p2.x - p1.x
    dy: float = p2.y - p1.y
    return (dx * dx + dy * dy) ** 0.5

```

### base_shapes.spy

```python
from utils import Point

enum ShapeType:
    CIRCLE = 1
    RECTANGLE = 2
    POLYGON = 3

@abstract
class Shape:
    position: Point
    _name: str
    
    def __init__(self, pos: Point, name: str):
        self.position = pos
        self._name = name
    
    @virtual
    def get_name(self) -> str:
        return self._name
    
    @abstract
    def compute_area(self) -> float
    
    @abstract
    def scale(self, factor: float) -> None
    
    @abstract
    def draw(self) -> str

```

### concrete_shapes.spy

```python
from utils import Point
from base_shapes import Shape

class Circle(Shape):
    radius: float
    
    def __init__(self, pos: Point, radius: float):
        super().__init__(pos, "Circle")
        self.radius = radius
    
    @override
    def compute_area(self) -> float:
        return 3.14159 * self.radius * self.radius
    
    @override
    def scale(self, factor: float) -> None:
        self.radius = self.radius * factor
    
    @override
    def draw(self) -> str:
        return f"Circle(r={self.radius:0.1f})"

class Rectangle(Shape):
    width: float
    height: float
    
    def __init__(self, pos: Point, width: float, height: float):
        super().__init__(pos, "Rectangle")
        self.width = width
        self.height = height
    
    @override
    def compute_area(self) -> float:
        return self.width * self.height
    
    @override
    def scale(self, factor: float) -> None:
        self.width = self.width * factor
        self.height = self.height * factor
    
    @override
    def draw(self) -> str:
        return f"Rectangle({self.width:0.1f}x{self.height:0.1f})"

```

### main.spy

```python
from base_shapes import Shape, ShapeType
from concrete_shapes import Circle, Rectangle
from utils import Point, distance

def describe(item: Shape) -> str:
    name: str = item.get_name()
    area: float = item.compute_area()
    drawing: str = item.draw()
    return f"{name}: area={area:0.2f}, {drawing}"

def main():
    origin: Point = Point(0.0, 0.0)
    corner: Point = Point(3.0, 4.0)
    
    circle: Circle = Circle(origin, 5.0)
    rect: Rectangle = Rectangle(corner, 4.0, 3.0)
    
    print(circle.get_name())
    print(rect.get_name())
    
    dist: float = distance(circle.position, rect.position)
    print(f"Distance: {dist:0.2f}")
    
    print(describe(circle))
    print(describe(rect))
    
    circle.scale(2.0)
    rect.scale(0.5)
    
    print(describe(circle))
    print(describe(rect))
    
    print(ShapeType.CIRCLE.name)
    print(ShapeType.RECTANGLE.value)

```

## Timing

- Generation: 257.73s
- Execution: 4.72s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
