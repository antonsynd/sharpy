# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T04:35:29.984924
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports, inheritance, and polymorphism

from geometry import ShapeType, Point, Shape, Circle, Rectangle
from utils import calculate_distance, format_shape_info, ShapeStats

def main():
    # Test struct methods and cross-module function usage
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    print(calculate_distance(p1, p2))
    
    # Create heterogeneous list demonstrating polymorphism
    shapes: list[Shape] = []
    shapes.append(Circle(Point(0.0, 0.0), 2.0))
    shapes.append(Rectangle(Point(0.0, 0.0), 3.0, 4.0))
    shapes.append(Circle(Point(0.0, 0.0), 1.0))
    
    stats: ShapeStats = ShapeStats()
    
    # Process shapes demonstrating virtual dispatch and enum usage
    for shape in shapes:
        area: float = shape.area()
        stats.add_shape(area)
        type_name: str = shape.get_type().name
        print(format_shape_info(type_name, area))
    
    print(stats.total_area)
    
    # Demonstrate enum iteration with name access
    for st in ShapeType:
        print(st.name)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Geometry.ShapeType' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Geometry.ShapeType' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp109ai3d8/main.spy:24:47
    |
 24 |         type_name: str = shape.get_type().name
    |                                               ^
    |


```

## Timing

- Generation: 328.76s
- Execution: 5.14s
