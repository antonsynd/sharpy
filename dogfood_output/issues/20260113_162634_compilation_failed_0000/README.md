# Issue Report: compilation_failed

**Timestamp:** 2026-01-13T16:26:21.539369
**Type:** compilation_failed
**Feature Focus:** function_calling_function
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: function calling another function
def add(a: int, b: int) -> int:
    return a + b

def multiply(x: int, y: int) -> int:
    sum_result: int = add(x, y)
    return sum_result * 2

result: int = multiply(3, 4)
print(result)
print(add(10, 5))

# EXPECTED OUTPUT:
# 14
# 15
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(6,80): error CS0116: A namespace cannot directly contain members such as fields, methods or statements
  dogfood_test.cs(6,23): error CS1514: { expected
  dogfood_test.cs(6,92): error CS1022: Type or namespace definition, or end-of-file expected

```

## Timing

- Generation: 4.68s
- Execution: 1.19s
