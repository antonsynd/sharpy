# Issue Report: execution_failed

**Timestamp:** 2026-01-24T18:36:29.020118
**Type:** execution_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage
from models import Rectangle, Circle
from calculator import ShapeCalculator, compare_shapes

def main():
    rect: Rectangle = Rectangle("MyRectangle", 5.0, 3.0)
    print(rect.area())
    print(rect.perimeter())

    circ: Circle = Circle("MyCircle", 2.0)
    print(circ.area())

    calc: ShapeCalculator = ShapeCalculator()
    calc.add_shape(rect)
    calc.add_shape(circ)
    print(calc.get_total())

    winner: str = compare_shapes(rect, circ)
    print(winner)

# EXPECTED OUTPUT:
# 15
# 16
# 12.56636
# 27.56636
# MyRectangle
```

## Error

```
Compilation failed:
  Semantic error at line 6, column 23: Undefined identifier 'Rectangle'
  Semantic error at line 10, column 20: Undefined identifier 'Circle'
  Semantic error at line 13, column 29: Undefined identifier 'ShapeCalculator'
  Semantic error at line 18, column 19: Undefined identifier 'compare_shapes'
  Semantic error: Type 'Rectangle' not found
  Semantic error: Type 'Circle' not found
  Semantic error: Type 'ShapeCalculator' not found

```

## Timing

- Generation: 10.61s
- Execution: 0.85s
