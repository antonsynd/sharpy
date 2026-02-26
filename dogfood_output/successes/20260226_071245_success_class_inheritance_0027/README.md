# Successful Dogfood Run

**Timestamp:** 2026-02-26T07:07:45.446517
**Feature Focus:** class_inheritance
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
@abstract
class Shape:
    @abstract
    def calculate_area(self) -> float:
        ...

    @virtual
    def calculate_perimeter(self) -> float:
        return 0.0

class Rectangle(Shape):
    width: float
    height: float

    def __init__(self, w: float, h: float):
        self.width = w
        self.height = h

    @override
    def calculate_area(self) -> float:
        return self.width * self.height

    @override
    def calculate_perimeter(self) -> float:
        return 2.0 * (self.width + self.height)


class Circle(Shape):
    radius: float

    def __init__(self, r: float):
        self.radius = r

    @override
    def calculate_area(self) -> float:
        return 3.14159 * self.radius ** 2.0

    @override
    def calculate_perimeter(self) -> float:
        return 2.0 * 3.14159 * self.radius


def main():
    rect: Rectangle = Rectangle(3.0, 4.0)
    circ: Circle = Circle(5.0)

    shapes: list[Shape] = [rect, circ]

    for shape in shapes:
        print(shape.calculate_area())
        print(shape.calculate_perimeter())
```

## Output

```
12.0
14.0
78.53975
31.4159
```

## Timing

- Generation: 280.47s
- Execution: 4.39s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
