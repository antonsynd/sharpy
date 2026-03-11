# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T17:44:17.497744
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
from shapes import Shape, Rectangle, Color
from containers import ShapeBox, Dimensions
from utils import total_area, find_largest, create_square

def main():
    # Create various shapes across modules
    r1: Rectangle = Rectangle(3.0, 4.0, Color.RED)
    r2: Rectangle = Rectangle(2.0, 5.0, Color.GREEN)
    r3: Rectangle = create_square(3.0, Color.BLUE)
    
    # Store in list of base type
    shapes: list[Shape] = [r1, r2, r3]
    
    # Calculate total area using cross-module utility
    total: float = total_area(shapes)
    print(total)
    
    # Find largest shape via polymorphic dispatch
    largest: Shape = find_largest(shapes)
    print(largest.describe())
    
    # Test abstraction through container
    box: ShapeBox = ShapeBox(r1)
    print(box.get_area())
    
    # Test interface method (Resizable)
    r1.resize(2.0)
    print(r1.area())
    
    # Test interface method (Drawable)
    print(r1.draw())
    
    # Test enum fields
    print(r2.color.value)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.Color' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'Shapes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp3mba10dd/main.spy:34:48
    |
 34 |     print(r2.color.value)
    |                          ^
    |

error[CS1061]: 'Shapes.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp3mba10dd/shapes.spy:29:71
    |
 29 |     
    |     ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Dimensions' is never used
  --> /tmp/tmp3mba10dd/main.spy:2:34
    |
  2 | from containers import ShapeBox, Dimensions
    |                                  ^^^^^^^^^^
    |


```

## Timing

- Generation: 494.20s
- Execution: 5.10s
