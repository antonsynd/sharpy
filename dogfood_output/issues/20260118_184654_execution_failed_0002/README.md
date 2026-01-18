# Issue Report: execution_failed

**Timestamp:** 2026-01-18T18:46:45.545305
**Type:** execution_failed
**Feature Focus:** from_import
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test from import with multiple items

# File: math_ops.spy (assumed to exist)
from math_ops import add_numbers, multiply_numbers

result1: int = add_numbers(7, 3)
result2: int = multiply_numbers(4, 5)

print(result1)
print(result2)

total: int = result1 + result2
print(total)

# EXPECTED OUTPUT:
# 10
# 20
# 30
```

## Error

```
Compilation failed:
  Semantic error at line 6, column 16: Undefined identifier 'add_numbers'
  Semantic error at line 7, column 16: Undefined identifier 'multiply_numbers'

```

## Timing

- Generation: 2.75s
- Execution: 0.85s
