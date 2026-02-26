# Successful Dogfood Run

**Timestamp:** 2026-02-25T10:26:29.905427
**Feature Focus:** named_tuple
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Named tuples with coordinate arithmetic, unpacking, and tiered filtering

type Point = tuple[x: float, y: float]

def sum_coordinates(pts: list[Point]) -> float:
    total: float = 0.0
    for px, py in pts:
        total += px + py
    return total

def main():
    points: list[Point] = [(x=1.5, y=2.5), (x=3.0, y=4.0), (x=5.5, y=6.5)]
    
    print(sum_coordinates(points))
    
    for pt in points:
        if pt.x < 2.0:
            print(pt.y)
        elif pt.x > 4.0:
            print(pt.x)
    
    first: Point = points[0]
    print(first.x + first.y)

# EXPECTED OUTPUT:
# 23.0
# 2.5
# 5.5
# 4.0
```

## Output

```
23.0
2.5
5.5
4.0
```

## Timing

- Generation: 241.91s
- Execution: 4.70s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
