# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T13:53:18.117534
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module polymorphism
from geometry import Point
from concrete_shapes import Circle, Rectangle
from shapes import IShape, ShapeType

def print_shape(s: IShape) -> None:
    print(f"Area: {s.area()}")
    print(f"Perimeter: {s.perimeter()}")

def main():
    # Create a Point from geometry module
    p: Point = Point(3.0, 4.0)
    print(f"Point: {p}")

    # Create Circle - cross-module inheritance
    print("Circle:")
    c: Circle = Circle(5.0)
    print(f"Radius: {c.radius}")
    print(f"Area: {c.area()}")
    print(f"Perimeter: {c.perimeter()}")

    # Create Rectangle - cross-module inheritance
    print("Rectangle:")
    r: Rectangle = Rectangle(3.0, 4.0)
    print(f"Dimensions: {r.width} x {r.height}")
    print(f"Area: {r.area()}")
    print(f"Perimeter: {r.perimeter()}")

    # Show enum value from shapes module
    print(f"Circle type: {str(c.shape_type)}")
    print(f"Rectangle type: {str(r.shape_type)}")

    # Polymorphic dispatch through interface
    print("Via interface (Circle):")
    print_shape(c)
    print("Via interface (Rectangle):")
    print_shape(r)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.ShapeBase' does not contain a definition for 'Center' and no accessible extension method 'Center' accepting a first argument of type 'Shapes.ShapeBase' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpxzw2k8qf/shapes.spy:27:66
    |
 27 |     print(f"Perimeter: {r.perimeter()}")
    |                                         ^
    |

error[CS1729]: 'Shapes.ShapeBase' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpxzw2k8qf/concrete_shapes.spy:27:40
    |
 27 |     print(f"Perimeter: {r.perimeter()}")
    |                                        ^
    |

error[CS1729]: 'Shapes.ShapeBase' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpxzw2k8qf/concrete_shapes.spy:48:57


```

## Compiler Output

```
warning[SPY0452]: Imported name 'geometry' is never used
  --> /tmp/tmpxzw2k8qf/shapes.spy:2:13
    |
  2 | from geometry import Point
    |             ^^^^^^^^
    |

warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmpxzw2k8qf/concrete_shapes.spy:3:29
    |
  3 | from concrete_shapes import Circle, Rectangle
    |                             ^^^^^^
    |

warning[SPY0452]: Imported name 'Dimensions' is never used
  --> /tmp/tmpxzw2k8qf/concrete_shapes.spy:4:18
    |
  4 | from shapes import IShape, ShapeType
    |                  ^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmpxzw2k8qf/main.spy:4:28
    |
  4 | from shapes import IShape, ShapeType
    |                            ^^^^^^^^^
    |


```

## Timing

- Generation: 283.79s
- Execution: 5.07s
