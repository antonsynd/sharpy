# Issue Report: compilation_failed

**Timestamp:** 2026-03-07T04:40:03.159719
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module inheritance,
# polymorphism, and multi-file module interactions

from shapes_core import ShapeType, IShape
from shapes_entities import Point, Circle, Rectangle, ShapeGroup
from shapes_utils import distance, scale_circle, rectangle_from_corners, ShapeStatistics

def main():
    # Create points
    origin: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)

    # Test distance calculation from shapes_utils
    dist: float = distance(origin, p2)
    print(dist)

    # Create shapes using cross-module types
    circle: Circle = Circle(origin, 5.0)
    rect: Rectangle = Rectangle(origin, 10.0, 20.0)

    # Test individual shape calculations
    print(circle.area())
    print(rect.perimeter())

    # Test scale utility
    bigger_circle: Circle = scale_circle(circle, 2.0)
    print(bigger_circle.radius)
    print(bigger_circle.area())

    # Test rectangle from corners
    corner_rect: Rectangle = rectangle_from_corners(Point(0.0, 0.0), Point(5.0, 10.0))
    print(corner_rect.width)
    print(corner_rect.height)
    print(corner_rect.area())

    # Test enum access through method (polymorphic dispatch)
    print(circle.get_shape_type().value)

    # Test polymorphism via ShapeGroup (interface-based collection)
    group: ShapeGroup = ShapeGroup()
    group.add(circle)
    group.add(rect)
    print(group.total_area())
    print(group.count())

    # Test shape statistics
    stats: ShapeStatistics = ShapeStatistics()
    stats.record_shape(circle)
    stats.record_shape(rect)
    stats.record_shape(bigger_circle)
    print(stats.shape_count)
    print(stats.total_area_sum)
    print(stats.get_average_area())

    # Test point in circle
    test_point: Point = Point(3.0, 4.0)
    dist_to_center: float = distance(test_point, origin)
    print(dist_to_center)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'ShapesCore.ShapeType' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'ShapesCore.ShapeType' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpq8qj5ki5/main.spy:37:61
    |
 37 |     print(circle.get_shape_type().value)
    |                                         ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmpq8qj5ki5/shapes_utils.spy:2:19
    |
  2 | # polymorphism, and multi-file module interactions
    |                   ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmpq8qj5ki5/main.spy:4:25
    |
  4 | from shapes_core import ShapeType, IShape
    |                         ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmpq8qj5ki5/main.spy:4:36
    |
  4 | from shapes_core import ShapeType, IShape
    |                                    ^^^^^^
    |


```

## Timing

- Generation: 819.31s
- Execution: 4.61s
