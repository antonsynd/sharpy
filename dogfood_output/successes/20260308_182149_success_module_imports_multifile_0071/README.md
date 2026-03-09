# Successful Dogfood Run

**Timestamp:** 2026-03-08T18:11:20.169149
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (5 files)

## Source Files

### math_utils.spy

```python
def square(x: float) -> float:
    return x * x

def cube(x: float) -> float:
    return x * x * x

def factorial(n: int) -> int:
    if n <= 1:
        return 1
    return n * factorial(n - 1)

PI: float = 3.14159
E: float = 2.71828

```

### string_utils.spy

```python
def reverse_string(s: str) -> str:
    result: str = ""
    i: int = len(s) - 1
    while i >= 0:
        result = result + str(s[i])
        i = i - 1
    return result

def capitalize_words(s: str) -> str:
    if len(s) == 0:
        return s
    first: str = str(s[0]).upper()
    rest: str = s[1:len(s)].lower()
    return first + rest

```

### data_types.spy

```python
from math_utils import square
from string_utils import reverse_string

class Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def distance_squared(self) -> float:
        return square(self.x) + square(self.y)

    def __str__(self) -> str:
        x_str: str = str(self.x)
        y_str: str = str(self.y)
        return "Point(" + x_str + ", " + y_str + ")"

class LabeledPoint:
    label: str
    point: Point

    def __init__(self, label: str, point: Point):
        self.label = label
        self.point = point

    def get_reversed_label(self) -> str:
        return reverse_string(self.label)

    def get_description(self) -> str:
        return self.label + " point"

```

### operations.spy

```python
from math_utils import factorial
from data_types import Point

def create_points() -> list[Point]:
    points: list[Point] = []
    p1: Point = Point(3.0, 4.0)
    p2: Point = Point(1.0, 1.0)
    p3: Point = Point(5.0, 12.0)
    points.append(p1)
    points.append(p2)
    points.append(p3)
    return points

def calculate_distances(points: list[Point]) -> list[float]:
    distances: list[float] = []
    for p in points:
        d: float = p.distance_squared()
        distances.append(d)
    return distances

def compute_factorial_sum(n: int) -> int:
    total: int = 0
    i: int = 1
    while i <= n:
        total = total + factorial(i)
        i = i + 1
    return total

```

### main.spy

```python
from math_utils import square, factorial, PI, E
from string_utils import reverse_string, capitalize_words
from data_types import Point, LabeledPoint
from operations import create_points, calculate_distances, compute_factorial_sum

def main():
    result: float = square(5.0)
    print(result)
    print(factorial(5))
    print(PI)
    print(E)
    print(reverse_string("hello"))
    print(capitalize_words("world"))
    p: Point = Point(3.0, 4.0)
    print(p)
    lp: LabeledPoint = LabeledPoint("origin", Point(0.0, 0.0))
    print(lp.get_description())
    print(lp.get_reversed_label())
    points: list[Point] = create_points()
    distances: list[float] = calculate_distances(points)
    for d in distances:
        print(d)
    print(compute_factorial_sum(4))

```

## Timing

- Generation: 591.54s
- Execution: 5.31s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
