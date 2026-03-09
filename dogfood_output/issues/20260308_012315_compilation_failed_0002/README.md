# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T01:21:30.788372
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# main.spy - Entry point demonstrating cross-module features
from types_module import Point, ShapeCategory, IScalable
from shapes_module import Circle, Rectangle, Shape
from utils_module import ShapeCollection, format_number

def demo_interface(scalable: IScalable) -> float:
    # Return area before scaling
    before: float = scalable.get_area()
    scalable.scale(2.0)
    after: float = scalable.get_area()
    return after

def main():
    # Create points using the struct from types_module
    origin: Point = Point(0.0, 0.0)
    p1: Point = Point(3.0, 4.0)

    # Test Point struct
    dist: float = origin.distance_to(p1)
    print(format_number(dist))
    print(format_number(origin.x))
    print(format_number(origin.y))

    # Test enum from types_module
    cat: ShapeCategory = ShapeCategory.TWO_DIMENSIONAL
    print(cat.name)
    print(format_number(float(cat.value)))

    # Create shapes from shapes_module
    circle: Circle = Circle(Point(0.0, 0.0), 5.0)
    rect: Rectangle = Rectangle(Point(1.0, 1.0), 3.0, 4.0)

    # Initial areas
    print(format_number(circle.get_area()))
    print(format_number(rect.get_area()))

    # Test polymorphism through interface (cross-module)
    circle_area_scaled: float = demo_interface(circle)
    print(format_number(circle_area_scaled))

    # rect_area_scaled: float = demo_interface(rect)
    # Collection from utils_module with shapes from shapes_module
    collection: ShapeCollection = ShapeCollection()
    collection.add(circle)
    collection.add(rect)

    # Total area after circle was scaled by 2x (area = 4x)
    total: float = collection.total_area()
    print(format_number(total))

    # Scale all by 1.5
    collection.scale_all(1.5)
    total_after: float = collection.total_area()
    print(format_number(total_after))

```

## Error

```
Assembly compilation failed:

error[CS0106]: The modifier 'virtual' is not valid for this item
  --> types_module.cs:28:31
    |
 28 | 
    | ^
    |

error[CS0136]: A local or parameter named 'Decimal' cannot be declared in this scope because that name is used in an enclosing local scope to define a local or parameter
  --> /tmp/tmpv3s6eedp/utils_module.spy:15:21
    |
 15 |     origin: Point = Point(0.0, 0.0)
    |                     ^
    |

error[CS0841]: Cannot use local variable 'Decimal' before it is declared
  --> /tmp/tmpv3s6eedp/utils_module.spy:15:61
    |
 15 |     origin: Point = Point(0.0, 0.0)
    |                                    ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmpv3s6eedp/utils_module.spy:2:16
    |
  2 | from types_module import Point, ShapeCategory, IScalable
    |                ^^^^^
    |

warning[SPY0452]: Imported name 'Circle' is never used
  --> /tmp/tmpv3s6eedp/utils_module.spy:2:55
    |
  2 | from types_module import Point, ShapeCategory, IScalable
    |                                                       ^^
    |

warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmpv3s6eedp/utils_module.spy:3:6
    |
  3 | from shapes_module import Circle, Rectangle, Shape
    |      ^^^^^^^^^
    |

warning[SPY0451]: Local variable 'before' is assigned but never used
  --> /tmp/tmpv3s6eedp/main.spy:8:5
    |
  8 |     before: float = scalable.get_area()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpv3s6eedp/main.spy:3:46
    |
  3 | from shapes_module import Circle, Rectangle, Shape
    |                                              ^^^^^
    |


```

## Timing

- Generation: 85.15s
- Execution: 5.21s
