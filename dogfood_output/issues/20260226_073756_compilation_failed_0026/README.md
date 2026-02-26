# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T07:36:01.862719
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests cross-module class interactions

from shapes import Shape, Rectangle, Square, Circle
from utils import total_perimeter, largest_area

def main():
    # Create shapes from different modules
    rect: Rectangle = Rectangle("MyRect", 5.0, 3.0)
    square: Square = Square("MySquare", 4.0)
    circle: Circle = Circle("MyCircle", 2.5)
    
    # Store in list of base type
    shapes: list[Shape] = [rect, square, circle]
    
    # Print individual shape info
    print(rect.area())
    print(circle.area())
    
    # Use utility function from other module
    total: float = total_perimeter(shapes)
    print(total)
    
    # Test polymorphism - find largest
    biggest: Shape = largest_area(shapes)
    print(biggest.name)
    
    # Test static field access
    print(Circle.PI)
```

## Error

```
Assembly compilation failed:

error[CS0117]: 'Shapes.Circle' does not contain a definition for 'Pi'
  --> /tmp/tmpg0ruriv1/shapes.spy:60:27

error[CS0117]: 'Shapes.Circle' does not contain a definition for 'Pi'
  --> /tmp/tmpg0ruriv1/shapes.spy:64:34

error[CS1061]: 'Shapes.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'Shapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpg0ruriv1/main.spy:28:46
    |
 28 |     print(Circle.PI)
    |                     ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmpg0ruriv1/utils.spy:3:9
    |
  3 | from shapes import Shape, Rectangle, Square, Circle
    |         ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Circle' is never used
  --> /tmp/tmpg0ruriv1/utils.spy:3:20
    |
  3 | from shapes import Shape, Rectangle, Square, Circle
    |                    ^^^^^^
    |


```

## Timing

- Generation: 99.56s
- Execution: 4.31s
