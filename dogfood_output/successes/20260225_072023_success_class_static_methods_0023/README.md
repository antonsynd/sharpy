# Successful Dogfood Run

**Timestamp:** 2026-02-25T07:19:03.582814
**Feature Focus:** class_static_methods
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class Point:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
    
    @static
    def origin() -> Point:
        return Point(0.0, 0.0)
    
    @static
    def distance(p1: Point, p2: Point) -> float:
        dx: float = p2.x - p1.x
        dy: float = p2.y - p1.y
        return (dx * dx + dy * dy) ** 0.5

class Calculator:
    history: list[str]
    
    def __init__(self):
        self.history = []
    
    def log(self, operation: str, result: float) -> None:
        self.history.append(f"{operation} = {result}")
    
    @static
    def add(a: float, b: float) -> float:
        return a + b
    
    @static
    def multiply(a: float, b: float) -> float:
        return a * b
    
    @static
    def compute_average(values: list[float]) -> float:
        total: float = 0.0
        for v in values:
            total += v
        return total / len(values) if len(values) > 0 else 0.0

def main():
    p1: Point = Point(3.0, 4.0)
    p2: Point = Point.origin()
    
    dist: float = Point.distance(p1, p2)
    print(dist)
    
    calc: Calculator = Calculator()
    sum_result: float = Calculator.add(10.0, 5.0)
    calc.log("10 + 5", sum_result)
    print(sum_result)
    
    product: float = Calculator.multiply(3.0, 7.0)
    print(product)
    
    # (static methods don't have self)
    nums: list[float] = [2.0, 4.0, 6.0, 8.0]
    avg: float = Calculator.compute_average(nums)
    print(avg)

# EXPECTED OUTPUT:
# 5.0
# 15.0
# 21.0
# 5.0
```

## Output

```
5.0
15.0
21.0
5.0
```

## Timing

- Generation: 69.84s
- Execution: 4.47s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
