# Successful Dogfood Run

**Timestamp:** 2026-02-24T00:32:59.945967
**Feature Focus:** tuple_types
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Tuple types with unpacking and iteration patterns
# Features: tuple type annotations, unpacking, function parameters/returns,
# list of tuples iteration, nested tuple unpacking

def scale_point(pair: tuple[int, int], factor: int) -> tuple[int, int]:
    x, y = pair
    return (x * factor, y * factor)

def main():
    origin: tuple[int, int] = (3, 4)
    x, y = origin
    print(x)
    print(y)
    
    doubled = scale_point(origin, 2)
    dx, dy = doubled
    print(dx)
    print(dy)
    
    points: list[tuple[int, int]] = [(1, 2), (3, 4), (5, 6)]
    for px, py in points:
        print(px + py)
    
    location: tuple[tuple[int, int], str] = ((7, 8), "center")
    (lx, ly), name = location
    print(lx)
    print(name)

# EXPECTED OUTPUT:
# 3
# 4
# 6
# 8
# 3
# 7
# 11
# 7
# center
```

## Output

```
3
4
6
8
3
7
11
7
center
```

## Timing

- Generation: 277.33s
- Execution: 4.71s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
