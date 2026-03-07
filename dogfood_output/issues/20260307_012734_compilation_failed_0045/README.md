# Issue Report: compilation_failed

**Timestamp:** 2026-03-07T01:18:44.812004
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point: demonstrates cross-module imports and polymorphism
from shapes import Shape, IDrawable
from graphics import Rectangle, Circle, Point, Color
from utils import describe_shape, create_point, color_from_hex

def main():
    # Test 1: Create rectangle using imported Color enum
    rect: Rectangle = Rectangle(5.0, 4.0, Color.GREEN)
    print(rect.draw())
    
    # Test 2: Describe rectangle via utility function (cross-module function call)
    desc: str = describe_shape(rect)
    print(desc)
    
    # Test 3: Create a Point struct via utility function, use it in Circle
    center: Point = create_point(0.0, 0.0)
    circle: Circle = Circle(2.0, center)
    print(circle.draw())
    
    # Test 4: Describe circle via shape abstraction
    print(describe_shape(circle))
    
    # Test 5: Use color_from_hex utility
    color: Color = color_from_hex(0xff0000)
    print(color.name)
    
    # Test 6: Polymorphism with Shape base class (interface working across modules)
    shapes: list[Shape] = [rect]
    shapes.append(circle)
    total_area: float = 0.0
    for shape in shapes:
        total_area += shape.area()
    print(f"Total area: {total_area}")

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Graphics.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Graphics.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp9sqstduc/graphics.spy:41:73


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmp9sqstduc/utils.spy:3:20
    |
  3 | from graphics import Rectangle, Circle, Point, Color
    |                    ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmp9sqstduc/main.spy:2:27
    |
  2 | from shapes import Shape, IDrawable
    |                           ^^^^^^^^^
    |


```

## Timing

- Generation: 512.19s
- Execution: 4.57s
