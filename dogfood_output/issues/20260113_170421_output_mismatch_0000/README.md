# Issue Report: output_mismatch

**Timestamp:** 2026-01-13T17:04:06.369412
**Type:** output_mismatch
**Feature Focus:** float_variables
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: float_variables - basic float arithmetic
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
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_3016a118d56d404ab6c5ed11866faffe.exe

=== Running Program ===

5.140000000000001
6.28
5.25
```

## Timing

- Generation: 4.92s
- Execution: 1.31s
