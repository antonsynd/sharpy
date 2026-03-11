# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T20:13:08.328870
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from multiple modules
from shapes import Shape, Polygon, Triangle, Square, IDrawable, IMeasurable
from utils import ShapeCategory, format_measurement
from geometry import Point, Rectangle, Circle

def test_shapes():
    tri: Triangle = Triangle(5.0, 4.0, 3.0, 4.0, 5.0)
    square: Square = Square(4.0)
    
    print(tri.name)
    print(square.name)
    
    print(tri.describe())
    print(square.describe())
    
    print(tri.display())
    print(square.display())
    
    tri_area: float = tri.get_area()
    sq_area: float = square.get_area()
    print(format_measurement(tri_area, "sq units"))
    print(format_measurement(sq_area, "sq units"))
    
    print(tri.draw())
    print(square.draw())

def test_geometry():
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    
    dist: float = p1.distance_to(p2)
    print(str(dist))
    
    rect: Rectangle = Rectangle(5.0, 3.0)
    print(format_measurement(rect.area(), "sq units"))
    
    circ: Circle = Circle(2.5)
    print(format_measurement(circ.area(), "sq units"))
    print(format_measurement(circ.circumference(), "units"))

def test_categories():
    rect: Rectangle = Rectangle(4.0, 6.0)
    cat: ShapeCategory = rect.category()
    print(cat.name)

def main():
    test_shapes()
    test_geometry()
    test_categories()

```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'sides' does not exist in the current context
  --> /tmp/tmpb71kgzhy/shapes.spy:70:115

error[CS0103]: The name 'sides' does not exist in the current context
  --> /tmp/tmpb71kgzhy/shapes.spy:88:53


```

## Compiler Output

```
warning[SPY0452]: Imported name 'ShapeCategory' is never used
  --> /tmp/tmpb71kgzhy/shapes.spy:2:21
    |
  2 | from shapes import Shape, Polygon, Triangle, Square, IDrawable, IMeasurable
    |                     ^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'format_measurement' is never used
  --> /tmp/tmpb71kgzhy/shapes.spy:2:36
    |
  2 | from shapes import Shape, Polygon, Triangle, Square, IDrawable, IMeasurable
    |                                    ^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'calculate_perimeter' is never used
  --> /tmp/tmpb71kgzhy/geometry.spy:2:34
    |
  2 | from shapes import Shape, Polygon, Triangle, Square, IDrawable, IMeasurable
    |                                  ^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpb71kgzhy/main.spy:2:20
    |
  2 | from shapes import Shape, Polygon, Triangle, Square, IDrawable, IMeasurable
    |                    ^^^^^
    |

warning[SPY0452]: Imported name 'Polygon' is never used
  --> /tmp/tmpb71kgzhy/main.spy:2:27
    |
  2 | from shapes import Shape, Polygon, Triangle, Square, IDrawable, IMeasurable
    |                           ^^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmpb71kgzhy/main.spy:2:54
    |
  2 | from shapes import Shape, Polygon, Triangle, Square, IDrawable, IMeasurable
    |                                                      ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmpb71kgzhy/main.spy:2:65
    |
  2 | from shapes import Shape, Polygon, Triangle, Square, IDrawable, IMeasurable
    |                                                                 ^^^^^^^^^^^
    |


```

## Timing

- Generation: 141.49s
- Execution: 5.16s
