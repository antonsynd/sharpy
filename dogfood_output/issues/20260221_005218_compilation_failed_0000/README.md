# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T00:48:46.357544
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# main.spy - Entry point demonstrating cross-module type usage

from shapes import IShape, IDrawable, Circle, Rectangle, Triangle, Shape
from colors import Color, Point, ColorPalette
from utils import ShapeCollector, create_random_shape, format_number

def main():
    print("Creating cross-module shapes...")
    
    # Create points and shapes using modules from different files
    origin: Point = Point(0.0, 0.0)
    center: Point = Point(5.0, 5.0)
    
    circle: Circle = Circle(3.0, Color.RED, origin)
    rect: Rectangle = Rectangle(4.0, 6.0, Color.GREEN, center)
    tri: Triangle = Triangle(3.0, 4.0, 5.0, Color.BLUE, Point(10.0, 10.0))
    
    print(circle.describe())
    print(rect.describe())
    print(tri.describe())
    
    # Test interface polymorphism
    collector: ShapeCollector = ShapeCollector()
    collector.add(circle)
    collector.add(rect)
    collector.add(tri)
    
    # Use utility functions
    shape1: Shape = create_random_shape(0, Color.YELLOW, Point(1.0, 1.0))
    shape2: Shape = create_random_shape(1, Color.PURPLE, Point(2.0, 2.0))
    collector.add(shape1)
    collector.add(shape2)
    
    print(f"Total shapes: {ShapeCollector.total_created}")
    print(f"Total area: {format_number(collector.get_total_area(), 2)}")
    print(f"Total perimeter: {format_number(collector.get_total_perimeter(), 2)}")

# EXPECTED OUTPUT:
# Creating cross-module shapes...
# Circle(r=3.0, color=Red)
# Rectangle(4.0x6.0, color=Green)
# Triangle(3.0, 4.0, 5.0, color=Blue)
# Total shapes: 5
# Total area: 122.56
# Total perimeter: 83.37
```

## Error

```
Assembly compilation failed:

error[CS0120]: An object reference is required for the non-static field, method, or property 'Utils.ShapeCollector.TotalCreated'
  --> /tmp/tmpt_q1osiz/main.spy:34:85
    |
 34 |     print(f"Total shapes: {ShapeCollector.total_created}")
    |                                                           ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'Utils.ShapeCollector.TotalCreated'
  --> /tmp/tmpt_q1osiz/utils.spy:15:13
    |
 15 |     rect: Rectangle = Rectangle(4.0, 6.0, Color.GREEN, center)
    |             ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'Utils.ShapeCollector.TotalCreated'
  --> /tmp/tmpt_q1osiz/utils.spy:15:43
    |
 15 |     rect: Rectangle = Rectangle(4.0, 6.0, Color.GREEN, center)
    |                                           ^
    |

error[CS1061]: 'Shapes.IShape' does not contain a definition for 'Describe' and no accessible extension method 'Describe' accepting a first argument of type 'Shapes.IShape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpt_q1osiz/utils.spy:32:43
    |
 32 |     collector.add(shape2)
    |                          ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'ColorPalette' is never used
  --> /tmp/tmpt_q1osiz/utils.spy:4:5
    |
  4 | from colors import Color, Point, ColorPalette
    |     ^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmpt_q1osiz/main.spy:3:20
    |
  3 | from shapes import IShape, IDrawable, Circle, Rectangle, Triangle, Shape
    |                    ^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmpt_q1osiz/main.spy:3:28
    |
  3 | from shapes import IShape, IDrawable, Circle, Rectangle, Triangle, Shape
    |                            ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ColorPalette' is never used
  --> /tmp/tmpt_q1osiz/main.spy:4:34
    |
  4 | from colors import Color, Point, ColorPalette
    |                                  ^^^^^^^^^^^^
    |


```

## Timing

- Generation: 193.45s
- Execution: 4.91s
