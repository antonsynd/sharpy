# Successful Dogfood Run

**Timestamp:** 2026-03-07T07:42:44.511304
**Feature Focus:** super_init_call
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test super().__init__() with multi-level inheritance and computed parameters
class Point:
    x: float
    y: float
    
    def __init__(self, x_val: float, y_val: float):
        self.x = x_val
        self.y = y_val

class Rectangle(Point):
    width: float
    height: float
    
    def __init__(self, origin_x: float, origin_y: float, w: float, h: float):
        super().__init__(origin_x, origin_y)
        self.width = w
        self.height = h
    
    def area(self) -> float:
        return self.width * self.height

class Square(Rectangle):
    def __init__(self, origin_x: float, origin_y: float, side: float):
        super().__init__(origin_x, origin_y, side, side)

def main():
    s: Square = Square(10.0, 20.0, 5.0)
    print(s.x)
    print(s.y)
    print(s.width)
    print(s.height)
    print(s.area())

```

## Output

```
10.0
20.0
5.0
5.0
25.0
```

## Timing

- Generation: 87.28s
- Execution: 4.45s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
