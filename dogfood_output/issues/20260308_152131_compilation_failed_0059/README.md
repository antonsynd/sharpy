# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T15:19:06.024340
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module features

from shapes import ShapeBase, Rectangle, Circle, IShape
from geometry import Point, Color, ColoredPoint, get_quadrant, Color
from utils import process_shapes, create_test_points, ShapeStats

def main():
    # Create shapes from shapes module
    shapes: list[ShapeBase] = []
    shapes.append(Rectangle(5.0, 3.0))
    shapes.append(Circle(2.0))
    shapes.append(Rectangle(4.0, 4.0))

    # Process shapes using utils function
    total_area: float
    total_perimeter: float
    total_area, total_perimeter = process_shapes(shapes)

    print(total_area)
    print(total_perimeter)

    # Test polymorphic describe (virtual dispatch)
    for shape in shapes:
        print(shape.describe())

    # Use geometry module - create points and test quadrants
    points: list[Point] = create_test_points()
    for p in points:
        print(get_quadrant(p))

    # Test ColoredPoint with property access via geometry module
    cp: ColoredPoint = ColoredPoint(7.0, 8.0, Color.YELLOW)
    print(cp.color.name)

    # Use ShapeStats singleton from utils
    stats: ShapeStats = ShapeStats.get_instance()
    stats.increment()
    stats.increment()
    print(stats.count)

    # Iterate over enum from geometry module
    color_count: int = 0
    for c in Color:
        color_count += 1
    print(color_count)

```

## Error

```
Assembly compilation failed:

error[CS0509]: 'Geometry.ColoredPoint': cannot derive from sealed type 'Geometry.Point'
  --> /tmp/tmp3zj2i_vv/geometry.spy:19:33
    |
 19 |     print(total_area)
    |                      ^
    |

error[CS0103]: The name 'math' does not exist in the current context
  --> /tmp/tmp3zj2i_vv/shapes.spy:66:20

error[CS1061]: 'Geometry.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Geometry.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp3zj2i_vv/main.spy:33:48
    |
 33 |     print(cp.color.name)
    |                         ^
    |

error[CS0103]: The name 'math' does not exist in the current context
  --> /tmp/tmp3zj2i_vv/shapes.spy:71:27

error[CS1061]: 'Geometry.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Geometry.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp3zj2i_vv/geometry.spy:33:43
    |
 33 |     print(cp.color.name)
    |                         ^
    |

error[CS1061]: 'Geometry.ColoredPoint' does not contain a definition for 'X' and no accessible extension method 'X' accepting a first argument of type 'Geometry.ColoredPoint' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp3zj2i_vv/geometry.spy:34:94
    |
 34 | 
    | ^
    |

error[CS1061]: 'Geometry.ColoredPoint' does not contain a definition for 'Y' and no accessible extension method 'Y' accepting a first argument of type 'Geometry.ColoredPoint' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp3zj2i_vv/geometry.spy:34:143
    |
 34 | 
    | ^
    |

error[CS1729]: 'object' does not contain a constructor that takes 2 arguments
  --> /tmp/tmp3zj2i_vv/geometry.spy:37:73
    |
 37 |     stats.increment()
    |                      ^
    |

error[CS1503]: Argument 1: cannot convert from 'Geometry.ColoredPoint' to 'Geometry.Point'
  --> /tmp/tmp3zj2i_vv/utils.spy:20:23
    |
 20 |     print(total_perimeter)
    |                       ^
    |

error[CS1503]: Argument 1: cannot convert from 'Geometry.ColoredPoint' to 'Geometry.Point'
  --> /tmp/tmp3zj2i_vv/utils.spy:21:23
    |
 21 | 
    | ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmp3zj2i_vv/utils.spy:3:17
    |
  3 | from shapes import ShapeBase, Rectangle, Circle, IShape
    |                 ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Circle' is never used
  --> /tmp/tmp3zj2i_vv/utils.spy:3:28
    |
  3 | from shapes import ShapeBase, Rectangle, Circle, IShape
    |                            ^^^^^^
    |

warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmp3zj2i_vv/main.spy:3:50
    |
  3 | from shapes import ShapeBase, Rectangle, Circle, IShape
    |                                                  ^^^^^^
    |


```

## Timing

- Generation: 125.86s
- Execution: 4.93s
