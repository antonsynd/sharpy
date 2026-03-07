# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T19:47:19.363058
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point testing module imports and inheritance
from math_utils import Drawable, average
from shapes import Rectangle, Square

@static
def describe_shape(shape: Drawable) -> str:
    return f"Area: {shape.area()}"

def main():
    # Test basic rectangle
    rect: Rectangle = Rectangle(5.0, 3.0)
    print(rect.area())
    print(rect.perimeter())
    
    # Test square with overridden area
    sq: Square = Square(4.0)
    print(sq.area())
    print(sq.perimeter())
    
    # Test static field access
    unit_square: Square = Square.get_unit()
    print(unit_square.area())
    
    # Test polymorphic dispatch through drawable interface
    shapes: list[Drawable] = [rect, sq]
    areas: list[float] = []
    for s in shapes:
        areas.append(s.area())
    
    # Calculate average area using math_utils function
    avg: float = average(areas)
    print(avg)
    
    # Test describe function with polymorphic dispatch
    description: str = describe_shape(sq)
    print(description)

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'MathUtils.Drawable.Area()' is abstract but it is contained in non-abstract type 'MathUtils.Drawable'
  --> math_utils.cs:14:32
    |
 14 |     
    |     ^
    |

error[CS0513]: 'MathUtils.Drawable.Perimeter()' is abstract but it is contained in non-abstract type 'MathUtils.Drawable'
  --> math_utils.cs:15:32
    |
 15 |     # Test square with overridden area
    |                                ^
    |

error[CS0119]: 'MathUtils.Square(int)' is a method, which is not valid in the given context
  --> /tmp/tmp5hka1szm/main.spy:21:36
    |
 21 |     unit_square: Square = Square.get_unit()
    |                                    ^
    |


```

## Timing

- Generation: 56.71s
- Execution: 4.33s
