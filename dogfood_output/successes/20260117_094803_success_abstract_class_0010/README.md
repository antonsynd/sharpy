# Successful Dogfood Run

**Timestamp:** 2026-01-17T09:47:42.495497
**Feature Focus:** abstract_class
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Abstract class with abstract and concrete methods

@abstract
class Shape:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @abstract
    def area(self) -> float:
        ...
    
    def describe(self) -> None:
        print(self.name)

class Rectangle(Shape):
    width: float
    height: float
    
    def __init__(self, w: float, h: float):
        super().__init__("Rectangle")
        self.width = w
        self.height = h
    
    @override
    def area(self) -> float:
        return self.width * self.height

class Circle(Shape):
    radius: float
    
    def __init__(self, r: float):
        super().__init__("Circle")
        self.radius = r
    
    @override
    def area(self) -> float:
        return 3.14 * self.radius * self.radius

rect = Rectangle(4.0, 5.0)
circ = Circle(3.0)

rect.describe()
print(rect.area())

circ.describe()
print(circ.area())

# EXPECTED OUTPUT:
# Rectangle
# 20
# Circle
# 28.26
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_c0d576edd1ed4164810835a72789863d.exe

=== Running Program ===

Rectangle
20
Circle
28.259999999999998
```

## Timing

- Generation: 8.47s
- Execution: 1.29s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
