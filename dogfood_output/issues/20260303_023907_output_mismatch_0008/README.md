# Issue Report: output_mismatch

**Timestamp:** 2026-03-03T02:35:12.240573
**Type:** output_mismatch
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests cross-module inheritance and interfaces
from shapes import Shape, Color, Point
from geometry import GeometricObject, IDrawable
from concrete import Circle, Rectangle
from utils import total_area, total_perimeter, filter_by_color, create_circle

def main():
    # Create shapes using cross-module types
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(5.0, 5.0)
    p3: Point = Point(10.0, 0.0)
    
    # Test struct Point
    dist: float = p1.distance_to(p2)
    print(dist)
    
    # Create shapes with different colors
    c1: Circle = Circle(5.0, Color.RED, p1)
    c2: Circle = Circle(3.0, Color.BLUE, p2)
    r1: Rectangle = Rectangle(4.0, 6.0, Color.RED, p3)
    
    # Test polymorphic dispatch through Shape (cross-module inheritance)
    shapes: list[Shape] = [c1, c2, r1]
    
    # Calculate totals using utility functions
    total_a: float = total_area(shapes)
    total_p: float = total_perimeter(shapes)
    print(total_a)
    print(total_p)
    
    # Test interface implementation (IDrawable)
    d1: IDrawable = c1
    d2: IDrawable = r1
    print(d1.draw())
    print(d2.draw())
    
    # Test method override from grandparent (Shape -> Circle)
    print(c1.get_description())
    
    # Test enum iteration and filtering
    red_shapes: list[Shape] = filter_by_color(shapes, Color.RED)
    print(float(len(red_shapes)))
    
    # Test factory function from utils
    c3: Circle = create_circle(2.5, 1.0, 1.0)
    print(c3.area())

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
7.0710678118654755
122.522025
56.84952
Drawing circle with radius 5.0
Drawing rectangle 4.0 x 6.0
Circle(r=5.0) at (0.0, 0.0)
2.0
19.6349375

```

### Actual
```
7.0710678118654755
130.81405999999998
70.26544
Drawing circle with radius 5.0
Drawing rectangle 4.0 x 6.0
Circle(r=5.0) at (0, 0)
2.0
19.6349375
```

## Timing

- Generation: 162.33s
- Execution: 5.10s
