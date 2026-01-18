# Issue Report: output_mismatch

**Timestamp:** 2026-01-18T13:18:43.840983
**Type:** output_mismatch
**Feature Focus:** simple_function
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Simple function: Calculate square of a number
def square(n: int) -> int:
    return n * n

def add_squares(a: int, b: int) -> int:
    return square(a) + square(b)

result1: int = square(7)
result2: int = add_squares(3, 4)

print(result1)
print(result2)

# EXPECTED OUTPUT:
# 49
# 25
```

## Output Comparison

### Expected
```
49
25

```

### Actual
```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_0c6d7dc88ad243b98d42a230129103b3.exe

=== Running Program ===

49
25
```

## Timing

- Generation: 4.49s
- Execution: 1.35s
