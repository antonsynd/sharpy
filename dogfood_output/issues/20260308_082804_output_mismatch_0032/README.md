# Issue Report: output_mismatch

**Timestamp:** 2026-03-08T08:24:54.923258
**Type:** output_mismatch
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates complex module imports

from shapes import Shape, IDrawable, IMeasurable
from geometry import ShapeType, Point, Color
from renderers import Circle, Rectangle, ShapeFactory

def main():
    # Test enum usage
    st: ShapeType = ShapeType.CIRCLE
    print(st.name)
    
    # Create shapes using factory
    circle: Circle = ShapeFactory.create_circle(0.0, 0.0, 5.0)
    rect: Rectangle = ShapeFactory.create_rectangle(1.0, 1.0, 10.0, 5.0)
    
    # Draw shapes (IDrawable interface)
    print(circle.draw())
    print(rect.draw())
    
    # Calculate area and perimeter (IMeasurable interface)
    print(circle.area())
    print(rect.area())
    print(circle.perimeter())
    print(rect.perimeter())
    
    # Test Point struct
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    dist: float = p1.distance_to(p2)
    print(dist)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Circle
Drawing Circle at (0.0, 0.0) with radius 5.0
Drawing Rectangle at (1.0, 1.0), 10.0 x 5.0
78.53975
50.0
31.4159
30.0
5.0

```

### Actual
```
Circle
Drawing Circle at (0, 0) with radius 5.0
Drawing Rectangle at (1, 1), 10.0 x 5.0
78.53975
50.0
31.4159
30.0
5.0
```

## Timing

- Generation: 125.39s
- Execution: 5.16s
