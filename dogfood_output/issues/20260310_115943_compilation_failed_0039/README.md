# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T11:53:41.012683
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class interactions
from shapes import Circle, Rectangle, ShapeBase
from geometry_types import IShape, IMeasurable, ShapeType
from math_utils import Point2D, GeometryUtils

def main():
    # Create points using the struct from math_utils
    origin: Point2D = Point2D(0.0, 0.0)
    corner: Point2D = Point2D(5.0, 5.0)
    print(f"Origin: {origin.to_string()}")
    print(f"Corner: {corner.to_string()}")
    print(f"Distance: {origin.distance_to(corner)}")

    # Create shapes using classes from shapes module
    circle: Circle = Circle(1, origin, 5.0)
    rectangle: Rectangle = Rectangle(2, corner, 10.0, 20.0)

    # Create list with interface type and append items individually
    # (Generic collections are invariant, cannot use list literal)
    shapes: list[IShape] = []
    shapes.append(circle)
    shapes.append(rectangle)

    for shape in shapes:
        shape_type: str = "Unknown"
        # Use isinstance check with enum comparison
        if isinstance(shape, Circle):
            shape_type = "Circle"
        elif isinstance(shape, Rectangle):
            shape_type = "Rectangle"
        print(f"Shape {shape_type}: area={shape.area()}, perimeter={shape.perimeter()}")

    # Create measurables list and append items
    measurables: list[IMeasurable] = []
    measurables.append(circle)
    measurables.append(rectangle)
    measurements: list[float] = []

    for m in measurables:
        measurement: tuple[float, str] = m.measure()
        measurements.append(measurement[0])
        print(f"Measurement: {measurement[0]} {measurement[1]}")

    # Test static method and method override
    avg: float = GeometryUtils.calculate_average(measurements)
    print(f"Average measurement: {avg}")

    # Test describe methods - polymorphic dispatch
    print(circle.describe())
    print(rectangle.describe())

```

## Error

```
Assembly compilation failed:

error[CS0708]: 'MathUtils.GeometryUtils.PI': cannot declare instance members in a static class
  --> /tmp/tmpmk7f_bs4/math_utils.spy:15:23
    |
 15 |     circle: Circle = Circle(1, origin, 5.0)
    |                       ^
    |

error[CS0246]: The type or namespace name 'StaticmethodAttribute' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpmk7f_bs4/math_utils.spy:16:10
    |
 16 |     rectangle: Rectangle = Rectangle(2, corner, 10.0, 20.0)
    |          ^
    |

error[CS0246]: The type or namespace name 'StaticmethodAttribute' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpmk7f_bs4/math_utils.spy:28:10
    |
 28 |             shape_type = "Circle"
    |          ^
    |

error[CS0117]: 'MathUtils.GeometryUtils' does not contain a definition for 'Pi'
  --> /tmp/tmpmk7f_bs4/math_utils.spy:25:44
    |
 25 |         shape_type: str = "Unknown"
    |                                    ^
    |

error[CS0117]: 'MathUtils.GeometryUtils' does not contain a definition for 'Pi'
  --> /tmp/tmpmk7f_bs4/shapes.spy:36:34
    |
 36 |     measurables.append(rectangle)
    |                                  ^
    |

error[CS0117]: 'MathUtils.GeometryUtils' does not contain a definition for 'Pi'
  --> /tmp/tmpmk7f_bs4/shapes.spy:40:41
    |
 40 |         measurement: tuple[float, str] = m.measure()
    |                                         ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Point2D' is never used
  --> /tmp/tmpmk7f_bs4/geometry_types.spy:2:34
    |
  2 | from shapes import Circle, Rectangle, ShapeBase
    |                                  ^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeBase' is never used
  --> /tmp/tmpmk7f_bs4/main.spy:2:39
    |
  2 | from shapes import Circle, Rectangle, ShapeBase
    |                                       ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmpmk7f_bs4/main.spy:3:49
    |
  3 | from geometry_types import IShape, IMeasurable, ShapeType
    |                                                 ^^^^^^^^^
    |


```

## Timing

- Generation: 329.06s
- Execution: 4.89s
