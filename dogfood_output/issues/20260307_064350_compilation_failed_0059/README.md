# Issue Report: compilation_failed

**Timestamp:** 2026-03-07T06:40:19.979371
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module class usage
from types_module import Point, Color
from implementations import Rectangle, Circle

def main():
    # Create Point instances for shape positions
    origin: Point = Point(0.0, 0.0)
    offset: Point = Point(10.0, 20.0)

    # Print Point struct functionality
    print(origin.x)
    print(offset.y)

    # Create shapes using cross-module classes
    rect: Rectangle = Rectangle(5.0, 3.0, Color.BLUE, origin)
    circle: Circle = Circle(2.5, Color.GREEN, offset)

    # Demonstrate inheritance and polymorphism across modules
    print(rect.get_type_name())
    print(rect.area())
    print(circle.get_type_name())
    print(circle.area())

    # Access colors via getter methods (polymorphic dispatch)
    rect_color: Color = rect.get_color()
    circle_color: Color = circle.get_color()
    print(rect_color.name)
    print(circle_color.value)

    # Access positions via getter methods
    rect_pos: Point = rect.get_position()
    circle_pos: Point = circle.get_position()
    print(rect_pos.x)
    print(circle_pos.y)

    # Demonstrate interface method (describe_position)
    print(rect.describe_position())
    print(circle.describe_position())

    # Create shapes with different colors to test enum
    red_circle: Circle = Circle(1.0, Color.RED, Point(5.0, 5.0))
    yellow_rect: Rectangle = Rectangle(2.0, 4.0, Color.YELLOW, Point(1.0, 1.0))

    print(red_circle.get_color().name)
    print(yellow_rect.get_color().name)
    print(red_circle.area())
    print(yellow_rect.area())

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'TypesModule.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'TypesModule.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp1_v91sy3/main.spy:44:60
    |
 44 |     print(red_circle.get_color().name)
    |                                       ^
    |

error[CS1061]: 'TypesModule.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'TypesModule.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp1_v91sy3/main.spy:45:61
    |
 45 |     print(yellow_rect.get_color().name)
    |                                        ^
    |


```

## Timing

- Generation: 179.63s
- Execution: 4.54s
