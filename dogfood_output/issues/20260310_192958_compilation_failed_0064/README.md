# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T19:26:14.232461
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage
from concrete_shapes import Circle, Rectangle
from shapes import Shape
from geometry_types import ShapeType, IMeasurable

type ShapeList = list[Shape]

def get_total_measure(shapes: ShapeList) -> float:
    total: float = 0.0
    for s in shapes:
        total = total + s.measure()
    return total

def main():
    c: Circle = Circle(5.0)
    r: Rectangle = Rectangle(4.0, 6.0)
    
    all_shapes: ShapeList = [c, r]
    
    # Print 1: enum .name - CIRCLE -> "Circle"
    print(c.get_type().name)
    
    # Print 2: rectangle description
    print(r.describe())
    
    # Print 3: total measure of all shapes
    # Circle: 3.14159 * 5.0 * 5.0 = 78.53975
    # Rectangle: 4.0 * 6.0 = 24.0
    # Total: 102.53975
    total: float = get_total_measure(all_shapes)
    print(total)
    
    # Print 4: number of shapes
    print(len(all_shapes))
    
    # Print 5: measure first shape via interface
    first: IMeasurable = all_shapes[0]
    print(first.measure())

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'GeometryTypes.ShapeType' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'GeometryTypes.ShapeType' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmppnqbe15r/main.spy:21:51
    |
 21 |     print(c.get_type().name)
    |                             ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmppnqbe15r/main.spy:4:28
    |
  4 | from geometry_types import ShapeType, IMeasurable
    |                            ^^^^^^^^^
    |


```

## Timing

- Generation: 205.03s
- Execution: 5.16s
