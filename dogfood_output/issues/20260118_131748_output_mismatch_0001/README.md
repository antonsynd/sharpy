# Issue Report: output_mismatch

**Timestamp:** 2026-01-18T13:17:31.225175
**Type:** output_mismatch
**Feature Focus:** virtual_override
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test virtual and override methods with a simple shape hierarchy

@abstract
class Shape:
    name: str

    def __init__(self, name: str):
        self.name = name

    @virtual
    def area(self) -> float:
        return 0.0

    @virtual
    def perimeter(self) -> float:
        return 0.0

    def describe(self) -> None:
        print(self.name)
        print(self.area())
        print(self.perimeter())

class Rectangle(Shape):
    width: float
    height: float

    def __init__(self, width: float, height: float):
        super().__init__("Rectangle")
        self.width = width
        self.height = height

    @override
    def area(self) -> float:
        return self.width * self.height

    @override
    def perimeter(self) -> float:
        return 2.0 * (self.width + self.height)

class Circle(Shape):
    radius: float

    def __init__(self, radius: float):
        super().__init__("Circle")
        self.radius = radius

    @override
    def area(self) -> float:
        return 3.14 * self.radius * self.radius

    @override
    def perimeter(self) -> float:
        return 2.0 * 3.14 * self.radius

rect: Rectangle = Rectangle(5.0, 3.0)
rect.describe()

circ: Circle = Circle(4.0)
circ.describe()

# EXPECTED OUTPUT:
# Rectangle
# 15.0
# 16.0
# Circle
# 50.24
# 25.12
```

## Output Comparison

### Expected
```
Rectangle
15.0
16.0
Circle
50.24
25.12

```

### Actual
```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_23f0dbc765dd4156bf5a05a80d70d30d.exe

=== Running Program ===

Rectangle
15
16
Circle
50.24
25.12
```

## Timing

- Generation: 5.07s
- Execution: 1.30s
