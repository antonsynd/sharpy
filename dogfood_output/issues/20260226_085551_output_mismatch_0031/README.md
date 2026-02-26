# Issue Report: output_mismatch

**Timestamp:** 2026-02-26T08:52:40.665701
**Type:** output_mismatch
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests module imports and cross-module inheritance

from math_utils import factorial
from shapes import calculate_area, AdvancedCalculator

def main():
    # Test imported function from math_utils
    print(factorial(5))
    
    # Test cross-module inheritance with @virtual/@override
    calc = AdvancedCalculator(10)
    calc.add(5)
    calc.add(3)
    print(calc.get_total())
    print(calc.get_product())
    
    # Test imported function from shapes module
    print(calculate_area(4, 5))
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
120
18
15
20

```

### Actual
```
120
18
3
20
```

## Timing

- Generation: 139.56s
- Execution: 4.43s
