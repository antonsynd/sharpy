# Issue Report: output_mismatch

**Timestamp:** 2026-02-25T09:29:59.279756
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Entry point demonstrating complex module utility system

from types_data import LogLevel, StatusCode, Point2D, NamedPoint, Box
from geometry_base import Shape, Rectangle, Square, PointProcessor
from shapes import Circle, ColoredBox
from module_utils import compute_factorial, filter_positive, get_status_message
from module_utils import process_point, get_shape_info, create_named_point, Aggregator

def main():
    # Test enum values
    print(LogLevel.INFO.value)

    # Test recursive factorial
    result: int = compute_factorial(5)
    print(result)

    # Test shape hierarchy
    rect: Rectangle = Rectangle(1, 3.0, 4.0)
    print(rect.area())

    square: Square = Square(2, 5.0)
    print(square.perimeter())

    circle: Circle = Circle(3, 2.5)
    print(circle.area())

    # Test shape polymorphism
    info: str = get_shape_info(circle)
    print(info)

    # Test processor interface
    point: Point2D = Point2D(3.0, 4.0)
    processor: PointProcessor = PointProcessor()
    magnitude: float = process_point(processor, point)
    print(magnitude)

    # Test list filtering with comprehension
    numbers: list[float] = [-2.5, 3.0, -1.0, 5.5, 0.0, 10.0]
    positive: list[float] = filter_positive(numbers)
    print(len(positive))

    # Test generic Box with map
    int_box: Box[int] = Box[int](10)
    doubled: Box[int] = int_box.map(lambda x: x * 2)
    print(doubled.unwrap())

    # Test Aggregator class
    agg: Aggregator = Aggregator()
    agg.add(10)
    agg.add(20)
    agg.add(30)
    print(agg.sum())

    # Test named point class
    named: NamedPoint = create_named_point("origin", 0.0, 0.0)
    x_val: float = named.x
    print(x_val)

    # Test status message
    print(get_status_message(StatusCode.SUCCESS))

# EXPECTED OUTPUT:
# 1
# 120
# 12.0
# 20.0
# 19.6349375
# Circle (id=3)
# 5.0
# 3
# 20
# 60
# 0.0
# Operation successful
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
1
120
12.0
20.0
19.6349375
Circle (id=3)
5.0
3
20
60
0.0
Operation successful

```

### Actual
```
1
120
12.0
20.0
19.6349375
Shape (id=3)
5.0
3
20
60
0.0
Operation successful
```

## Timing

- Generation: 354.44s
- Execution: 4.85s
