# Issue Report: execution_failed

**Timestamp:** 2026-01-24T18:35:05.058563
**Type:** execution_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports
from math_operations import Calculator, square, cube
from validators import RangeValidator, is_perfect_square

def main():
    # Test Calculator class from math_operations
    calc: Calculator = Calculator(10)
    print(calc.accumulator)
    
    result1: int = calc.add(5)
    print(result1)
    
    # Test standalone functions from math_operations
    squared: int = square(4)
    print(squared)
    
    # Test RangeValidator from validators (which itself imports from math_operations)
    validator: RangeValidator = RangeValidator(1, 100)
    is_in_range: bool = validator.is_valid(50)
    print(is_in_range)
    
    # Test is_perfect_square function from validators
    check: bool = is_perfect_square(16)
    print(check)

# EXPECTED OUTPUT:
# 10
# 15
# 16
# True
# True
```

## Error

```
Compilation failed:
  Semantic error at line 7, column 24: Undefined identifier 'Calculator'
  Semantic error at line 14, column 20: Undefined identifier 'square'
  Semantic error at line 18, column 33: Undefined identifier 'RangeValidator'
  Semantic error at line 23, column 19: Undefined identifier 'is_perfect_square'
  Semantic error: Type 'Calculator' not found
  Semantic error: Type 'RangeValidator' not found

```

## Timing

- Generation: 9.86s
- Execution: 0.86s
