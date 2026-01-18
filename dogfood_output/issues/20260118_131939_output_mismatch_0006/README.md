# Issue Report: output_mismatch

**Timestamp:** 2026-01-18T13:19:23.558104
**Type:** output_mismatch
**Feature Focus:** simple_function
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Simple function with parameters and return value
def add_three_numbers(a: int, b: int, c: int) -> int:
    result: int = a + b + c
    return result

x: int = 5
y: int = 12
z: int = 8
sum_value: int = add_three_numbers(x, y, z)
print(sum_value)

# EXPECTED OUTPUT:
# 25
```

## Output Comparison

### Expected
```
25

```

### Actual
```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_b64142efc26840d0b5e7df9454482fbb.exe

=== Running Program ===

25
```

## Timing

- Generation: 2.66s
- Execution: 1.35s
