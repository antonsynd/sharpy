# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T10:55:27.647922
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests cross-module inheritance and interfaces
from utilities import Color, Point, ScalableRectangle, get_color_name, make_point, ScaleRecord, IScalable
from geometry import Shape, Rectangle, Circle, create_rectangle, total_area

def main():
    # Test 1: Shape inheritance and virtual method dispatch
    r: Rectangle = Rectangle(5.0, 3.0)
    c: Circle = Circle(2.0)
    print(r.describe())
    print(c.describe())

    # Test 2: Shape area calculations
    print(r.area())
    print(c.area())

    # Test 3: Total area from geometry module
    shapes: list[Shape] = []
    shapes.append(r)
    shapes.append(c)
    print(total_area(shapes))

    # Test 4: Enum from utilities
    current_color: Color = Color.GREEN
    print(get_color_name(current_color))

    # Test 5: Struct usage and instantiation
    p: Point = make_point(3.0, 4.0)
    print(p.distance_from_origin())

    # Test 6: Cross-module inheritance + interface implementation
    sr: ScalableRectangle = ScalableRectangle(0.0, 0.0, 10.0, 20.0)
    sr.scale(2.0)
    print(sr.width)
    print(sr.record.average_size())

```

## Error

```
Assembly compilation failed:

error[CS0509]: 'Utilities.ScalableRectangle': cannot derive from sealed type 'Utilities.Point'
  --> /tmp/tmpmdvy1nbx/utilities.spy:41:38

error[CS1729]: 'object' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpmdvy1nbx/utilities.spy:68:85

error[CS0117]: 'Geometry.Circle' does not contain a definition for 'Pi'
  --> /tmp/tmpmdvy1nbx/geometry.spy:70:27

error[CS0117]: 'Geometry.Circle' does not contain a definition for 'Pi'
  --> /tmp/tmpmdvy1nbx/geometry.spy:75:34


```

## Compiler Output

```
warning[SPY0452]: Imported name 'utilities' is never used
  --> /tmp/tmpmdvy1nbx/geometry.spy:2:5
    |
  2 | from utilities import Color, Point, ScalableRectangle, get_color_name, make_point, ScaleRecord, IScalable
    |     ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ScaleRecord' is never used
  --> /tmp/tmpmdvy1nbx/main.spy:2:84
    |
  2 | from utilities import Color, Point, ScalableRectangle, get_color_name, make_point, ScaleRecord, IScalable
    |                                                                                    ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IScalable' is never used
  --> /tmp/tmpmdvy1nbx/main.spy:2:97
    |
  2 | from utilities import Color, Point, ScalableRectangle, get_color_name, make_point, ScaleRecord, IScalable
    |                                                                                                 ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'create_rectangle' is never used
  --> /tmp/tmpmdvy1nbx/main.spy:3:48
    |
  3 | from geometry import Shape, Rectangle, Circle, create_rectangle, total_area
    |                                                ^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 269.03s
- Execution: 5.24s
