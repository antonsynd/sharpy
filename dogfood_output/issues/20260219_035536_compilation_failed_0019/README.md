# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T03:49:19.784915
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module inheritance
# Tests import patterns across module boundaries
from shapes import Circle, Rectangle
from shapes import ShapeType as ST
from utils import Color, Point, format_color, color_to_int, shape_type_to_int

def process_circles(circles: list[Circle]) -> float:
    total_area = 0.0
    for c in circles:
        total_area += c.area()
    return total_area

def process_rectangles(rects: list[Rectangle]) -> float:
    total_area = 0.0
    for r in rects:
        total_area += r.area()
    return total_area

def main():
    # Create shapes with cross-module enums
    pt1 = Point(0.0, 0.0)
    pt2 = Point(5.0, 5.0)
    c1 = Circle(5.0, pt1, Color.RED)
    c2 = Circle(3.0, pt2, Color.BLUE)
    r1 = Rectangle(4.0, 6.0, pt1, Color.GREEN)

    # Test 1: Base class methods
    print(c1.get_name())

    # Test 2: Virtual method override
    print(c1.get_description())

    # Test 3: Circle draw method
    print(c1.draw())

    # Test 4: Rectangle area method
    print(r1.area())

    # Test 5: Cross-module enum usage
    print(format_color(c2.color))

    # Test 6: ShapeType enum via alias
    print(r1.shape_type == ST.RECTANGLE)

    # Test 7: Process circles
    circles: list[Circle] = [c1, c2]
    circle_area = process_circles(circles)
    print(circle_area)

    # Test 8: Process rectangles
    rects: list[Rectangle] = [r1]
    rect_area = process_rectangles(rects)
    print(rect_area)

    # Test 9: Test struct Point
    print(pt1.distance_squared_to(pt2))

    # Test 10: Helper functions
    print(color_to_int(Color.BLUE))
    print(shape_type_to_int(ST.CIRCLE))

# EXPECTED OUTPUT:
# Circle
# A Red circle with radius 5.0
# Drawing circle at (0.0, 0.0)
# 24.0
# Blue
# True
# 106.859575
# 24.0
# 50.0
# 3
# 1
```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'ST' does not exist in the current context
  --> /tmp/tmp2uxrkl5s/main.spy:43:55
    |
 43 |     print(r1.shape_type == ST.RECTANGLE)
    |                                         ^
    |

error[CS0103]: The name 'ST' does not exist in the current context
  --> /tmp/tmp2uxrkl5s/main.spy:60:54
    |
 60 |     print(shape_type_to_int(ST.CIRCLE))
    |                                        ^
    |


```

## Timing

- Generation: 349.70s
- Execution: 4.30s
