# Successful Dogfood Run

**Timestamp:** 2026-03-06T16:06:57.224112
**Feature Focus:** match_positional_pattern
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

class Size:
    width: int
    height: int

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height

def classify_point(p: Point) -> str:
    match p:
        case Point(0, 0):
            return "origin"
        case Point(0, _):
            return "y-axis"
        case Point(_, 0):
            return "x-axis"
        case _:
            return "quadrant"

def describe_size(s: Size) -> str:
    match s:
        case Size(w, h):
            area: int = w * h
            return f"size {w}x{h}, area {area}"
        case _:
            return "unknown"

def analyze_shape(p: Point, s: Size) -> str:
    match p:
        case Point(x, y):
            match s:
                case Size(w, h):
                    if x == w and y == h:
                        return "point matches size"
                    else:
                        return f"point ({x},{y}) vs size {w}x{h}"
                case _:
                    return "no size"
        case _:
            return "no point"

def main():
    # Test classify_point
    print(classify_point(Point(0, 0)))
    print(classify_point(Point(0, 5)))
    print(classify_point(Point(3, 0)))
    print(classify_point(Point(2, 3)))

    # Test describe_size with binding
    print(describe_size(Size(10, 20)))

    # Test analyze_shape
    print(analyze_shape(Point(5, 5), Size(5, 5)))
    print(analyze_shape(Point(2, 3), Size(4, 6)))

```

## Output

```
origin
y-axis
x-axis
quadrant
size 10x20, area 200
point matches size
point (2,3) vs size 4x6
```

## Timing

- Generation: 455.04s
- Execution: 4.69s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
