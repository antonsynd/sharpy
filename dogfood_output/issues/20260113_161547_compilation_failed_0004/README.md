# Issue Report: compilation_failed

**Timestamp:** 2026-01-13T16:15:32.102187
**Type:** compilation_failed
**Feature Focus:** functions
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
def square(n: int) -> int:
    return n * n

result = square(7)
print(result)

# EXPECTED OUTPUT:
# 49
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(6,81): error CS0116: A namespace cannot directly contain members such as fields, methods or statements
  dogfood_test.cs(6,23): error CS1514: { expected
  dogfood_test.cs(6,93): error CS1022: Type or namespace definition, or end-of-file expected

```

## Timing

- Generation: 3.94s
- Execution: 1.21s
