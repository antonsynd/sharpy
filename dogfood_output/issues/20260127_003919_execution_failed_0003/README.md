# Issue Report: execution_failed

**Timestamp:** 2026-01-27T00:38:45.899003
**Type:** execution_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - demonstrates utility module usage
from utils import MathHelper, StringHelper, calculate_sum, find_max

def main():
    # Test MathHelper
    math: MathHelper = MathHelper(5)
    result1: int = math.apply(7)
    print(result1)

    power_result: int = math.power(2, 3)
    print(power_result)

    # Test StringHelper
    str_helper: StringHelper = StringHelper("INFO")
    message: str = str_helper.format_message("System ready")
    print(message)

    # Test calculate_sum function
    numbers: list[int] = [10, 20, 30]
    total: int = calculate_sum(numbers)
    print(total)

    # Test find_max function
    maximum: int = find_max(15, 42, 28)
    print(maximum)

# EXPECTED OUTPUT:
# 35
# 8
# INFO: System ready
# 60
# 42
```

## Error

```
Compilation failed:
  Semantic error at line 20, column 32: Cannot pass argument of type 'list[int]' to parameter of type 'list'

```

## Timing

- Generation: 11.71s
- Execution: 0.89s
