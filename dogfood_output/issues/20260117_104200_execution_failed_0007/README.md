# Issue Report: execution_failed

**Timestamp:** 2026-01-17T10:41:37.203097
**Type:** execution_failed
**Feature Focus:** import_statement
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# main.sharpy - Tests import statements with math utilities

import math_utils
from math_utils import square, is_even

def process_numbers(limit: int) -> None:
    print(limit)
    count: int = 0
    
    for i in range(1, limit):
        if is_even(i):
            result: int = square(i)
            print(result)
            count += 1
    
    print(count)

def demonstrate_module_access() -> None:
    val: int = 5
    cubed: int = math_utils.cube(val)
    print(cubed)
    
    pi: int = math_utils.PI_APPROX
    print(pi)

print(1)
process_numbers(6)
print(2)
demonstrate_module_access()
print(3)

# EXPECTED OUTPUT:
# 1
# 6
# 4
# 16
# 2
# 2
# 125
# 3
# 3
```

## Error

```
Compilation failed:
  Semantic error at line 11, column 12: Undefined identifier 'is_even'
  Semantic error at line 12, column 27: Undefined identifier 'square'
  Semantic error at line 20, column 18: Undefined identifier 'math_utils'
  Semantic error at line 23, column 15: Undefined identifier 'math_utils'

```

## Timing

- Generation: 8.50s
- Execution: 0.89s
