# Successful Dogfood Run

**Timestamp:** 2026-03-07T01:12:01.408656
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### shapes.spy

```python
interface IDrawable:
    def draw(self) -> str: ...
    def area(self) -> float: ...

@abstract
class Shape:
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstract
    def get_description(self) -> str: ...

    @virtual
    def get_name(self) -> str:
        return self.name

class Circle(Shape, IDrawable):
    radius: float

    def __init__(self, name: str, radius: float):
        super().__init__(name)
        self.radius = radius

    @override
    def get_description(self) -> str:
        return f"Circle with radius {self.radius}"

    @override
    def get_name(self) -> str:
        return f"[Circle] {self.name}"

    def draw(self) -> str:
        return "Drawing circle"

    def area(self) -> float:
        return 3.14159 * self.radius * self.radius

class Rectangle(Shape, IDrawable):
    width: float
    height: float

    def __init__(self, name: str, width: float, height: float):
        super().__init__(name)
        self.width = width
        self.height = height

    @override
    def get_description(self) -> str:
        return f"Rectangle {self.width}x{self.height}"

    def draw(self) -> str:
        return "Drawing rectangle"

    def area(self) -> float:
        return self.width * self.height

```

### utils.spy

```python
from shapes import IDrawable

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

    def __str__(self) -> str:
        return f"Point({self.x}, {self.y})"

def calculate_area(shape: IDrawable) -> float:
    return shape.area()

```

### generics.spy

```python
from shapes import IDrawable

class Container[T]:
    items: list[T]

    def __init__(self):
        self.items = []

    def add(self, item: T) -> None:
        self.items.append(item)

    def count(self) -> int:
        return len(self.items)

    def get_first(self) -> T?:
        if len(self.items) > 0:
            return Some(self.items[0])
        return None()

def create_filter(min_area: float) -> (IDrawable) -> bool:
    return lambda s: s.area() > min_area

```

### main.spy

```python
from shapes import Shape, Circle, Rectangle
from utils import Color, Point, calculate_area
from generics import Container, create_filter

def main():
    # Create shapes
    c = Circle("MyCircle", 5.0)
    r = Rectangle("MyRect", 3.0, 4.0)

    # Test 1: Polymorphic method call (overridden)
    print(c.get_name())

    # Test 2: Polymorphic method call (base implementation)
    print(r.get_name())

    # Test 3: Interface method calls
    print(c.area())
    print(r.area())

    # Test 4: Point struct
    p = Point(1.0, 2.0)
    print(str(p))

    # Test 5: Enum iteration
    total: int = 0
    for color in Color:
        total = total + color.value
    print(total)

    # Test 6: Direct function call with Circle
    result: float = calculate_area(c)
    print(result)

    # Test 7: Direct function call with Rectangle
    print(calculate_area(r))

    # Test 8: Generic container
    container = Container[Shape]()
    container.add(c)
    container.add(r)
    print(container.count())

    # Test 9: Filter function (high threshold - Circle passes)
    filter_fn = create_filter(30.0)
    print(filter_fn(c))

    # Test 10: Filter function (low area Rectangle fails)
    print(filter_fn(r))

```

## Timing

- Generation: 372.27s
- Execution: 4.98s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
