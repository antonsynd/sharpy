# Successful Dogfood Run

**Timestamp:** 2026-01-18T14:16:54.858774
**Feature Focus:** struct_definition
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test struct definition with value semantics and methods
struct Rectangle:
    width: int
    height: int

    def area(self) -> int:
        return self.width * self.height

    def perimeter(self) -> int:
        return 2 * (self.width + self.height)

    def is_square(self) -> bool:
        return self.width == self.height

struct Circle:
    radius: int

    def diameter(self) -> int:
        return self.radius * 2

    def approximate_area(self) -> int:
        # Using integer approximation: area ≈ 3 * r * r
        return 3 * self.radius * self.radius

def compare_shapes() -> None:
    rect: Rectangle = Rectangle()
    rect.width = 5
    rect.height = 8
    
    square: Rectangle = Rectangle()
    square.width = 6
    square.height = 6
    
    circ: Circle = Circle()
    circ.radius = 4
    
    print(rect.area())
    print(rect.perimeter())
    print(square.is_square())
    print(rect.is_square())
    print(circ.diameter())
    print(circ.approximate_area())

compare_shapes()

# EXPECTED OUTPUT:
# 40
# 26
# True
# False
# 8
# 48
```

## Output

```
40
26
True
False
8
48
```

## Timing

- Generation: 4.99s
- Execution: 1.37s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
