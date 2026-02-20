# Issue Report: output_mismatch

**Timestamp:** 2026-02-19T02:25:58.170257
**Type:** output_mismatch
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating complex cross-module imports
# Tests: from X import A, B, C syntax with classes, structs, enums, functions

from shapes import Shape, Rectangle, Circle, IColorable, ColoredRectangle
from geometry import Point, Size, ShapeType, calculate_bounding_box
from utils import format_number, sum_areas, classify_shape, create_point_grid, ShapeStats, DEFAULT_COLOR

def main():
    print("=== Cross-Module Import Test ===")
    
    # Test 1: Create shapes from shapes module
    rect: Rectangle = Rectangle("my_rect", 10.0, 5.0)
    circle: Circle = Circle("my_circle", 3.0)
    
    print(rect.describe())
    print(circle.describe())
    
    # Test 2: Calculate areas and format using utils
    rect_area: float = rect.area()
    circle_area: float = circle.area()
    print(f"Rectangle area: {format_number(rect_area, 2)}")
    print(f"Circle area: {format_number(circle_area, 2)}")
    
    # Test 3: Interface implementation (IColorable)
    colored_rect: ColoredRectangle = ColoredRectangle("colored", 4.0, 6.0, "red")
    print(f"Color: {colored_rect.get_color()}")
    colored_rect.set_color("blue")
    print(f"New color: {colored_rect.get_color()}")
    
    # Test 4: Struct value semantics from geometry
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    distance: float = p1.distance_to(p2)
    print(f"Distance: {format_number(distance, 1)}")
    
    # Test 5: Enum usage
    shape_type: ShapeType = ShapeType.RECTANGLE
    print(f"Shape type: {shape_type}")
    
    # Test 6: Complex function with cross-module types
    points: list[Point] = create_point_grid(2, 2, 5.0)
    bbox: Size = calculate_bounding_box(points)
    print(f"Grid bounding box: {format_number(bbox.width, 1)} x {format_number(bbox.height, 1)}")
    
    # Test 7: Polymorphic collection and sum_areas
    shapes: list[Shape] = [rect, circle, colored_rect]
    total: float = sum_areas(shapes)
    print(f"Total area: {format_number(total, 2)}")
    
    # Test 8: Statistics class
    stats: ShapeStats = ShapeStats()
    stats.add_shape(rect)
    stats.add_shape(circle)
    print(f"Average area: {format_number(stats.get_average(), 2)}")
    
    # Test 9: Classification
    for s in shapes:
        print(f"{s.name} is {classify_shape(s)}")
    
    print(f"Default color from utils: {DEFAULT_COLOR}")
    print("=== All Tests Passed ===")

# EXPECTED OUTPUT:
# === Cross-Module Import Test ===
# Shape: my_rect
# Circle(my_circle) with radius 3.0
# Rectangle area: 50.0
# Circle area: 28.27
# Color: red
# New color: blue
# Distance: 5.0
# Shape type: Rectangle
# Grid bounding box: 5.0 x 5.0
# Total area: 103.27
# Average area: 39.14
# my_rect is rectangular
# my_circle is circular
# colored is rectangular
# Default color from utils: gray
# === All Tests Passed ===
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
=== Cross-Module Import Test ===
Shape: my_rect
Circle(my_circle) with radius 3.0
Rectangle area: 50.0
Circle area: 28.27
Color: red
New color: blue
Distance: 5.0
Shape type: Rectangle
Grid bounding box: 5.0 x 5.0
Total area: 103.27
Average area: 39.14
my_rect is rectangular
my_circle is circular
colored is rectangular
Default color from utils: gray
=== All Tests Passed ===

```

### Actual
```
=== Cross-Module Import Test ===
Shape: my_rect
Circle(my_circle) with radius 3
Rectangle area: 50
Circle area: 28.27
Color: red
New color: blue
Distance: 5
Shape type: Rectangle
Grid bounding box: 5 x 5
Total area: 102.27
Average area: 39.14
my_rect is rectangular
my_circle is circular
colored is rectangular
Default color from utils: gray
=== All Tests Passed ===
```

## Timing

- Generation: 220.94s
- Execution: 4.70s
