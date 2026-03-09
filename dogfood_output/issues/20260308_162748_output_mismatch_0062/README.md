# Issue Report: output_mismatch

**Timestamp:** 2026-03-08T16:21:32.453622
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports and polymorphism
# Uses both modules: shapes and utils
from shapes import Rectangle, Circle, Point, create_square
from utils import total_area_rectangles, describe_all_rectangles, filter_large_rectangles, scale_rectangle, get_rectangle_area

def main():
    # Create some shapes
    rect: Rectangle = Rectangle(4.0, 5.0)
    circle: Circle = Circle(3.0)
    square: Rectangle = create_square(2.5)

    # Create list of rectangles using append (invariant generics workaround)
    shapes: list[Rectangle] = []
    shapes.append(rect)
    shapes.append(square)

    # Test struct methods
    p: Point = Point(3.0, 4.0)
    distance: float = p.distance_from_origin()
    print(distance)

    # Calculate total area of rectangles
    area_sum: float = total_area_rectangles(shapes)
    print(area_sum)

    # Describe all drawable rectangles
    descriptions: list[str] = describe_all_rectangles(shapes)
    if len(descriptions) >= 1:
        print(descriptions[0])
    if len(descriptions) >= 2:
        print(descriptions[1])

    # Test scaled rectangle
    scaled: Rectangle = scale_rectangle(rect, 2.0)
    scaled_area: float = get_rectangle_area(scaled)
    print(scaled_area)

    # Filter large shapes
    large_shapes: list[Rectangle] = filter_large_rectangles(shapes)
    print(len(large_shapes))

    # Test circle area directly
    circle_area: float = circle.area()
    print(circle_area)

    # Test square
    square_area: float = square.area()
    print(square_area)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
5.0
20.0
Rectangle(4.0, 5.0)
Rectangle(2.5, 2.5)
40.0
1
28.27431
6.25

```

### Actual
```
5.0
26.25
Rectangle(4.0, 5.0)
Rectangle(2.5, 2.5)
80.0
1
28.274309999999996
6.25
```

## Timing

- Generation: 318.41s
- Execution: 5.13s
