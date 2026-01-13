# Issue Report: output_mismatch

**Timestamp:** 2026-01-13T17:04:40.364656
**Type:** output_mismatch
**Feature Focus:** float_variables
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: float_variables - basic float declaration and arithmetic

a: float = 3.14
b: float = 2.5
c: float = a + b
d: float = a * b

print(a)
print(b)
print(c)
print(d)

# EXPECTED OUTPUT:
# 3.14
# 2.5
# 5.64
# 7.85
```

## Output Comparison

### Expected
```
3.14
2.5
5.64
7.85
```

### Actual
```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_66cb360dc1b448cc924b2c1cb01e5fe3.exe

=== Running Program ===

3.14
2.5
5.640000000000001
7.8500000000000005
```

## Timing

- Generation: 4.43s
- Execution: 1.31s
