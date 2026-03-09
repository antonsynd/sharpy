# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T05:11:27.925118
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point

from shapes import Shape, Rectangle, Circle, Square
from utils import scale_shape, total_area

def main():
    # Create shapes
    r: Rectangle = Rectangle(5.0, 3.0)
    c: Circle = Circle(2.0)
    s: Square = Square(4.0)
    
    # Print individual areas
    print(r.area())
    print(c.area())
    print(s.area())
    
    # Store in list
    shapes: list[Shape] = list[Shape]()
    shapes.append(r)
    shapes.append(c)
    shapes.append(s)
    
    # Calculate total area
    print(total_area(shapes))
    
    # Scale shapes
    scaled: Shape = scale_shape(r, 2.0)
    print(scaled.area())
    
    # Test polymorphism
    for shape in shapes:
        print(shape.perimeter())

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'Shapes.Shape.Area()' is abstract but it is contained in non-abstract type 'Shapes.Shape'
  --> shapes.cs:14:32
    |
 14 |     print(c.area())
    |                    ^
    |

error[CS0513]: 'Shapes.Shape.Perimeter()' is abstract but it is contained in non-abstract type 'Shapes.Shape'
  --> shapes.cs:15:32
    |
 15 |     print(s.area())
    |                    ^
    |


```

## Timing

- Generation: 217.56s
- Execution: 4.89s
