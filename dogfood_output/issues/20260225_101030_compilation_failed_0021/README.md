# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T09:56:21.610990
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point

from types_data import Color, Point, PI_APPROX
from geometry_base import Shape, IDrawable, IMeasurable
from shapes import Rectangle, Circle

def main():
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    p3: Point = Point(10.0, 10.0)

    dist: float = p1.distance_to(p2)
    print(dist)

    rect: Rectangle = Rectangle(5.0, 3.0, p1, Color.BLUE)
    circle: Circle = Circle(2.5, p3, Color.GREEN)

    r_desc: str = rect.describe()
    print(r_desc)

    c_desc: str = circle.describe()
    print(c_desc)

    r_draw: str = rect.draw()
    print(r_draw)

    c_draw: str = circle.draw()
    print(c_draw)

    r_area: float = rect.get_area()
    print(r_area)

    c_area: float = circle.get_area()
    print(c_area)

    r_perim: float = rect.get_perimeter()
    print(r_perim)

    is_blue: bool = rect.color == Color.BLUE
    print(is_blue)

# EXPECTED OUTPUT:
# 5.0
# Rectangle 5.0x3.0
# Circle radius 2.5
# Drawing rectangle at Point(0.0, 0.0)
# Drawing circle at Point(10.0, 10.0)
# 15.0
# 19.6349375
# 16.0
# True
```

## Error

```
Assembly compilation failed:

error[CS0506]: 'Shapes.Rectangle.Draw()': cannot override inherited member 'GeometryBase.Shape.Draw()' because it is not marked virtual, abstract, or override
  --> /tmp/tmp4syfr4lp/shapes.spy:24:32
    |
 24 |     r_draw: str = rect.draw()
    |                              ^
    |

error[CS0506]: 'Shapes.Circle.Draw()': cannot override inherited member 'GeometryBase.Shape.Draw()' because it is not marked virtual, abstract, or override
  --> /tmp/tmp4syfr4lp/shapes.spy:51:32
    |
 51 | # True
    |       ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmp4syfr4lp/geometry_base.spy:3:46
    |
  3 | from types_data import Color, Point, PI_APPROX
    |                                              ^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmp4syfr4lp/shapes.spy:5:3
    |
  5 | from shapes import Rectangle, Circle
    |   ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmp4syfr4lp/shapes.spy:5:14
    |
  5 | from shapes import Rectangle, Circle
    |              ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'PI_APPROX' is never used
  --> /tmp/tmp4syfr4lp/main.spy:3:38
    |
  3 | from types_data import Color, Point, PI_APPROX
    |                                      ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmp4syfr4lp/main.spy:4:27
    |
  4 | from geometry_base import Shape, IDrawable, IMeasurable
    |                           ^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmp4syfr4lp/main.spy:4:34
    |
  4 | from geometry_base import Shape, IDrawable, IMeasurable
    |                                  ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmp4syfr4lp/main.spy:4:45
    |
  4 | from geometry_base import Shape, IDrawable, IMeasurable
    |                                             ^^^^^^^^^^^
    |


```

## Timing

- Generation: 819.58s
- Execution: 4.23s
