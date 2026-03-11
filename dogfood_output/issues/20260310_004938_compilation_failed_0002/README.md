# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T00:46:57.854124
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests cross-module class features

from shapes import Shape, Rectangle, Circle, DrawablePoint, IDrawable
from utils import calculate_total_area, create_default_rectangle, describe_all

def main():
    # Create shapes from imported classes
    rect: Rectangle = Rectangle("R1", 4.0, 6.0)
    circle: Circle = Circle("C1", 3.0)
    point: DrawablePoint = DrawablePoint("P1", 1.0, 2.0)
    
    # Test 1: Individual area calculations
    print(rect.area())
    print(circle.area())
    
    # Test 2: Polymorphic list and total area calculation
    shapes: list[Shape] = [rect, circle, point]
    total: float = calculate_total_area(shapes)
    print(total)
    
    # Test 3: Get descriptions through utility function
    descs: list[str] = describe_all(shapes)
    for d in descs:
        print(d)
    
    # Test 4: Interface method across module
    drawable: IDrawable = point
    print(drawable.draw())
    
    # Test 5: Static field access and utility function
    default_rect: Rectangle = create_default_rectangle()
    print(default_rect.area())

```

## Error

```
Assembly compilation failed:

error[CS0117]: 'Shapes.Circle' does not contain a definition for 'Pi'
  --> /tmp/tmpnwo4cne6/shapes.spy:48:27


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmpnwo4cne6/utils.spy:3:25
    |
  3 | from shapes import Shape, Rectangle, Circle, DrawablePoint, IDrawable
    |                         ^^^^^^^^^
    |


```

## Timing

- Generation: 143.47s
- Execution: 5.10s
