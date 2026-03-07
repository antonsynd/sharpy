# Successful Dogfood Run

**Timestamp:** 2026-03-07T03:48:27.915548
**Feature Focus:** tuple_types
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Named tuple types with bounds calculation and unpacking
# Tests named tuple construction, field access, function returns, and unpacking

type Point = tuple[x: int, y: int]

def translate_point(p: Point, dx: int, dy: int) -> Point:
    return (x=p.x + dx, y=p.y + dy)

def get_bounds(points: list[Point]) -> tuple[min_x: int, max_x: int, min_y: int, max_y: int]:
    if len(points) == 0:
        return (min_x=0, max_x=0, min_y=0, max_y=0)
    
    result_min_x: int = points[0].x
    result_max_x: int = points[0].x
    result_min_y: int = points[0].y
    result_max_y: int = points[0].y
    
    for pt in points:
        if pt.x < result_min_x:
            result_min_x = pt.x
        if pt.x > result_max_x:
            result_max_x = pt.x
        if pt.y < result_min_y:
            result_min_y = pt.y
        if pt.y > result_max_y:
            result_max_y = pt.y
    
    return (min_x=result_min_x, max_x=result_max_x, min_y=result_min_y, max_y=result_max_y)

def main():
    # Create points using named tuple construction
    p1: Point = (x=10, y=20)
    p2: Point = (x=30, y=40)
    p3: Point = (x=5, y=50)
    
    # Translate p1 by (5, -5) to get (15, 15)
    t1: Point = translate_point(p1, 5, -5)
    print(t1.x)
    print(t1.y)
    
    # Compute bounds of all points
    points: list[Point] = [p1, p2, p3, t1]
    bounds = get_bounds(points)
    
    # Unpack the bounds tuple
    min_x_val, max_x_val, min_y_val, max_y_val = bounds
    print(min_x_val)
    print(max_x_val)
    print(min_y_val)
    print(max_y_val)
    
    print(len(points))

```

## Output

```
15
15
5
30
15
50
4
```

## Timing

- Generation: 223.84s
- Execution: 4.79s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
