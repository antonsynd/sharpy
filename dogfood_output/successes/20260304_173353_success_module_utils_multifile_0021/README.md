# Successful Dogfood Run

**Timestamp:** 2026-03-04T17:20:57.646479
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### shapes.spy

```python
interface IDrawable:
    def draw(self) -> str: ...

@abstract
class Shape(IDrawable):
    color: str

    def __init__(self, color: str):
        self.color = color

    def draw(self) -> str:
        return f"Drawing {self.color} shape"

    @abstract
    def get_area(self) -> float: ...

    @virtual
    def describe(self) -> str:
        return "A shape"

class Circle(Shape):
    radius: float

    def __init__(self, color: str, radius: float):
        super().__init__(color)
        self.radius = radius

    @override
    def get_area(self) -> float:
        return 3.14159 * self.radius * self.radius

    @override
    def describe(self) -> str:
        return f"A {self.color} circle with radius {self.radius}"

class Rectangle(Shape):
    width: float
    height: float

    def __init__(self, color: str, width: float, height: float):
        super().__init__(color)
        self.width = width
        self.height = height

    @override
    def get_area(self) -> float:
        return self.width * self.height

    @override
    def describe(self) -> str:
        return f"A {self.color} rectangle {self.width}x{self.height}"

```

### utils.spy

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

struct Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def distance_to_origin(self) -> float:
        return pow(self.x * self.x + self.y * self.y, 0.5)

def format_area(area: float) -> str:
    return f"Area: {area:.2f}"

def get_color_name(c: Color) -> str:
    return c.name

```

### collections.spy

```python
class Container[T]:
    items: list[T]

    def __init__(self):
        self.items = []

    def add(self, item: T) -> None:
        self.items.append(item)

    def get_count(self) -> int:
        return len(self.items)

    def get_all(self) -> list[T]:
        return self.items.copy()

class Counter:
    value: int

    def __init__(self, start: int = 0):
        self.value = start

    def increment(self) -> int:
        self.value += 1
        return self.value

    def get_value(self) -> int:
        return self.value

```

### main.spy

```python
from shapes import IDrawable, Circle, Rectangle
from utils import Color, Point, format_area, get_color_name
from collections import Container, Counter

def draw_any(drawable: IDrawable) -> str:
    return drawable.draw()

def main():
    circle: Circle = Circle("red", 5.0)
    rect: Rectangle = Rectangle("blue", 3.0, 4.0)

    generic_draw: str = draw_any(circle)
    print(generic_draw)

    desc: str = rect.describe()
    print(desc)

    circle_area: float = circle.get_area()
    print(format_area(circle_area))

    rect_area: float = rect.get_area()
    print(format_area(rect_area))

    color: Color = Color.GREEN
    color_name: str = get_color_name(color)
    print(f"Color: {color_name}")

    point: Point = Point(3.0, 4.0)
    dist: float = point.distance_to_origin()
    print(f"Distance: {dist}")

    container: Container[str] = Container[str]()
    container.add("first")
    container.add("second")
    count: int = container.get_count()
    print(f"Count: {count}")

    counter: Counter = Counter(10)
    new_val: int = counter.increment()
    print(f"Value: {new_val}")

```

## Timing

- Generation: 756.56s
- Execution: 5.15s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
