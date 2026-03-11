# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T18:30:05.298326
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates complex cross-module imports
# Combines types_module, shapes_module, and utils_module

from types_module import Color, Point, IShape
from shapes_module import Rectangle, Circle, Square
from utils_module import ShapeTracker, create_point, create_default_color, format_area

def process_shape(s: IShape) -> None:
    print(s.describe())
    print(format_area("Shape", s.area()))

def main():
    # Reset and start tracking
    ShapeTracker.reset()
    print(f"Shapes created: {ShapeTracker.get_count()}")

    # Create points (uses utils_module which uses types_module)
    p1: Point = create_point(3.0, 4.0)
    p2: Point = create_point(0.0, 0.0)
    print(f"Point distance: {p1.distance_from_origin():.1f}")

    # Create shapes with different colors
    rect: Rectangle = Rectangle(5.0, 3.0, Color.RED)
    ShapeTracker.increment()

    circle: Circle = Circle(2.0, p2)
    ShapeTracker.increment()

    square: Square = Square(4.0, create_default_color())
    ShapeTracker.increment()

    print(f"Shapes created: {ShapeTracker.get_count()}")

    # Process shapes through interface
    print(f"Rectangle color: {rect.get_color_name()}")
    print(f"Rectangle area: {rect.area()}")

    # Scale and check
    circle.scale(2.0)
    print(f"Scaled circle area: {circle.area():.2f}")

    # Square inherits from Rectangle
    square.scale(0.5)
    print(f"Scaled square area: {square.area()}")

```

## Error

```
Assembly compilation failed:

error[CS0708]: 'UtilsModule.ShapeTracker.ShapesCreated': cannot declare instance members in a static class
  --> utils_module.cs:15:20
    |
 15 |     print(f"Shapes created: {ShapeTracker.get_count()}")
    |                    ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'UtilsModule.ShapeTracker.ShapesCreated'
  --> /tmp/tmp_vm19_8l/utils_module.spy:12:13
    |
 12 | def main():
    |            ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'UtilsModule.ShapeTracker.ShapesCreated'
  --> /tmp/tmp_vm19_8l/utils_module.spy:12:42
    |
 12 | def main():
    |            ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'UtilsModule.ShapeTracker.ShapesCreated'
  --> /tmp/tmp_vm19_8l/utils_module.spy:16:20
    |
 16 | 
    | ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'UtilsModule.ShapeTracker.ShapesCreated'
  --> /tmp/tmp_vm19_8l/utils_module.spy:20:13
    |
 20 |     print(f"Point distance: {p1.distance_from_origin():.1f}")
    |             ^
    |

error[CS1061]: 'TypesModule.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'TypesModule.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp_vm19_8l/shapes_module.spy:30:35
    |
 30 |     ShapeTracker.increment()
    |                             ^
    |


```

## Timing

- Generation: 138.54s
- Execution: 5.01s
