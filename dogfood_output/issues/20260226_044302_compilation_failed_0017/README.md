# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T04:34:37.095721
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Module: main
# Entry point - tests cross-module class inheritance and polymorphism
from utils import Color, Point, GeometryUtils
from shapes import Shape, Circle, Rectangle, Triangle, IDrawable
from renderer import SceneRenderer, CircleFactory, RectangleFactory

def test_polymorphism(shapes: list[Shape]) -> None:
    for shape in shapes:
        print(shape.describe())

def main():
    # Create various shapes
    c1 = Circle("Sun", Color.YELLOW, Point(0.0, 0.0), 10.0)
    c2 = Circle("Moon", Color.BLUE, Point(50.0, 50.0), 5.0)
    r1 = Rectangle("Field", Color.GREEN, Point(10.0, 10.0), 100.0, 50.0)
    t1 = Triangle("Arrow", Color.RED, Point(0.0, 0.0), Point(5.0, 10.0), Point(10.0, 0.0))

    # Create renderer and add shapes
    renderer = SceneRenderer()
    renderer.add_shape(c1)
    renderer.add_shape(c2)
    renderer.add_shape(r1)
    renderer.add_shape(t1)

    # Test 1: Polymorphic draw calls
    drawings = renderer.render_all()
    for drawing in drawings:
        print(drawing)

    # Test 2: Calculate total area
    total: float = renderer.total_area()
    print(f"Total area: {total}")

    # Test 3: Count shapes by color
    red_count: int = renderer.count_by_color(Color.RED)
    print(f"Red shapes: {red_count}")

    # Test 4: Test factories from renderer module
    cf = CircleFactory()
    rf = RectangleFactory()
    factory_circle = cf.create_shape("FactoryCircle")
    factory_rect = rf.create_shape("FactoryRect")
    print(factory_circle.name)
    print(factory_rect.name)

    # Test 5: Direct polymorphic list
    shapes: list[Shape] = [c1, r1, t1]
    test_polymorphism(shapes)
```

## Error

```
Assembly compilation failed:

error[CS0113]: A member 'Utils.Point.ToString()' marked as override cannot be marked as new or virtual
  --> /tmp/tmpcbs4qb0b/utils.spy:26:40
    |
 26 |     drawings = renderer.render_all()
    |                                     ^
    |

error[CS0117]: 'Utils.GeometryUtils' does not contain a definition for 'Pi'
  --> /tmp/tmpcbs4qb0b/utils.spy:35:34
    |
 35 |     red_count: int = renderer.count_by_color(Color.RED)
    |                                  ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Triangle' is never used
  --> /tmp/tmpcbs4qb0b/renderer.spy:3:24
    |
  3 | from utils import Color, Point, GeometryUtils
    |                        ^^^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmpcbs4qb0b/renderer.spy:3:34
    |
  3 | from utils import Color, Point, GeometryUtils
    |                                  ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IHasArea' is never used
  --> /tmp/tmpcbs4qb0b/renderer.spy:3:45
    |
  3 | from utils import Color, Point, GeometryUtils
    |                                             ^
    |

warning[SPY0452]: Imported name 'GeometryUtils' is never used
  --> /tmp/tmpcbs4qb0b/main.spy:3:33
    |
  3 | from utils import Color, Point, GeometryUtils
    |                                 ^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmpcbs4qb0b/main.spy:4:56
    |
  4 | from shapes import Shape, Circle, Rectangle, Triangle, IDrawable
    |                                                        ^^^^^^^^^
    |


```

## Timing

- Generation: 471.98s
- Execution: 4.47s
