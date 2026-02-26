# Issue Report: output_mismatch

**Timestamp:** 2026-02-25T01:27:13.775399
**Type:** output_mismatch
**Feature Focus:** spread_call
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def multiply(x: int, y: int) -> int:
    return x * y

def combine(a: int, b: int, c: int) -> int:
    return a + b * c

# Higher-order function that spreads tuple into callable
def execute(fn: (int, int) -> int, args: tuple[int, int]) -> int:
    return fn(*args)

class Point:
    x: int
    y: int
    
    def __init__(self, x_coord: int, y_coord: int):
        self.x = x_coord
        self.y = y_coord
    
    def distance_from(self, dx: int, dy: int) -> int:
        return (self.x - dx) ** 2 + (self.y - dy) ** 2

def main():
    pair: tuple[int, int] = (5, 3)
    print(multiply(*pair))
    
    triple: tuple[int, int, int] = (2, 4, 6)
    print(combine(*triple))
    
    print(execute(multiply, pair))
    
    p = Point(10, 20)
    print(p.distance_from(*pair))

# EXPECTED OUTPUT:
# 15
# 26
# 15
# 250
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
15
26
15
250

```

### Actual
```
15
26
15
314
```

## Timing

- Generation: 168.55s
- Execution: 4.58s
