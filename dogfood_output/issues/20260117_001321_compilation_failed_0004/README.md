# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T00:13:04.653629
**Type:** compilation_failed
**Feature Focus:** class_static_methods
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Static methods in classes (methods without self parameter)
class MathUtils:
    def add(a: int, b: int) -> int:
        return a + b
    
    def multiply(a: int, b: int) -> int:
        return a * b
    
    def square(n: int) -> int:
        return n * n

result1 = MathUtils.add(3, 7)
result2 = MathUtils.multiply(4, 5)
result3 = MathUtils.square(6)

print(result1)
print(result2)
print(result3)

# EXPECTED OUTPUT:
# 10
# 20
# 36
```

## Error

```
Assembly compilation failed:
  error CS5001: Program does not contain a static 'Main' method suitable for an entry point

```

## Timing

- Generation: 6.94s
- Execution: 1.48s
