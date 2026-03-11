# Successful Dogfood Run

**Timestamp:** 2026-03-10T05:19:37.663335
**Feature Focus:** type_alias
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Type aliases for simple tuple coordinates
# Tests arithmetic with aliased tuple types

type Point2D = tuple[int, int]
type Offset = int

def move_point(p: Point2D, dx: Offset, dy: Offset) -> Point2D:
    x: int = p[0]
    y: int = p[1]
    return (x + dx, y + dy)

def main():
    origin: Point2D = (0, 0)
    shift_x: Offset = 5
    shift_y: Offset = 3
    
    moved: Point2D = move_point(origin, shift_x, shift_y)
    print(moved[0])
    print(moved[1])
    
    # Chain moves using the same alias
    final: Point2D = move_point(moved, 2, -1)
    print(final[0])
    print(final[1])

```

## Output

```
5
3
7
2
```

## Timing

- Generation: 145.38s
- Execution: 5.18s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
