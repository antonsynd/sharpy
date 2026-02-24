# Issue Report: compilation_failed

**Timestamp:** 2026-02-24T01:43:39.079699
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports and usage
# Imports from THREE different modules

from geometry import Shape, ShapeType
from shapes import Rectangle, Circle
from utils import Point, analyze_shapes, classify_shape

def create_shapes() -> list[Shape]:
    shapes: list[Shape] = []
    
    shapes.append(Circle(5.0, 0, 0))
    shapes.append(Circle(3.0, 10, 10))
    shapes.append(Rectangle(4.0, 4.0, 5, 5))
    shapes.append(Rectangle(6.0, 3.0, 15, 15))
    
    return shapes

def main():
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    distance: float = p1.distance_to(p2)
    
    shapes: list[Shape] = create_shapes()
    stats: dict[str, float] = analyze_shapes(shapes)
    
    print(f"Shape analysis demo")
    print(len(shapes))
    print(distance)
    print(stats["total_area"])
    print(stats["total_perimeter"])

# EXPECTED OUTPUT:
# Shape analysis demo
# 4
# 5.0
# 140.81406
# 84.26544
```

## Error

```
Assembly compilation failed:

error[CS0029]: Cannot implicitly convert type 'Sharpy.Dict<string, object>' to 'Sharpy.Dict<string, double>'
  --> /tmp/tmpu33bx1tr/utils.spy:28:16
    |
 28 |     print(distance)
    |                ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmpu33bx1tr/utils.spy:4:22
    |
  4 | from geometry import Shape, ShapeType
    |                      ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmpu33bx1tr/main.spy:4:29
    |
  4 | from geometry import Shape, ShapeType
    |                             ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'classify_shape' is never used
  --> /tmp/tmpu33bx1tr/main.spy:6:42
    |
  6 | from utils import Point, analyze_shapes, classify_shape
    |                                          ^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 903.67s
- Execution: 4.52s
