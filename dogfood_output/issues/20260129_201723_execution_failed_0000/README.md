# Issue Report: execution_failed

**Timestamp:** 2026-01-29T20:17:04.561447
**Type:** execution_failed
**Feature Focus:** generic_function
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Generic function with min/max operations on different types
# Tests type parameter inference across int, float, and str types

def min_value[T](a: T, b: T) -> T:
    if a < b:
        return a
    return b

def max_value[T](a: T, b: T) -> T:
    if a > b:
        return a
    return b

def main():
    print(min_value(10, 5))
    print(max_value(10, 5))
    print(min_value(3.14, 2.71))
    print(max_value(3.14, 2.71))
    print(min_value("zebra", "apple"))
    print(max_value("zebra", "apple"))

# EXPECTED OUTPUT:
# 5
# 10
# 2.71
# 3.14
# apple
# zebra
```

## Error

```
Compilation failed:
  Semantic error at line 5, column 8: Type 'T' does not support operator '<' with operand of type 'T'
  Semantic error at line 10, column 8: Type 'T' does not support operator '>' with operand of type 'T'

```

## Timing

- Generation: 8.67s
- Execution: 0.99s
