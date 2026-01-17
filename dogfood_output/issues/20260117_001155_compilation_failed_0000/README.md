# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T00:11:32.856615
**Type:** compilation_failed
**Feature Focus:** abstract_class
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Abstract class for shapes with area calculation
@abstract
class Shape:
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstract
    def compute_area(self) -> int:
        ...

    def describe(self) -> None:
        print(self.name)

class Rectangle(Shape):
    width: int
    height: int

    def __init__(self, w: int, h: int):
        super().__init__("Rectangle")
        self.width = w
        self.height = h

    @override
    def compute_area(self) -> int:
        return self.width * self.height

class Square(Shape):
    side: int

    def __init__(self, s: int):
        super().__init__("Square")
        self.side = s

    @override
    def compute_area(self) -> int:
        return self.side * self.side

rect = Rectangle(4, 5)
rect.describe()
print(rect.compute_area())

sq = Square(6)
sq.describe()
print(sq.compute_area())

# EXPECTED OUTPUT:
# Rectangle
# 20
# Square
# 36
```

## Error

```
Assembly compilation failed:
  error CS5001: Program does not contain a static 'Main' method suitable for an entry point

```

## Timing

- Generation: 7.21s
- Execution: 1.74s
