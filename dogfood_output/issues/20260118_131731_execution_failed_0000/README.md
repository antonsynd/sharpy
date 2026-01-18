# Issue Report: execution_failed

**Timestamp:** 2026-01-18T13:17:18.300150
**Type:** execution_failed
**Feature Focus:** class_inheritance
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test class inheritance with multiple levels and method overriding

@abstract
class Shape:
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstract
    def area(self) -> float:
        ...

    @virtual
    def describe(self) -> None:
        print(self.name)

class Rectangle(Shape):
    width: float
    height: float

    def __init__(self, name: str, width: float, height: float):
        super().__init__(name)
        self.width = width
        self.height = height

    @override
    def area(self) -> float:
        return self.width * self.height

class Square(Rectangle):
    def __init__(self, name: str, side: float):
        super().__init__(name, side, side)

    @override
    def describe(self) -> None:
        super().describe()
        print(self.width)

rect = Rectangle("MyRectangle", 5.0, 3.0)
rect.describe()
print(rect.area())

square = Square("MySquare", 4.0)
square.describe()
print(square.area())

# EXPECTED OUTPUT:
# MyRectangle
# 15
# MySquare
# 4
# 16
```

## Error

```
Compilation failed:
  Semantic error at line 37, column 9: Parent class 'Rectangle' has no method 'describe'

```

## Timing

- Generation: 4.68s
- Execution: 1.16s
