# Issue Report: output_mismatch

**Timestamp:** 2026-03-08T10:23:26.226338
**Type:** output_mismatch
**Feature Focus:** partial_application
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Partial application with operator sections in data transformation pipeline
def apply_transform(value: int, fn: (int) -> int) -> int:
    return fn(value)

def clamp(min_val: int, max_val: int, x: int) -> int:
    if x < min_val:
        return min_val
    if x > max_val:
        return max_val
    return x

def lerp(start: float, end: float, t: float) -> float:
    return start + (end - start) * t

def main():
    # Partial application: clamp with fixed bounds
    clamp_0_100 = clamp(0, 100, _)
    
    # Test clamping various values
    print(clamp_0_100(-10))
    print(clamp_0_100(50))
    print(clamp_0_100(150))
    
    # Operator section: double value
    double = (_ * 2)
    print(double(7))
    
    # Partial application with apply_transform
    double_transform = apply_transform(_, (_ * 2))
    print(double_transform(5))
    
    # Partial lerp for interpolation factor
    lerp_half = lerp(0.0, 100.0, _)
    result = lerp_half(0.5)
    print(result)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
0
50
100
14
10
50.0

```

### Actual
```
0
50
100
7.0
10
50.0
```

## Timing

- Generation: 97.84s
- Execution: 5.05s
