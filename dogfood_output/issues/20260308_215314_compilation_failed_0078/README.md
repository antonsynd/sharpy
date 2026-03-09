# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T21:51:32.471697
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates multi-file module usage

from math_utils import MathHelper, factorial, clamp
from shapes import Shape, Rectangle, Circle, scale_dimension, create_default_rectangle

def main():
    # Test static class and methods
    sq: int = MathHelper.square(5)
    print(sq)
    
    # Test factorial function
    fact5: int = factorial(5)
    print(fact5)
    
    # Test clamp function
    clamped_val: int = clamp(150, 0, 100)
    print(clamped_val)
    
    # Test Rectangle
    rect: Rectangle = create_default_rectangle()
    print(rect.area())
    print(rect.perimeter())
    
    # Test Circle
    circle: Circle = Circle(3.0)
    print(circle.area())
    print(circle.diameter())
    
    # Test scale_dimension (returns 50.0 * 3 = 150.0 since factor 3 is valid)
    scaled: float = scale_dimension(50.0, 3)
    print(scaled)
    
    # Test description through base class method
    desc: str = rect.describe()
    print(desc)

```

## Error

```
Assembly compilation failed:

error[CS0708]: 'MathUtils.MathHelper.PI': cannot declare instance members in a static class
  --> math_utils.cs:14:23
    |
 14 |     
    |     ^
    |

error[CS0708]: 'MathUtils.MathHelper.EULER': cannot declare instance members in a static class
  --> math_utils.cs:15:23
    |
 15 |     # Test clamp function
    |                       ^
    |

error[CS0117]: 'MathUtils.MathHelper' does not contain a definition for 'Pi'
  --> /tmp/tmpin966qi5/shapes.spy:44:31

error[CS0117]: 'MathUtils.MathHelper' does not contain a definition for 'Pi'
  --> /tmp/tmpin966qi5/shapes.spy:48:38


```

## Compiler Output

```
warning[SPY0452]: Imported name 'factorial' is never used
  --> /tmp/tmpin966qi5/shapes.spy:3:24
    |
  3 | from math_utils import MathHelper, factorial, clamp
    |                        ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpin966qi5/main.spy:4:20
    |
  4 | from shapes import Shape, Rectangle, Circle, scale_dimension, create_default_rectangle
    |                    ^^^^^
    |


```

## Timing

- Generation: 85.98s
- Execution: 4.90s
