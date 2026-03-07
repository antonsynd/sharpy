# Issue Report: compilation_failed

**Timestamp:** 2026-03-07T07:40:15.725174
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage
from enums import Color, ShapeType
from interfaces import IRenderable, IMeasurable
from shapes import Shape, Circle, Rectangle, ShapeFactory
from structs import Point, Size
from utils import format_measurement, total_area, points_equal, color_info, ShapeAnalyzer

def main():
    # Test enum usage across modules
    print(color_info(Color.RED))

    # Create shapes using factory
    circle: Circle = ShapeFactory.create_circle(5.0)
    square: Rectangle = ShapeFactory.create_square(10.0)

    # Test polymorphic behavior with interface types
    renderables: list[IRenderable] = [circle, square]
    r: IRenderable

    # Virtual dispatch test - each render() should be called
    for r in renderables:
        print(r.render())

    # Test struct usage
    p1: Point = Point(3.0, 4.0)
    p2: Point = Point(0.0, 0.0)
    print(format_measurement(p1.distance_to(p2), "units"))

    # Test size struct
    size: Size = square.get_size()
    print(format_measurement(size.diagonal(), "diagonal"))

    # Test polymorphism through shape base class
    shapes: list[Shape] = [circle, square]
    s: Shape
    for s in shapes:
        print(s.describe())

    # Test interface-based calculations
    measurables: list[IMeasurable] = [circle, square]
    total: float = total_area(measurables)
    print(format_measurement(total, "sq units"))

    # Test analyzer with cross-module isinstance
    analyzer: ShapeAnalyzer = ShapeAnalyzer()
    result1: str = analyzer.analyze(circle)
    result2: str = analyzer.analyze(square)
    print(result1)
    print(result2)

    # Show analyzer counts
    print(analyzer.circles)
    print(analyzer.rectangles)

```

## Error

```
Assembly compilation failed:

error[CS1721]: Class 'Shapes.Shape' cannot have multiple base classes: 'Interfaces.IRenderable' and 'Interfaces.IMeasurable'
  --> shapes.cs:15:59
    |
 15 | 
    | ^
    |

error[CS1721]: Class 'Shapes.Shape' cannot have multiple base classes: 'Interfaces.IRenderable' and 'Interfaces.IPositioned'
  --> shapes.cs:15:83
    |
 15 | 
    | ^
    |

error[CS0533]: 'Shapes.Shape.GetColor()' hides inherited abstract member 'Interfaces.IRenderable.GetColor()'
  --> shapes.cs:19:28
    |
 19 | 
    | ^
    |

error[CS0115]: 'Shapes.Circle.Area()': no suitable method found to override
  --> /tmp/tmpmghzq110/shapes.spy:33:32
    |
 33 |     # Test polymorphism through shape base class
    |                                ^
    |

error[CS0115]: 'Shapes.Circle.Perimeter()': no suitable method found to override
  --> /tmp/tmpmghzq110/shapes.spy:40:32
    |
 40 |     measurables: list[IMeasurable] = [circle, square]
    |                                ^
    |

error[CS0534]: 'Shapes.Circle' does not implement inherited abstract member 'Interfaces.IRenderable.GetColor()'
  --> /tmp/tmpmghzq110/shapes.spy:30:18
    |
 30 |     size: Size = square.get_size()
    |                  ^
    |

error[CS0115]: 'Shapes.Rectangle.Area()': no suitable method found to override
  --> /tmp/tmpmghzq110/shapes.spy:41:32
    |
 41 |     total: float = total_area(measurables)
    |                                ^
    |

error[CS0115]: 'Shapes.Rectangle.Perimeter()': no suitable method found to override
  --> /tmp/tmpmghzq110/shapes.spy:68:32

error[CS0534]: 'Shapes.Rectangle' does not implement inherited abstract member 'Interfaces.IRenderable.GetColor()'
  --> /tmp/tmpmghzq110/shapes.spy:37:18
    |
 37 |         print(s.describe())
    |                  ^
    |

error[CS1061]: 'Shapes.Shape' does not contain a definition for 'X' and no accessible extension method 'X' accepting a first argument of type 'Shapes.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpmghzq110/shapes.spy:26:67
    |
 26 |     p2: Point = Point(0.0, 0.0)
    |                                ^
    |

error[CS1061]: 'Shapes.Shape' does not contain a definition for 'Y' and no accessible extension method 'Y' accepting a first argument of type 'Shapes.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpmghzq110/shapes.spy:26:79
    |
 26 |     p2: Point = Point(0.0, 0.0)
    |                                ^
    |

error[CS1950]: The best overloaded Add method 'List<Interfaces.IMeasurable>.Add(Interfaces.IMeasurable)' for the collection initializer has some invalid arguments
  --> /tmp/tmpmghzq110/main.spy:42:13
    |
 42 |     print(format_measurement(total, "sq units"))
    |             ^
    |

error[CS1503]: Argument 1: cannot convert from 'Shapes.Circle' to 'Interfaces.IMeasurable'
  --> /tmp/tmpmghzq110/main.spy:42:13
    |
 42 |     print(format_measurement(total, "sq units"))
    |             ^
    |

error[CS1950]: The best overloaded Add method 'List<Interfaces.IMeasurable>.Add(Interfaces.IMeasurable)' for the collection initializer has some invalid arguments
  --> /tmp/tmpmghzq110/main.spy:43:13
    |
 43 | 
    | ^
    |

error[CS1503]: Argument 1: cannot convert from 'Shapes.Rectangle' to 'Interfaces.IMeasurable'
  --> /tmp/tmpmghzq110/main.spy:43:13
    |
 43 | 
    | ^
    |

error[CS1729]: 'Shapes.Shape' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpmghzq110/shapes.spy:55:59

error[CS1729]: 'Shapes.Shape' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpmghzq110/shapes.spy:83:76


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmpmghzq110/shapes.spy:4:52
    |
  4 | from shapes import Shape, Circle, Rectangle, ShapeFactory
    |                                                    ^^^^^
    |

warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmpmghzq110/main.spy:2:26
    |
  2 | from enums import Color, ShapeType
    |                          ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'points_equal' is never used
  --> /tmp/tmpmghzq110/main.spy:6:51
    |
  6 | from utils import format_measurement, total_area, points_equal, color_info, ShapeAnalyzer
    |                                                   ^^^^^^^^^^^^
    |


```

## Timing

- Generation: 125.98s
- Execution: 4.58s
