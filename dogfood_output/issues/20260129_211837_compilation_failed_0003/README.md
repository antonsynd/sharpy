# Issue Report: compilation_failed

**Timestamp:** 2026-01-29T21:17:50.032573
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test cross-module class inheritance
from shapes import Rectangle
from advanced_shapes import Circle, Triangle

def main():
    rect = Rectangle(5.0, 3.0)
    print(rect.describe())
    print(rect.area())
    
    circ = Circle(4.0)
    print(circ.perimeter())
    
    tri = Triangle(3.0, 4.0, 5.0)
    print(tri.area())
    print(tri.perimeter())


# EXPECTED OUTPUT:
# Shape: Rectangle
# 15.0
# 25.13272
# 6.0
# 12.0
```

## Error

```
Assembly compilation failed:
  shapes.cs(17,40): error CS0503: The abstract method 'Shape.Area()' cannot be marked virtual
  shapes.cs(18,40): error CS0503: The abstract method 'Shape.Perimeter()' cannot be marked virtual
  shapes.cs(44,16): error CS7036: There is no argument given that corresponds to the required parameter 'shapeName' of 'Shape.Shape(string)'
  advanced_shapes.cs(28,16): error CS7036: There is no argument given that corresponds to the required parameter 'shapeName' of 'Shape.Shape(string)'
  advanced_shapes.cs(51,16): error CS7036: There is no argument given that corresponds to the required parameter 'shapeName' of 'Shape.Shape(string)'

```

## Timing

- Generation: 15.65s
- Execution: 1.33s
