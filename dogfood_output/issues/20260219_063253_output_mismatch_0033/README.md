# Issue Report: output_mismatch

**Timestamp:** 2026-02-19T06:25:33.689776
**Type:** output_mismatch
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Entry point demonstrating cross-module classes, inheritance, and interfaces

from shapes import Shape, ShapeType, IDrawable
from geometry import Point, Circle, Rectangle
from utils import ShapeStats, create_circle_at_origin, format_shape_info

def main():
    # Create points and shapes from geometry module
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(10.0, 5.0)
    
    circle: Circle = Circle(p1, 5.0)
    rect: Rectangle = Rectangle(p2, 20.0, 10.0)
    
    # Store in list of base type (polymorphism)
    shapes: list[Shape] = [circle, rect]
    
    # Print formatted info for each shape
    for shape in shapes:
        print(format_shape_info(shape))
    
    # Demonstrate interface methods via IDrawable
    drawable_count: int = ShapeStats.count_drawable(shapes)
    print(f"Drawable shapes: {drawable_count}")
    
    for shape in shapes:
        if isinstance(shape, IDrawable):
            drawable: IDrawable = shape
            print(drawable.draw())
    
    # Test static utility methods
    total: float = ShapeStats.total_area(shapes)
    print(f"Total area: {total}")
    
    # Test factory function creating shape with struct parameter
    default_circle: Circle = create_circle_at_origin(3.0)
    print(f"Default circle area: {default_circle.area()}")
    
    # Test struct value semantics and method
    distance: float = p1.distance_to(p2)
    print(f"Distance between points: {distance}")
    
    # EXPECTED OUTPUT:
    # Circle[r=5.0]: area=78.53975, perimeter=31.4159
    # Rectangle: area=200.0, perimeter=60.0
    # Drawable shapes: 2
    # Drawing circle at (0.0, 0.0)
    # Drawing rectangle at (10.0, 5.0)
    # Total area: 278.53975
    # Default circle area: 28.27431
    # Distance between points: 11.180339887498949
```

## Error

```
AI verification backend failure
```

## Output Comparison

### Expected
```
Circle[r=5.0]: area=78.53975, perimeter=31.4159
Rectangle: area=200.0, perimeter=60.0
Drawable shapes: 2
Drawing circle at (0.0, 0.0)
Drawing rectangle at (10.0, 5.0)
Total area: 278.53975
Default circle area: 28.27431
Distance between points: 11.180339887498949

```

### Actual
```
Circle[r=5]: area=78.53975, perimeter=31.4159
Rectangle: area=200, perimeter=60
Drawable shapes: 2
Drawing circle at (0, 0)
Drawing rectangle at (10, 5)
Total area: 278.53975
Default circle area: 28.274309999999996
Distance between points: 11.180339887498949
```

## Timing

- Generation: 272.75s
- Execution: 4.63s
