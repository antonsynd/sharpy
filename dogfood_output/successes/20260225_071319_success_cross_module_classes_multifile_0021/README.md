# Successful Dogfood Run

**Timestamp:** 2026-02-25T07:10:09.150151
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (2 files)

## Source Files

### shapes.spy

```python
# Cross-module class hierarchy test: Shape definitions
#
# Provides base Shape class and concrete implementations
# Tests polymorphism across module boundaries with @virtual/@override

@abstract
class Shape:
    """Base shape class with virtual method"""
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstract
    def area(self) -> float: ...

    @virtual
    def describe(self) -> str:
        return f"Shape: {self.name}"

    @virtual
    def get_corners(self) -> int:
        return 0

class Circle(Shape):
    """Circle shape with radius"""
    radius: float

    def __init__(self, name: str, radius: float):
        super().__init__(name)
        self.radius = radius

    @override
    def area(self) -> float:
        return 3.14159 * self.radius * self.radius

    @override
    def describe(self) -> str:
        base: str = super().describe()
        return f"{base}, radius={self.radius}"

class Rectangle(Shape):
    """Rectangle shape with width and height"""
    width: float
    height: float

    def __init__(self, name: str, width: float, height: float):
        super().__init__(name)
        self.width = width
        self.height = height

    @override
    def area(self) -> float:
        return self.width * self.height

    @override
    def get_corners(self) -> int:
        return 4

class Triangle(Shape):
    """Triangle shape with base and height"""
    base: float
    height: float

    def __init__(self, name: str, base: float, height: float):
        super().__init__(name)
        self.base = base
        self.height = height

    @override
    def area(self) -> float:
        return 0.5 * self.base * self.height

    @override
    def describe(self) -> str:
        return f"Triangle {self.name} with base={self.base}"
```

### main.spy

```python
# Cross-module class hierarchy test: Main module
#
# Tests importing class hierarchy from shapes module
# and polymorphic dispatch across module boundaries

from shapes import Circle, Rectangle, Triangle, Shape

def print_shape_info(shape: Shape):
    """Helper function demonstrating polymorphism"""
    print(shape.describe())
    print(shape.area())

def total_area(shapes: list[Shape]) -> float:
    """Calculate total area of a list of shapes"""
    total: float = 0.0
    for s in shapes:
        total += s.area()
    return total

def main():
    # Create shapes from imported classes
    circle: Circle = Circle("MyCircle", 5.0)
    rect: Rectangle = Rectangle("MyRect", 10.0, 20.0)
    tri: Triangle = Triangle("MyTri", 6.0, 4.0)

    # Print individual shape info (demonstrating polymorphism)
    print("=== Circle ===")
    print_shape_info(circle)

    print("=== Rectangle ===")
    print_shape_info(rect)
    print(f"Corners: {rect.get_corners()}")

    print("=== Triangle ===")
    print_shape_info(tri)

    # Demonstrate storing in list and calculating total
    print("=== All shapes ===")
    all_shapes: list[Shape] = [circle, rect, tri]
    total: float = total_area(all_shapes)
    print(f"Total area: {total}")

    # Verify shape types using isinstance
    print("=== Type checks ===")
    print(f"Circle is Shape: {isinstance(circle, Shape)}")
    print(f"Triangle is Shape: {isinstance(tri, Shape)}")

# EXPECTED OUTPUT:
# === Circle ===
# Shape: MyCircle, radius=5.0
# 78.53975
# === Rectangle ===
# Shape: MyRect
# 200.0
# Corners: 4
# === Triangle ===
# Triangle MyTri with base=6.0
# 12.0
# === All shapes ===
# Total area: 290.53975
# === Type checks ===
# Circle is Shape: True
# Triangle is Shape: True
```

## Timing

- Generation: 177.52s
- Execution: 4.65s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
