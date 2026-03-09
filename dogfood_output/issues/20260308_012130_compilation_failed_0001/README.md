# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T01:20:27.790498
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module inheritance and complex imports

from shapes import Shape, ShapeUtils, IMeasurable, ITransformable
from utils import Color, Point, ColorUtility, Constants
from models import Circle, Rectangle, Square

def main():
    # Create points using the struct from utils module
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    
    # Distance between points (from struct method)
    dist: float = p1.distance_to(p2)
    print(dist)
    
    # Create shapes from models module (inherit from shapes module)
    circle: Circle = Circle("c1", Point(0.0, 0.0), 5.0)
    rect: Rectangle = Rectangle("r1", Point(10.0, 10.0), 4.0, 6.0)
    square: Square = Square("s1", Point(20.0, 20.0), 3.0)
    
    # Test cross-module inheritance - area methods from concrete implementations
    print(circle.area())
    print(rect.area())
    print(square.area())
    
    # Test interface implementation (ITransformable from shapes)
    circle.scale(2.0)
    print(circle.area())
    
    # Test comparison using utils from shapes module
    print(ShapeUtils.compare_areas(circle, rect))
    
    # Test enum usage from utils
    mixed: Color = ColorUtility.mix(Color.RED, Color.BLUE)
    print(ColorUtility.get_color_name(mixed))
    
    # Test polymorphic draw (Drawable interface)
    shapes: list[Shape] = [circle, rect, square]
    for s in shapes:
        print(s.name)
    
    # Verify subtyping works across modules
    print(square.perimeter())

```

## Error

```
Assembly compilation failed:

error[CS0708]: 'Utils.Constants.PI': cannot declare instance members in a static class
  --> utils.cs:22:23
    |
 22 |     print(circle.area())
    |                       ^
    |

error[CS1721]: Class 'Models.Circle' cannot have multiple base classes: 'Shapes.Shape' and 'Shapes.Drawable'
  --> models.cs:14:41
    |
 14 |     print(dist)
    |                ^
    |

error[CS1721]: Class 'Models.Rectangle' cannot have multiple base classes: 'Shapes.Shape' and 'Shapes.Drawable'
  --> /tmp/tmpp3i4h4b1/models.spy:20:44
    |
 20 |     
    |     ^
    |

error[CS0534]: 'Models.Circle' does not implement inherited abstract member 'Shapes.Shape.Perimeter()'
  --> models.cs:14:18
    |
 14 |     print(dist)
    |                ^
    |

error[CS0534]: 'Models.Rectangle' does not implement inherited abstract member 'Shapes.Shape.Perimeter()'
  --> /tmp/tmpp3i4h4b1/models.spy:20:18
    |
 20 |     
    |     ^
    |

error[CS0117]: 'Utils.Constants' does not contain a definition for 'Pi'
  --> /tmp/tmpp3i4h4b1/models.spy:19:30
    |
 19 |     square: Square = Square("s1", Point(20.0, 20.0), 3.0)
    |                              ^
    |

error[CS0117]: 'Utils.Constants' does not contain a definition for 'Pi'
  --> /tmp/tmpp3i4h4b1/models.spy:22:37
    |
 22 |     print(circle.area())
    |                         ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'ShapeUtils' is never used
  --> /tmp/tmpp3i4h4b1/models.spy:3:22
    |
  3 | from shapes import Shape, ShapeUtils, IMeasurable, ITransformable
    |                      ^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmpp3i4h4b1/main.spy:3:39
    |
  3 | from shapes import Shape, ShapeUtils, IMeasurable, ITransformable
    |                                       ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ITransformable' is never used
  --> /tmp/tmpp3i4h4b1/main.spy:3:52
    |
  3 | from shapes import Shape, ShapeUtils, IMeasurable, ITransformable
    |                                                    ^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Constants' is never used
  --> /tmp/tmpp3i4h4b1/main.spy:4:47
    |
  4 | from utils import Color, Point, ColorUtility, Constants
    |                                               ^^^^^^^^^
    |


```

## Timing

- Generation: 43.79s
- Execution: 5.01s
