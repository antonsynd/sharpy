# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T03:53:46.064019
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates shape utilities with inheritance

from shape_utils import Rectangle, Circle, compare_shapes

def main():
    # Create shapes
    rect: Rectangle = Rectangle(6.0, 5.0)
    circle: Circle = Circle(3.0)
    
    # Calculate and print shape properties
    print(rect.area())
    print(rect.perimeter())
    print(circle.area())
    print(circle.perimeter())
    
    # Compare shapes using polymorphic function
    larger: str = compare_shapes(rect, circle)
    print(larger)

```

## Error

```
Assembly compilation failed:

error[CS0117]: 'ShapeUtils.Circle' does not contain a definition for 'Pi'
  --> /tmp/tmpaen1_7g9/shape_utils.spy:38:27

error[CS0117]: 'ShapeUtils.Circle' does not contain a definition for 'Pi'
  --> /tmp/tmpaen1_7g9/shape_utils.spy:42:34


```

## Timing

- Generation: 258.15s
- Execution: 4.61s
