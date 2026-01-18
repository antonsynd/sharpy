# Issue Report: execution_failed

**Timestamp:** 2026-01-18T14:18:59.424168
**Type:** execution_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - cross-module class usage
from geometry import Point, Circle, Rectangle
from calculator import ShapeCalculator, compare_shapes

# Create shapes
p1: Point = Point(3.0, 4.0)
print(p1.distance_from_origin())

circle: Circle = Circle("MyCircle", p1, 5.0)
rect: Rectangle = Rectangle("MyRect", 10.0, 7.0)

print(circle.area())
print(rect.area())

# Use calculator from another module
calc: ShapeCalculator = ShapeCalculator()
calc.add_shape(circle)
calc.add_shape(rect)

print(calc.get_total())

# Compare shapes across modules
winner: str = compare_shapes(circle, rect)
print(winner)

# EXPECTED OUTPUT:
# 5.0
# 78.53975
# 70.0
# 148.53975
# MyRect
```

## Error

```
Compilation failed:
  Semantic error at line 6, column 13: Undefined identifier 'Point'
  Semantic error at line 9, column 18: Undefined identifier 'Circle'
  Semantic error at line 10, column 19: Undefined identifier 'Rectangle'
  Semantic error at line 16, column 25: Undefined identifier 'ShapeCalculator'
  Semantic error at line 23, column 15: Undefined identifier 'compare_shapes'
  Semantic error: Type 'Point' not found
  Semantic error: Type 'Circle' not found
  Semantic error: Type 'Rectangle' not found
  Semantic error: Type 'ShapeCalculator' not found

```

## Timing

- Generation: 7.12s
- Execution: 0.85s
