# Issue Report: compilation_failed

**Timestamp:** 2026-01-26T22:12:17.388377
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - demonstrates using multiple utility modules
from math_utils import Calculator, square, is_even
from string_utils import repeat_string, Formatter

def main():
    # Test math utilities
    calc = Calculator("MyCalc")
    sum_result: int = calc.add(10, 5)
    print(sum_result)
    
    squared: int = square(7)
    print(squared)
    
    # Test string utilities
    repeated: str = repeat_string("Hi", 3)
    print(repeated)
    
    # Test combining utilities
    even_check: bool = is_even(squared)
    print(even_check)
    
    # Test formatter class
    formatter = Formatter("Result")
    message: str = formatter.format_message("done")
    print(message)

# EXPECTED OUTPUT:
# 15
# 49
# HiHiHi
# False
# Result: done
```

## Error

```
Assembly compilation failed:
  main.cs(7,26): error CS0234: The type or namespace name 'MathUtils' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(8,26): error CS0234: The type or namespace name 'StringUtils' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(16,40): error CS0234: The type or namespace name 'MathUtils' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(19,27): error CS0103: The name 'Square' does not exist in the current context
  main.cs(21,31): error CS0103: The name 'RepeatString' does not exist in the current context
  main.cs(23,30): error CS0103: The name 'IsEven' does not exist in the current context
  main.cs(25,45): error CS0234: The type or namespace name 'StringUtils' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)

```

## Timing

- Generation: 9.37s
- Execution: 1.25s
