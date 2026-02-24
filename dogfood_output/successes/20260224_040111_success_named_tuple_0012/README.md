# Successful Dogfood Run

**Timestamp:** 2026-02-24T03:58:52.718893
**Feature Focus:** named_tuple
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
type Point = tuple[x: float, y: float]
type Dimension = tuple[width: float, height: float]
type Rectangle = tuple[origin: Point, size: Dimension]

struct Box:
    min_corner: Point
    max_corner: Point
    
    def __init__(self, min_c: Point, max_c: Point):
        self.min_corner = min_c
        self.max_corner = max_c
    
    def area(self) -> float:
        width = self.max_corner.x - self.min_corner.x
        height = self.max_corner.y - self.min_corner.y
        return width * height

def main():
    p1: Point = (x=0.0, y=0.0)
    p2: Point = (x=10.0, y=20.0)
    
    box = Box(p1, p2)
    print(box.area())
    
    points: list[Point] = [(x=1.0, y=2.0), (x=3.0, y=4.0), (x=5.0, y=6.0)]
    total: float = 0.0
    for x_val, y_val in points:
        total = total + x_val + y_val
    print(total)
    
    rect: Rectangle = (origin=(x=1.0, y=2.0), size=(width=50.0, height=30.0))
    print(rect.origin.x)
    print(rect.size.height)
    
    if p2.x > p1.x:
        print(p2.x - p1.x)
# EXPECTED OUTPUT:
# 200.0
# 21.0
# 1.0
# 30.0
# 10.0
```

## Output

```
200.0
21.0
1.0
30.0
10.0
```

## Timing

- Generation: 128.20s
- Execution: 4.59s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
