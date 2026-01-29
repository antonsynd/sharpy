# Issue Report: execution_failed

**Timestamp:** 2026-01-29T00:07:59.768570
**Type:** execution_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - comprehensive test of geometry module system
from geometry import Shape, Point2D, ShapeType
from concrete_shapes import Rectangle, Circle, Cube
from utils import calculate_total_area, calculate_total_perimeter, create_unit_square, distance_between_points

def main():
    # Create various shapes
    rect: Rectangle = Rectangle(5.0, 3.0)
    circle: Circle = Circle(2.0)
    cube: Cube = Cube(4.0)
    unit_square: Rectangle = create_unit_square()

    # Test basic shape properties
    print(rect.area())
    print(circle.perimeter())

    # Test cross-module inheritance
    print(rect.describe())

    # Test interface implementation (Cube implements IMeasurable)
    print(cube.volume())
    print(cube.surface_area())

    # Test utility functions with polymorphism
    shapes: list[Shape] = [rect, circle, cube, unit_square]
    total_area: float = calculate_total_area(shapes)
    total_perimeter: float = calculate_total_perimeter(shapes)
    print(total_area)
    print(total_perimeter)

    # Test struct usage
    p1: Point2D = Point2D(0.0, 0.0)
    p2: Point2D = Point2D(3.0, 4.0)
    dist: float = distance_between_points(p1, p2)
    print(dist)

# EXPECTED OUTPUT:
# 15.0
# 12.56636
# Shape: Rectangle
# 64.0
# 96.0
# 93.63096
# 62.283179999999995
# 5.0
```

## Error

```
Compilation failed:
  Semantic error at line 2, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpgh1fa1ik/geometry.spy': Parser error at line 36, column 13: Expected newline, got Dedent (in main.spy)
  Semantic error at line 2, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpgh1fa1ik/geometry.spy': Parser error at line 36, column 13: Expected newline, got Dedent (in concrete_shapes.spy)
  Semantic error at line 2, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpgh1fa1ik/geometry.spy': Parser error at line 36, column 13: Expected newline, got Dedent (in concrete_shapes.spy)
  Semantic error at line 2, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpgh1fa1ik/geometry.spy': Parser error at line 36, column 13: Expected newline, got Dedent (in utils.spy)
  Semantic error at line 2, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpgh1fa1ik/geometry.spy': Parser error at line 36, column 13: Expected newline, got Dedent (in utils.spy)

```

## Timing

- Generation: 21.95s
- Execution: 1.01s
