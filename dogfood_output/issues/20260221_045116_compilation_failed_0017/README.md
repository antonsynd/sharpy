# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T04:48:55.869708
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates complex multi-file imports
from geometry_types import Point, IShape, ShapeRectangle, ShapeCircle
from math_utils import PI, square, hypotenuse, clamp, Operation, apply_operation
from shape_registry import ShapeRegistry, create_rectangle, create_circle

def main():
    # Test basic math utilities
    a: float = 3.0
    b: float = 4.0
    c: float = hypotenuse(a, b)
    print(c)
    print(square(5.0))

    # Test clamp function
    val: float = 150.0
    print(clamp(val, 0.0, 100.0))

    # Test enum and operation
    result: float = apply_operation(10.0, 5.0, Operation.MULTIPLY)
    print(result)

    # Test structs and point distance
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    dist: float = p1.distance_to(p2)
    print(dist)

    # Test shapes and registry
    registry: ShapeRegistry = ShapeRegistry()

    rect: ShapeRectangle = create_rectangle(5.0, 3.0)
    circ: ShapeCircle = create_circle(2.0)

    registry.add(rect)
    registry.add(circ)

    # Print shape info
    print(rect.area())
    print(circ.area())
    print(registry.total_area())
    print(registry.count())

# EXPECTED OUTPUT:
# 5.0
# 25.0
# 100.0
# 50.0
# 5.0
# 15.0
# 12.566
# 27.566
# 2
```

## Error

```
Assembly compilation failed:

error[CS0509]: 'GeometryTypes.ShapeRectangle': cannot derive from sealed type 'GeometryTypes.Rectangle'
  --> /tmp/tmpwmapnnpc/geometry_types.spy:28:35
    |
 28 |     # Test shapes and registry
    |                               ^
    |

error[CS0509]: 'GeometryTypes.ShapeCircle': cannot derive from sealed type 'GeometryTypes.Circle'
  --> /tmp/tmpwmapnnpc/geometry_types.spy:44:32
    |
 44 | # 5.0
    |      ^
    |

error[CS1061]: 'GeometryTypes.ShapeCircle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'GeometryTypes.ShapeCircle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpwmapnnpc/geometry_types.spy:44:37
    |
 44 | # 5.0
    |      ^
    |

error[CS1729]: 'GeometryTypes.Point' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpwmapnnpc/main.spy:23:38
    |
 23 |     p1: Point = Point(0.0, 0.0)
    |                                ^
    |

error[CS1729]: 'GeometryTypes.Point' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpwmapnnpc/main.spy:24:38
    |
 24 |     p2: Point = Point(3.0, 4.0)
    |                                ^
    |

error[CS1061]: 'GeometryTypes.ShapeRectangle' does not contain a definition for 'Width' and no accessible extension method 'Width' accepting a first argument of type 'GeometryTypes.ShapeRectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpwmapnnpc/geometry_types.spy:28:25
    |
 28 |     # Test shapes and registry
    |                         ^
    |

error[CS1061]: 'GeometryTypes.ShapeRectangle' does not contain a definition for 'Height' and no accessible extension method 'Height' accepting a first argument of type 'GeometryTypes.ShapeRectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpwmapnnpc/geometry_types.spy:28:38
    |
 28 |     # Test shapes and registry
    |                               ^
    |

error[CS1061]: 'GeometryTypes.ShapeRectangle' does not contain a definition for 'Width' and no accessible extension method 'Width' accepting a first argument of type 'GeometryTypes.ShapeRectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpwmapnnpc/geometry_types.spy:31:33
    |
 31 |     rect: ShapeRectangle = create_rectangle(5.0, 3.0)
    |                                 ^
    |

error[CS1061]: 'GeometryTypes.ShapeRectangle' does not contain a definition for 'Height' and no accessible extension method 'Height' accepting a first argument of type 'GeometryTypes.ShapeRectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpwmapnnpc/geometry_types.spy:31:46
    |
 31 |     rect: ShapeRectangle = create_rectangle(5.0, 3.0)
    |                                              ^
    |

error[CS1061]: 'GeometryTypes.ShapeCircle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'GeometryTypes.ShapeCircle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpwmapnnpc/geometry_types.spy:47:37
    |
 47 | # 50.0
    |       ^
    |

error[CS1729]: 'GeometryTypes.ShapeRectangle' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpwmapnnpc/shape_registry.spy:37:20
    |
 37 |     # Print shape info
    |                    ^
    |

error[CS1729]: 'GeometryTypes.ShapeCircle' does not contain a constructor that takes 1 arguments
  --> /tmp/tmpwmapnnpc/shape_registry.spy:40:20
    |
 40 |     print(registry.total_area())
    |                    ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmpwmapnnpc/shape_registry.spy:2:33
    |
  2 | from geometry_types import Point, IShape, ShapeRectangle, ShapeCircle
    |                                 ^^^^^
    |

warning[SPY0452]: Imported name 'Operation' is never used
  --> /tmp/tmpwmapnnpc/shape_registry.spy:2:62
    |
  2 | from geometry_types import Point, IShape, ShapeRectangle, ShapeCircle
    |                                                              ^^^^^^^^
    |

warning[SPY0452]: Imported name 'apply_operation' is never used
  --> /tmp/tmpwmapnnpc/shape_registry.spy:3:3
    |
  3 | from math_utils import PI, square, hypotenuse, clamp, Operation, apply_operation
    |   ^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmpwmapnnpc/main.spy:2:35
    |
  2 | from geometry_types import Point, IShape, ShapeRectangle, ShapeCircle
    |                                   ^^^^^^
    |

warning[SPY0452]: Imported name 'PI' is never used
  --> /tmp/tmpwmapnnpc/main.spy:3:24
    |
  3 | from math_utils import PI, square, hypotenuse, clamp, Operation, apply_operation
    |                        ^^
    |


```

## Timing

- Generation: 122.26s
- Execution: 4.73s
