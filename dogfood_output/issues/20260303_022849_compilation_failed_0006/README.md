# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T02:27:06.470599
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates multi-file imports

from math_utils import cube, average, MathHelper
from shapes import Rectangle, Circle, calculate_with_helper

def main():
    # Test basic function import
    result: int = cube(3)
    print(result)
    
    # Test imported average function
    avg: float = average(10.0, 20.0)
    print(avg)
    
    # Test imported class from shapes
    rect: Rectangle = Rectangle(5.0, 3.0)
    print(rect.describe())
    print(rect.area())
    
    # Test imported class that uses imported constant
    circle: Circle = Circle(2.0)
    print(circle.describe())
    print(circle.area())
    
    # Test function that takes imported class parameter
    helper: MathHelper = MathHelper(2)
    scaled: int = calculate_with_helper(4, helper)
    print(scaled)

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'Shapes.Shape.Area()' is abstract but it is contained in non-abstract type 'Shapes.Shape'
  --> shapes.cs:15:32
    |
 15 |     # Test imported class from shapes
    |                                ^
    |

error[CS0513]: 'Shapes.Shape.Describe()' is abstract but it is contained in non-abstract type 'Shapes.Shape'
  --> shapes.cs:16:32
    |
 16 |     rect: Rectangle = Rectangle(5.0, 3.0)
    |                                ^
    |


```

## Timing

- Generation: 87.46s
- Execution: 4.49s
