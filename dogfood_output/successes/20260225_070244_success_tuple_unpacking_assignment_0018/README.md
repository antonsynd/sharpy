# Successful Dogfood Run

**Timestamp:** 2026-02-25T06:53:41.438528
**Feature Focus:** tuple_unpacking_assignment
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class BoundingBox:
    min_x: int
    min_y: int
    max_x: int
    max_y: int

    def __init__(self, x1: int, y1: int, x2: int, y2: int):
        self.min_x = x1
        self.min_y = y1
        self.max_x = x2
        self.max_y = y2

    def get_min_corner(self) -> tuple[int, int]:
        return (self.min_x, self.min_y)

    def get_max_corner(self) -> tuple[int, int]:
        return (self.max_x, self.max_y)

    def get_width(self) -> int:
        return self.max_x - self.min_x

    def get_height(self) -> int:
        return self.max_y - self.min_y

def swap_helper(a: int, b: int) -> tuple[int, int]:
    return (b, a)

def main():
    box = BoundingBox(10, 20, 110, 250)

    x1: int
    y1: int
    x2: int
    y2: int
    width: int
    height: int

    corner1 = box.get_min_corner()
    corner2 = box.get_max_corner()

    x1, y1 = corner1
    x2, y2 = corner2

    temp_result = swap_helper(x1, y1)
    x1, y1 = temp_result

    width = box.get_width()
    height = box.get_height()

    temp_result2 = swap_helper(width, height)
    width, height = temp_result2

    print(x1)
    print(y1)
    print(x2)
    print(y2)
    print(width)
    print(height)

# EXPECTED OUTPUT:
# 20
# 10
# 110
# 250
# 230
# 100
```

## Output

```
20
10
110
250
230
100
```

## Timing

- Generation: 528.37s
- Execution: 4.60s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
