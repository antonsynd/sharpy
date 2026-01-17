# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T09:43:33.276536
**Type:** compilation_failed
**Feature Focus:** class_instance_methods
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Test class instance methods with inheritance and interfaces

interface IDisplayable:
    def display(self) -> None:
        ...

interface ICalculable:
    def calculate(self) -> int:
        ...

@abstract
class Shape(IDisplayable):
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @abstract
    def area(self) -> int:
        ...
    
    @virtual
    def describe(self) -> int:
        return 1

class Rectangle(Shape, ICalculable):
    width: int
    height: int
    
    def __init__(self, w: int, h: int):
        super().__init__("Rectangle")
        self.width = w
        self.height = h
    
    @override
    def area(self) -> int:
        return self.width * self.height
    
    @override
    def describe(self) -> int:
        return 2
    
    def display(self) -> None:
        print(self.name)
        print(self.area())
    
    def calculate(self) -> int:
        return self.width + self.height
    
    def scale(self, factor: int) -> None:
        self.width *= factor
        self.height *= factor

class Square(Rectangle):
    def __init__(self, side: int):
        super().__init__(side, side)
        self.name = "Square"
    
    @override
    def describe(self) -> int:
        base_val: int = super().describe()
        return base_val + 1

rect: Rectangle = Rectangle(5, 3)
rect.display()
print(rect.calculate())
print(rect.describe())

sq: Square = Square(4)
sq.display()
print(sq.calculate())
print(sq.describe())

sq.scale(2)
print(sq.area())

count: int = 0
for i in range(3):
    if rect.width > i:
        count += 1

print(count)

# EXPECTED OUTPUT:
# Rectangle
# 15
# 8
# 2
# Square
# 16
# 8
# 3
# 64
# 3
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(22,39): error CS0535: 'Program.Shape' does not implement interface member 'Program.IDisplayable.Display()'
  dogfood_test.cs(85,22): error CS1061: 'Program.Square' does not contain a definition for 'name' and no accessible extension method 'name' accepting a first argument of type 'Program.Square' could be found (are you missing a using directive or an assembly reference?)

```

## Timing

- Generation: 9.25s
- Execution: 1.23s
