# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T10:33:06.236380
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module classes

from shapes import Shape, Rectangle, Circle, Polygon, IDrawable, IMeasurable
from utils import Point, Color, ColoredShape, format_area, create_origin

def main():
    # Test struct from utils
    p1: Point = Point(3.0, 4.0)
    p2: Point = create_origin()
    distance: float = p1.distance_to(p2)
    print(distance)
    
    # Test enum from utils
    red: Color = Color.RED
    print(red.name)
    
    # Test class inheritance from shapes
    rect: Rectangle = Rectangle(5.0, 3.0)
    print(rect.describe())
    
    # Test polymorphic dispatch
    shapes: list[Shape] = [rect, Circle(2.0)]
    for s in shapes:
        area: float = s.calculate_area()
        print(format_area(s.name, area))
    
    # Test rectangle's own methods
    print(rect.get_perimeter())
    
    # Test ColoredShape class from utils using enum
    colored: ColoredShape = ColoredShape(Color.BLUE)
    print(colored.get_color_name())

```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'math' does not exist in the current context
  --> /tmp/tmpq0qwag3_/shapes.spy:76:20

error[CS1061]: 'Utils.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Utils.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpq0qwag3_/utils.spy:34:31

error[CS0266]: Cannot implicitly convert type 'object' to 'Shapes.Rectangle'. An explicit conversion exists (are you missing a cast?)
  --> /tmp/tmpq0qwag3_/shapes.spy:62:38


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Polygon' is never used
  --> /tmp/tmpq0qwag3_/main.spy:3:46
    |
  3 | from shapes import Shape, Rectangle, Circle, Polygon, IDrawable, IMeasurable
    |                                              ^^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmpq0qwag3_/main.spy:3:55
    |
  3 | from shapes import Shape, Rectangle, Circle, Polygon, IDrawable, IMeasurable
    |                                                       ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmpq0qwag3_/main.spy:3:66
    |
  3 | from shapes import Shape, Rectangle, Circle, Polygon, IDrawable, IMeasurable
    |                                                                  ^^^^^^^^^^^
    |


```

## Timing

- Generation: 133.02s
- Execution: 5.21s
