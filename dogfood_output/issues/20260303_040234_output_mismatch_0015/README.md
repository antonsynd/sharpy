# Issue Report: output_mismatch

**Timestamp:** 2026-03-03T04:00:31.415557
**Type:** output_mismatch
**Feature Focus:** tuple_unpacking_nested
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test nested tuple unpacking with geometric shapes
# Tests: nested unpacking in assignments, control flow with unpacked values
#        arithmetic on unpacked nested values

def main():
    # Rectangle defined by two corner points: ((x1, y1), (x2, y2))
    rect: tuple[tuple[int, int], tuple[int, int]] = ((3, 5), (8, 11))
    
    # Unpack nested tuple to get all four coordinates
    (x1, y1), (x2, y2) = rect
    
    print(x1)
    print(y1)
    print(x2)
    print(y2)
    
    # Calculate dimensions using unpacked values
    width: int = x2 - x1
    height: int = y2 - y1
    area: int = width * height
    
    print(width)
    print(height)
    print(area)
    
    # Control flow using unpacked values
    if width > 5 and height > 5:
        print("large")
    else:
        print("small")
    
    # Another nested pattern: triple nesting for 3D bounds
    bounds: tuple[tuple[tuple[int, int], tuple[int, int]], int] = (((0, 0), (10, 10)), 5)
    ((min_x, min_y), (max_x, max_y)), depth = bounds
    
    print(min_x)
    print(max_x)
    print(depth)
    
    # Verify arithmetic with deeply unpacked values
    total_span: int = (max_x - min_x) + (max_y - min_y) + depth
    print(total_span)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
3
5
8
11
5
6
30
large
0
10
5
25

```

### Actual
```
3
5
8
11
5
6
30
small
0
10
5
25
```

## Timing

- Generation: 53.95s
- Execution: 4.82s
