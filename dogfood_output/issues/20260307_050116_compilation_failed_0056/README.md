# Issue Report: compilation_failed

**Timestamp:** 2026-03-07T04:59:13.120659
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports and usage
from types_module import Color, Point
from entities_module import Circle, Rectangle
from utils_module import create_origin, compute_distance_between

def main():
    # Create origin point
    origin: Point = create_origin()
    print(f"Origin: ({origin.x}, {origin.y})")
    
    # Create two circles with different positions
    c1: Circle = Circle(Color.RED, Point(0.0, 0.0), 5.0)
    c2: Circle = Circle(Color.BLUE, Point(3.0, 4.0), 3.0)
    
    # Print circle info using overridden describe method
    print(c1.describe())
    print(c2.describe())
    
    # Test interface implementation - IMeasurable
    print(f"Circle area: {c1.get_area():.2f}")
    print(f"Circle perimeter: {c1.get_perimeter():.2f}")
    
    # Create rectangle and test polymorphism
    rect: Rectangle = Rectangle(Color.GREEN, Point(10.0, 10.0), 4.0, 6.0)
    print(rect.describe())
    
    # Test IDrawable interface method
    print(rect.draw())
    
    # Compute distance between two points
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    dist: float = compute_distance_between(p1, p2)
    print(f"Distance: {dist}")
    
    # Test enum iteration
    count: int = 0
    for c in Color:
        if c.value > 0:
            count = count + 1
    print(f"Non-zero colors: {count}")

```

## Error

```
Assembly compilation failed:

error[CS0534]: 'EntitiesModule.Circle' does not implement inherited abstract member 'EntitiesModule.Shape.GetArea()'
  --> /tmp/tmpr08eass7/entities_module.spy:19:18
    |
 19 |     # Test interface implementation - IMeasurable
    |                  ^
    |

error[CS0534]: 'EntitiesModule.Rectangle' does not implement inherited abstract member 'EntitiesModule.Shape.GetArea()'
  --> /tmp/tmpr08eass7/entities_module.spy:30:18
    |
 30 |     # Compute distance between two points
    |                  ^
    |

error[CS1061]: 'TypesModule.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'TypesModule.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpr08eass7/entities_module.spy:19:71
    |
 19 |     # Test interface implementation - IMeasurable
    |                                                  ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'format_color' is never used
  --> /tmp/tmpr08eass7/entities_module.spy:3:44
    |
  3 | from entities_module import Circle, Rectangle
    |                                            ^^
    |


```

## Timing

- Generation: 105.79s
- Execution: 4.63s
