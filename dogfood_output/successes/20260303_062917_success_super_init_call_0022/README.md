# Successful Dogfood Run

**Timestamp:** 2026-03-03T06:28:02.902028
**Feature Focus:** super_init_call
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Subclass providing convenience constructor with computed super call
class Rectangle:
    width: int
    height: int

    def __init__(self, w: int, h: int):
        self.width = w
        self.height = h

class Square(Rectangle):
    def __init__(self, side: int):
        super().__init__(side, side)

def main():
    sq = Square(7)
    print(sq.width)
    print(sq.height)

```

## Output

```
7
7
```

## Timing

- Generation: 64.14s
- Execution: 4.54s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
