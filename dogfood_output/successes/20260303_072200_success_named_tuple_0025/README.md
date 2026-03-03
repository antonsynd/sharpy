# Successful Dogfood Run

**Timestamp:** 2026-03-03T07:14:13.850409
**Feature Focus:** named_tuple
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
type Point2D = tuple[x: float, y: float]

@abstract
class Shape:
    @abstract
    def get_bounds(self) -> tuple[min: Point2D, max: Point2D]:
        ...

class Rectangle(Shape):
    _top_left: Point2D
    _bottom_right: Point2D
    
    def __init__(self, tl: Point2D, br: Point2D):
        self._top_left = tl
        self._bottom_right = br
    
    @override
    def get_bounds(self) -> tuple[min: Point2D, max: Point2D]:
        return (min=self._top_left, max=self._bottom_right)

class Circle(Shape):
    _center: Point2D
    _radius: float
    
    def __init__(self, c: Point2D, r: float):
        self._center = c
        self._radius = r
    
    @override
    def get_bounds(self) -> tuple[min: Point2D, max: Point2D]:
        min_x = self._center.x - self._radius
        min_y = self._center.y - self._radius
        max_x = self._center.x + self._radius
        max_y = self._center.y + self._radius
        return (min=(x=min_x, y=min_y), max=(x=max_x, y=max_y))

def calculate_centroid(shapes: list[Shape]) -> Point2D:
    if len(shapes) == 0:
        return (x=0.0, y=0.0)
    
    total_x = 0.0
    total_y = 0.0
    
    for s in shapes:
        bounds = s.get_bounds()
        min_pt, max_pt = bounds
        cx = (min_pt.x + max_pt.x) / 2.0
        cy = (min_pt.y + max_pt.y) / 2.0
        total_x += cx
        total_y += cy
    
    return (x=total_x / len(shapes), y=total_y / len(shapes))

def main():
    rect = Rectangle((x=0.0, y=0.0), (x=10.0, y=5.0))
    circle = Circle((x=5.0, y=5.0), 3.0)
    
    shapes: list[Shape] = [rect, circle]
    centroid = calculate_centroid(shapes)
    
    print(centroid.x)
    print(centroid.y)
    
    rect_bounds = rect.get_bounds()
    circle_bounds = circle.get_bounds()
    
    print(rect_bounds.min.x)
    print(rect_bounds.max.y)
    print(circle_bounds.min.x)
    print(circle_bounds.max.y)

```

## Output

```
5.0
3.75
0.0
5.0
2.0
8.0
```

## Timing

- Generation: 456.19s
- Execution: 4.85s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
