# Issue Report: output_mismatch

**Timestamp:** 2026-02-17T19:04:32.827460
**Type:** output_mismatch
**Feature Focus:** dotnet_import
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Import and use System.Math methods
# Tests: from system import, .NET static method calls
from system import Math

def main():
    # Test Math.Abs with float
    x: float = -3.5
    abs_val: float = Math.Abs(x)
    print(abs_val)

    # Test Math.Max with ints
    a: int = 10
    b: int = 25
    max_val: int = Math.Max(a, b)
    print(max_val)

    # Test Math.Sqrt
    val: float = 16.0
    root: float = Math.Sqrt(val)
    print(root)

    # EXPECTED OUTPUT:
    # 3.5
    # 25
    # 4
```

## Error

```
AI verification backend failure
```

## Output Comparison

### Expected
```
3.5
25
4

```

### Actual
```
3.5
25
4.0
```

## Timing

- Generation: 299.34s
- Execution: 4.64s
