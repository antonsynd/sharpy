# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T12:09:17.909426
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module imports

from geometry_utils import ShapeType, distance_between, distance_from_origin, Point
from shapes_extended import Circle, Rectangle

def main():
    # Create points and shapes using multiple modules
    origin: Point = Point(0.0, 0.0)
    center: Point = Point(5.0, 5.0)
    
    circle: Circle = Circle(1, center, 3.0)
    rect: Rectangle = Rectangle(2, Point(1.0, 1.0), 4.0, 6.0)
    
    # Test 1: Circle area and drawing (Interface method dispatch)
    area: float = circle.area()
    print(area)
    
    # Test 2: Rectangle drawing
    desc: str = rect.draw()
    print(desc)
    
    # Test 3: Rectangle perimeter
    perim: float = rect.perimeter()
    print(perim)
    
    # Test 4: Distance calculation between two points
    dist: float = distance_between(Point(0.0, 0.0), Point(3.0, 4.0))
    print(dist)
    
    # Test 5: Distance from point to origin
    dist_origin: float = distance_from_origin(center)
    print(dist_origin)
    
    # Test 6: Shape type enum values
    ct: ShapeType = ShapeType.CIRCLE
    rt: ShapeType = ShapeType.RECTANGLE
    print(ct)
    print(rt)
    
# EXPECTED OUTPUT:
# 28.27431
# Rectangle at (1.0, 1.0) size=4.0x6.0
# 20.0
# 5.0
# 7.0710678118654755
# 0
# 1
```

## Error

```
Assembly compilation failed:

error[CS0534]: 'ShapesExtended.Circle' does not implement inherited abstract member 'ShapesTypes.Shape.GetType()'
  --> shapes_extended.cs:14:18
    |
 14 |     # Test 1: Circle area and drawing (Interface method dispatch)
    |                  ^
    |

error[CS0534]: 'ShapesExtended.Rectangle' does not implement inherited abstract member 'ShapesTypes.Shape.GetType()'
  --> /tmp/tmp_0__k3k9/shapes_extended.spy:17:18
    |
 17 |     
    |     ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmp_0__k3k9/shapes_extended.spy:4:7
    |
  4 | from shapes_extended import Circle, Rectangle
    |       ^^^^^^^^^
    |

warning[SPY0451]: Local variable 'origin' is assigned but never used
  --> /tmp/tmp_0__k3k9/main.spy:8:5
    |
  8 |     origin: Point = Point(0.0, 0.0)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 178.56s
- Execution: 4.26s
