# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T15:47:06.667277
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports and usage

from shapes import ShapeType, IShape, IColorable, Circle, Rectangle
from utils import Point, format_number, calculate_statistics, shape_type_to_string
from collections import ShapeCollection, Point3D

def main():
    # Test 1: Create shapes and calculate properties
    circle: Circle = Circle(5.0)
    rect: Rectangle = Rectangle(4.0, 6.0)
    print(circle.area())
    print(rect.perimeter())

    # Test 2: Use interface functionality
    circle.set_color("red")
    colorable: IColorable = circle
    print(colorable.get_color())

    # Test 3: Use struct from utils
    pt: Point = Point(3.0, 4.0)
    print(pt.distance_from_origin())

    # Test 4: Use shape collection from collections
    collection: ShapeCollection = ShapeCollection("MyShapes")
    collection.add(circle)
    collection.add(rect)
    print(collection.total_area())
    print(format_number(collection.total_perimeter(), 2))

    # Test 5: Use Point3D (inherits from Point across modules)
    pt3d: Point3D = Point3D(1.0, 2.0, 2.0)
    print(pt3d.distance_from_origin())

    # Test 6: Enum to string conversion
    print(shape_type_to_string(1))

```

## Error

```
Assembly compilation failed:

error[CS0509]: 'Collections.Point3D': cannot derive from sealed type 'Utils.Point'
  --> /tmp/tmpmgk1qagm/collections.spy:16:28
    |
 16 |     colorable: IColorable = circle
    |                            ^
    |

error[CS1061]: 'Collections.Point3D' does not contain a definition for 'X' and no accessible extension method 'X' accepting a first argument of type 'Collections.Point3D' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpmgk1qagm/collections.spy:46:42

error[CS1061]: 'Collections.Point3D' does not contain a definition for 'X' and no accessible extension method 'X' accepting a first argument of type 'Collections.Point3D' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpmgk1qagm/collections.spy:46:51

error[CS1061]: 'Collections.Point3D' does not contain a definition for 'Y' and no accessible extension method 'Y' accepting a first argument of type 'Collections.Point3D' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpmgk1qagm/collections.spy:46:60

error[CS1061]: 'Collections.Point3D' does not contain a definition for 'Y' and no accessible extension method 'Y' accepting a first argument of type 'Collections.Point3D' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpmgk1qagm/collections.spy:46:69

error[CS1729]: 'object' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpmgk1qagm/collections.spy:49:56

error[CS1061]: 'Shapes.ShapeType' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'Shapes.ShapeType' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpmgk1qagm/shapes.spy:27:43
    |
 27 |     print(collection.total_area())
    |                                   ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmpmgk1qagm/main.spy:3:20
    |
  3 | from shapes import ShapeType, IShape, IColorable, Circle, Rectangle
    |                    ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmpmgk1qagm/main.spy:3:31
    |
  3 | from shapes import ShapeType, IShape, IColorable, Circle, Rectangle
    |                               ^^^^^^
    |

warning[SPY0452]: Imported name 'calculate_statistics' is never used
  --> /tmp/tmpmgk1qagm/main.spy:4:41
    |
  4 | from utils import Point, format_number, calculate_statistics, shape_type_to_string
    |                                         ^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 639.46s
- Execution: 5.12s
