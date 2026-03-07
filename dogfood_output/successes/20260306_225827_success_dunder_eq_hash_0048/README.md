# Successful Dogfood Run

**Timestamp:** 2026-03-06T22:51:49.429726
**Feature Focus:** dunder_eq_hash
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class Color:
    red: int
    green: int

    def __init__(self, r: int, g: int):
        self.red = r
        self.green = g

    def __eq__(self, other: object) -> bool:
        match other:
            case Color() as c:
                return self.red == c.red and self.green == c.green
            case _:
                return False

    def __hash__(self) -> int:
        return self.red * 31 + self.green

def main():
    c1 = Color(255, 100)
    c2 = Color(255, 100)
    c3 = Color(100, 255)
    
    print(c1 == c2)
    print(c1 == c3)
    
    colors: set[Color] = set()
    colors.add(c1)
    colors.add(c2)
    colors.add(c3)
    
    print(len(colors))
    
    # Sort for deterministic output since set iteration order is not guaranteed
    reds: list[int] = []
    for c in colors:
        reds.append(c.red)
    reds.sort()
    for r in reds:
        print(r)

```

## Output

```
True
False
2
100
255
```

## Timing

- Generation: 373.96s
- Execution: 5.44s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
