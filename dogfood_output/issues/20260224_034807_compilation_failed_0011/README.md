# Issue Report: compilation_failed

**Timestamp:** 2026-02-24T03:46:25.653716
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage

from shapes import Shape, Rectangle, Circle
from utils import ShapeFormatter, calculate_total_area

def main():
    # Create instances of classes from shapes module
    rect: Rectangle = Rectangle("MyRect", 4.0, 5.0)
    circle: Circle = Circle("MyCircle", 3.0)
    
    # Use class methods from shapes module
    print(rect.get_area())
    print(circle.get_area())
    
    # Use utility formatter from utils module
    formatted: str = ShapeFormatter.format_shape(rect)
    print(formatted)
    
    # Use utility function that works with classes from shapes
    total: float = calculate_total_area(rect, circle)
    print(total)
    
    # Demonstrate inheritance through get_details
    base: Shape = Shape("Base")
    print(base.get_details())

# EXPECTED OUTPUT:
# 20.0
# 28.27431
# [MyRect - 4.0 x 5.0]
# 48.27431
# Base - base shape
```

## Error

```
Assembly compilation failed:

error[CS0513]: 'Shapes.IShape.GetArea()' is abstract but it is contained in non-abstract type 'Shapes.IShape'
  --> shapes.cs:14:32
    |
 14 |     
    |     ^
    |

error[CS0513]: 'Shapes.IShape.GetName()' is abstract but it is contained in non-abstract type 'Shapes.IShape'
  --> shapes.cs:15:32
    |
 15 |     # Use utility formatter from utils module
    |                                ^
    |


```

## Timing

- Generation: 86.97s
- Execution: 4.31s
