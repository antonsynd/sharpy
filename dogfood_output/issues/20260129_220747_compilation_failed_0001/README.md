# Issue Report: compilation_failed

**Timestamp:** 2026-01-29T22:07:04.865332
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - demonstrates module imports and cross-module usage
from math_utils import Calculator, factorial, PI_APPROXIMATION
from geometry import Circle, Rectangle

def main():
    # Test Calculator from math_utils
    sum_result: int = Calculator.add(10, 5)
    print(sum_result)
    
    # Test factorial function
    fact_5: int = factorial(5)
    print(fact_5)
    
    # Test Circle from geometry (which uses math_utils internally)
    circle: Circle = Circle(5.0)
    circle_area: float = circle.area()
    print(circle_area)
    
    # Test Rectangle from geometry (which uses Calculator)
    rect: Rectangle = Rectangle(4, 6)
    rect_area: int = rect.area()
    print(rect_area)
    
    # Demonstrate imported constant
    print(PI_APPROXIMATION)

# EXPECTED OUTPUT:
# 15
# 120
# 78.53975
# 24
# 3.14159
```

## Error

```
Assembly compilation failed:
  main.cs(28,47): error CS0229: Ambiguity between 'Exports.PI_APPROXIMATION' and 'Exports.PI_APPROXIMATION'

```

## Timing

- Generation: 13.12s
- Execution: 1.51s
