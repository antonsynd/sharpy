# Issue Report: output_mismatch

**Timestamp:** 2026-02-25T07:31:14.203428
**Type:** output_mismatch
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point testing cross-module imports

from types_data import Point, Color, Status, create_red_color, point_from_coords, get_default_status
from math_utils import square, hypotenuse, distance_between, clamp, average, sum_ints
from geometry_shapes import Rectangle, Circle, ShapeCollection, create_unit_square, create_unit_circle

def main():
    # Test 1: Basic enum from types_data
    s: Status = get_default_status()
    if s == Status.ACTIVE:
        print(1)
    else:
        print(0)

    # Test 2: Struct creation from types_data
    c: Color = create_red_color()
    print(c.r)
    print(c.g)
    print(c.b)

    # Test 3: hex conversion
    hex_str: str = c.to_hex()
    print(hex_str)

    # Test 4: Point struct from types_data
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    print(p1.x)
    print(p2.y)

    # Test 5: Point distance
    dist1: float = p1.distance_to_origin()
    dist2: float = p2.distance_to_origin()
    print(dist1)
    print(dist2)

    # Test 6: tuple to point
    coords: tuple[float, float] = (5.0, 12.0)
    p3: Point = point_from_coords(coords)
    print(p3.distance_to_origin())

    # Test 7: Math functions from math_utils
    sq: float = square(5.0)
    print(sq)
    hyp: float = hypotenuse(3.0, 4.0)
    print(hyp)

    # Test 8: Distance between points
    dist: float = distance_between(p1, p2)
    print(dist)

    # Test 9: Clamp function
    print(clamp(15.0, 0.0, 10.0))
    print(clamp(5.0, 0.0, 10.0))

    # Test 10: Average
    values: list[float] = [1.0, 2.0, 3.0, 4.0, 5.0]
    avg: float = average(values)
    print(avg)

    # Test 11: Sum ints
    ints: list[int] = [1, 2, 3, 4, 5]
    total: int = sum_ints(ints)
    print(total)

    # Test 12: Rectangle from geometry_shapes
    origin: Point = Point(0.0, 0.0)
    rect: Rectangle = create_unit_square(origin)
    print(rect.area())
    print(rect.perimeter())

    # Test 13: Circle from geometry_shapes
    center: Point = Point(5.0, 5.0)
    circle: Circle = create_unit_circle(center)
    print(circle.area())
    print(circle.circumference())

    # Test 14: Point containment
    inside: Point = Point(5.5, 5.5)
    outside: Point = Point(10.0, 10.0)
    print(circle.contains_point(inside))
    print(circle.contains_point(outside))

    # Test 15: ShapeCollection
    collection: ShapeCollection = ShapeCollection()
    r1: Rectangle = create_unit_square(Point(0.0, 0.0))
    r2: Rectangle = Rectangle(Point(10.0, 10.0), 2.0, 3.0, Color(0, 255, 0))
    collection.add_rectangle(r1)
    collection.add_rectangle(r2)
    print(collection.get_shape_count())

    c1: Circle = create_unit_circle(Point(20.0, 20.0))
    collection.add_circle(c1)
    collection.add_circle(c1)
    print(collection.get_shape_count())

    # Test 16: Total areas
    print(collection.total_rectangle_area())
    print(collection.total_circle_area())

    # Test 17: Color hex from shape
    print(r1.get_color_name())
    print(c1.get_color_name())

# EXPECTED OUTPUT:
# 1
# 255
# 0
# 0
# #FF0000
# 0.0
# 4.0
# 0.0
# 5.0
# 13.0
# 5.0
# 10.0
# 5.0
# 3.0
# 15.0
# 3.0
# 1.0
# 2.0
# 3.0
# 4.0
# 5.0
# 15
# 1.0
# 4.0
# 3.14159
# 6.28318
# True
# False
# 2
# 4
# 7.0
# 6.28318
# #FF0000
# #0000FF
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
1
255
0
0
#FF0000
0.0
4.0
0.0
5.0
13.0
5.0
10.0
5.0
3.0
15.0
3.0
1.0
2.0
3.0
4.0
5.0
15
1.0
4.0
3.14159
6.28318
True
False
2
4
7.0
6.28318
#FF0000
#0000FF

```

### Actual
```
1
255
0
0
#FF0000
0.0
4.0
0.0
5.0
13.0
25.0
5.0
5.0
10.0
5.0
3.0
15
1.0
4.0
3.14159
6.28318
True
False
2
4
7.0
6.28318
#FF0000
#0000FF
```

## Timing

- Generation: 584.97s
- Execution: 4.66s
