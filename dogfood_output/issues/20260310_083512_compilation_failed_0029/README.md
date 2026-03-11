# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T08:32:35.614831
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
from math_utils import MathUtils, Statistics
from geometric_shapes import Point, Rectangle, Circle, ShapeCollection, Shape
from config import ShapeType, Color, Dimensions, Config

def main():
    # Test 1: Math utilities
    print("=== Math Utils Test ===")
    numbers: list[float] = [10.0, 20.0, 30.0, 40.0, 50.0]
    total: float = MathUtils.sum(numbers)
    avg: float = MathUtils.average(numbers)
    print(total)
    print(avg)
    
    # Test 2: Statistics class
    print("=== Statistics Test ===")
    stats: Statistics = Statistics()
    stats.add(5.0)
    stats.add(15.0)
    stats.add(10.0)
    print(stats.mean())
    print(stats.max_val())
    print(stats.min_val())
    
    # Test 3: Point operations
    print("=== Point Test ===")
    p: Point = Point(3.0, 4.0)
    print(p.distance_from_origin())
    
    # Test 4: Shape hierarchy with cross-module inheritance
    print("=== Shape Test ===")
    rect: Rectangle = Rectangle(5.0, 3.0)
    circle: Circle = Circle(2.0)
    print(rect.area())
    print(rect.perimeter())
    print(circle.area())
    print(circle.perimeter())
    
    # Test 5: Shape collection
    print("=== Collection Test ===")
    collection: ShapeCollection = ShapeCollection()
    collection.add(rect)
    collection.add(circle)
    print(collection.total_area())
    print(collection.total_perimeter())
    
    # Test 6: Enum values
    print("=== Enum Test ===")
    s_type: ShapeType = ShapeType.CIRCLE
    color: Color = Color.GREEN
    print(s_type.value)
    print(color.value)
    
    # Test 7: Struct
    print("=== Struct Test ===")
    dim: Dimensions = Dimensions(4.0, 6.0)
    print(dim.area())
    
    # Test 8: Config class
    print("=== Config Test ===")
    supported: list[str] = Config.get_supported_shapes()
    for s in supported:
        print(s)

```

## Error

```
Assembly compilation failed:

error[CS0708]: 'MathUtils.PI': cannot declare instance members in a static class
  --> math_utils.cs:12:19
    |
 12 |     print(avg)
    |               ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'geometric_shapes' is never used
  --> /tmp/tmpwa0rvyyk/math_utils.spy:2:14
    |
  2 | from geometric_shapes import Point, Rectangle, Circle, ShapeCollection, Shape
    |              ^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpwa0rvyyk/main.spy:2:73
    |
  2 | from geometric_shapes import Point, Rectangle, Circle, ShapeCollection, Shape
    |                                                                         ^^^^^
    |


```

## Timing

- Generation: 138.17s
- Execution: 4.96s
