# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T12:37:16.692700
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
from geometry_utils import Rectangle, Circle, total_area, average_perimeter

def main():
    # Create some shapes for testing
    rect1 = Rectangle(5.0, 3.0)
    rect2 = Rectangle(4.0, 4.0)
    circle = Circle(2.0)

    # Create a list of shapes
    shapes: list[Shape] = [rect1, rect2, circle]

    # Test individual areas
    print(rect1.area())
    print(circle.area())

    # Test total area function
    total: float = total_area(shapes)
    print(total)

    # Test average perimeter function
    avg_perim: float = average_perimeter(shapes)
    print(avg_perim)

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'GeometryUtils.Shape.Area()' is abstract but it is contained in non-abstract type 'GeometryUtils.Shape'
  --> geometry_utils.cs:14:32
    |
 14 |     print(circle.area())
    |                         ^
    |

error[CS0513]: 'GeometryUtils.Shape.Perimeter()' is abstract but it is contained in non-abstract type 'GeometryUtils.Shape'
  --> geometry_utils.cs:15:32
    |
 15 | 
    | ^
    |


```

## Timing

- Generation: 124.87s
- Execution: 4.97s
