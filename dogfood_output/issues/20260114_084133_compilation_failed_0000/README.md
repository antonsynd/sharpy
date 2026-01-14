# Issue Report: compilation_failed

**Timestamp:** 2026-01-14T08:41:14.360504
**Type:** compilation_failed
**Feature Focus:** float_variables
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Float variables and arithmetic operations test

def compute_average(a: float, b: float, c: float) -> float:
    total: float = a + b + c
    avg: float = total / 3.0
    return avg

def scale_value(val: float, factor: float) -> float:
    return val * factor

# Initialize float variables
x: float = 3.5
y: float = 2.0
z: float = 4.5

print(x)
print(y)
print(z)

# Compute average of three floats
average: float = compute_average(x, y, z)
print(average)

# Scale the average by a factor
scaled: float = scale_value(average, 2.0)
print(scaled)

# Test float division and floor division
ratio: float = z / y
print(ratio)

floor_result: float = 7.0 // 2.0
print(floor_result)

# EXPECTED OUTPUT:
# 3.5
# 2.0
# 4.5
# 3.333333333333333
# 6.666666666666666
# 2.25
# 3.0
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(36,46): error CS0121: The call is ambiguous between the following methods or properties: 'Math.Floor(double)' and 'Math.Floor(decimal)'

```

## Timing

- Generation: 7.30s
- Execution: 1.28s
