# Issue Report: output_mismatch

**Timestamp:** 2026-03-04T17:53:15.760961
**Type:** output_mismatch
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating complex cross-module imports
from types_module import ShapeCategory, Point2D
from shapes_module import Circle, Rectangle, create_unit_circle, create_default_rectangle
from utils_module import sum_areas, moved_point

def main():
    # Create shapes with Point2D struct from types_module
    center: Point2D = Point2D(3.0, 4.0)
    circle: Circle = Circle(center, 5.0)

    # Test struct method from types_module
    dist: float = center.distance_from_origin()
    print(dist)

    # Test enum property accessed through shape
    print(circle.get_category_name())

    # Test polymorphic describe method (virtual dispatch)
    print(circle.describe())

    # Test interface implementation and scaling
    circle.scale(2.0)
    print(circle.get_area())

    # Create and test rectangle
    rect: Rectangle = create_default_rectangle()
    print(rect.describe())
    print(rect.get_area())

    # Test list of interface type with sum utility
    # Note: Declare as list[IHasArea] and append to avoid invariance issue
    shapes: list[IHasArea] = []
    shapes.append(Circle(Point2D(0.0, 0.0), 1.0))
    shapes.append(Rectangle(Point2D(1.0, 1.0), 2.0, 2.0))
    total: float = sum_areas(shapes)
    print(total)

    # Test struct operations from utils
    p1: Point2D = Point2D(1.0, 2.0)
    p2: Point2D = moved_point(p1, 5.0, 3.0)
    print(p2.x)
    print(p2.y)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
5.0
Basic
Circle with radius 5.0 at (3.0, 4.0)
78.53975
Rectangle 2.0x3.0 at (0.0, 0.0)
6.0
9.283185307179586
6.0
3.0

```

### Actual
```
5.0
Basic
Circle with radius 5.0 at (3, 4)
314.159
Rectangle 2.0x3.0 at (0, 0)
6.0
7.14159
6.0
5.0
```

## Timing

- Generation: 262.23s
- Execution: 5.05s
