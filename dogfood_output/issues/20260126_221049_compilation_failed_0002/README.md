# Issue Report: compilation_failed

**Timestamp:** 2026-01-26T22:10:15.085976
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - tests module imports and cross-module usage
from math_utils import factorial, is_even, Calculator
from geometry import Rectangle

def main():
    # Test importing function from math_utils
    fact_5: int = factorial(5)
    print(fact_5)

    # Test importing class from math_utils
    calc = Calculator("BasicCalc")
    sum_result: int = calc.add(10, 7)
    print(sum_result)

    # Test importing from geometry (which itself imports from math_utils)
    rect = Rectangle(4, 6)
    rect_area: int = rect.area()
    print(rect_area)

    # Test multiple imports working together
    prod: int = calc.multiply(3, 8)
    check_even: bool = is_even(prod)
    print(check_even)

    # Test perimeter calculation
    rect_perim: int = rect.perimeter()
    print(rect_perim)

# EXPECTED OUTPUT:
# 120
# 17
# 24
# True
# 20
```

## Error

```
Assembly compilation failed:
  main.cs(7,26): error CS0234: The type or namespace name 'MathUtils' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(8,26): error CS0234: The type or namespace name 'Geometry' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(16,25): error CS0103: The name 'Factorial' does not exist in the current context
  main.cs(18,40): error CS0234: The type or namespace name 'MathUtils' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(21,40): error CS0234: The type or namespace name 'Geometry' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(25,30): error CS0103: The name 'IsEven' does not exist in the current context

```

## Timing

- Generation: 9.93s
- Execution: 1.27s
