# Issue Report: output_mismatch

**Timestamp:** 2026-01-13T17:06:57.888174
**Type:** output_mismatch
**Feature Focus:** float_variables
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test float variables and basic arithmetic
a: float = 3.14
b: float = 2.0
c: float = a + b
print(c)

d: float = a * b
print(d)

e: float = 10.5 / 2.0
print(e)

# EXPECTED OUTPUT:
# 5.14
# 6.28
# 5.25
```

## Output Comparison

### Expected
```
5.14
6.28
5.25
```

### Actual
```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_33eb3f1b3bbf45129aa587e45feb3a04.exe

=== Running Program ===

5.140000000000001
6.28
5.25
```

## Timing

- Generation: 4.45s
- Execution: 1.36s
