# Issue Report: compilation_failed

**Timestamp:** 2026-01-13T16:17:13.091072
**Type:** compilation_failed
**Feature Focus:** function_calls
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
def multiply(a: int, b: int) -> int:
    return a * b

result: int = multiply(7, 6)
print(result)

# EXPECTED OUTPUT:
# 42
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(6,81): error CS0116: A namespace cannot directly contain members such as fields, methods or statements
  dogfood_test.cs(6,23): error CS1514: { expected
  dogfood_test.cs(6,93): error CS1022: Type or namespace definition, or end-of-file expected

```

## Timing

- Generation: 3.91s
- Execution: 1.20s
