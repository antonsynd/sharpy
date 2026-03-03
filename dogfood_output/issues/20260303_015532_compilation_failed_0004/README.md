# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T01:42:20.401603
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from all modules
from structures import Status, Point, Config
from shapes import Rectangle, Circle, IShape, IDrawable
from processors import ShapeProcessor

def main():
    # Create points using struct from structures
    origin: Point = Point(0.0, 0.0, 0.0)
    offset: Point = Point(1.0, 2.0, 3.0)

    # Create shapes from shapes module
    rect: Rectangle = Rectangle(origin, 4.0, 5.0)
    circle: Circle = Circle(offset, 2.0)

    # Calculate individual areas
    print(rect.area())
    print(circle.area())

    # Process shapes individually (avoiding invariant generic list assignment)
    shapes_list: list[float] = []
    shapes_list.append(rect.area())
    shapes_list.append(circle.area())
    print(len(shapes_list))

    # Calculate total area
    total: float = shapes_list[0] + shapes_list[1]
    print(total)

    # Access enum from structures
    print(Status.ACTIVE.value)

    # Access static field from structures
    print(Config.MAX_ITEMS)

    # Use IDrawable interface via processor (process individually)
    desc1: str = ShapeProcessor.get_drawable_description(rect)
    desc2: str = ShapeProcessor.get_drawable_description(circle)
    print(desc1)
    print(desc2)

```

## Error

```
Assembly compilation failed:

error[CS0117]: 'Structures.Config' does not contain a definition for 'MaxItems'
  --> /tmp/tmpe8831wot/main.spy:33:46
    |
 33 |     print(Config.MAX_ITEMS)
    |                            ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmpe8831wot/main.spy:3:39
    |
  3 | from shapes import Rectangle, Circle, IShape, IDrawable
    |                                       ^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmpe8831wot/main.spy:3:47
    |
  3 | from shapes import Rectangle, Circle, IShape, IDrawable
    |                                               ^^^^^^^^^
    |


```

## Timing

- Generation: 746.35s
- Execution: 4.74s
