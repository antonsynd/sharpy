# Issue Report: output_mismatch

**Timestamp:** 2026-02-26T01:13:30.047498
**Type:** output_mismatch
**Feature Focus:** tuple_unpacking_assignment
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Advanced tuple unpacking patterns including swap, multi-return, and rest patterns
# Calculate bounding box from a list of 2D points using tuple unpacking

def get_min_max(a: int, b: int) -> tuple[int, int]:
    # Return sorted pair using tuple unpacking swap if needed
    if a < b:
        return (a, b)
    else:
        return (b, a)

def main():
    # Points data
    points: list[tuple[int, int]] = [(5, 10), (3, 8), (7, 12), (1, 4), (9, 6)]
    
    # Unpack first point to initialize bounds
    min_x, min_y = points[0]
    max_x = min_x
    max_y = min_y
    
    # Iterate over remaining points using rest unpacking
    first, *rest = points
    print(f"First: ({first[0]}, {first[1]})")
    print(f"Rest count: {len(rest)}")
    
    # Process remaining points with for loop unpacking
    for x, y in rest:
        # Swap min/max if needed using get_min_max
        min_x, _ = get_min_max(min_x, x)
        _, max_x = get_min_max(min_x, x)
        
        # Direct comparison for y
        if y < min_y:
            min_y = y
        if y > max_y:
            max_y = y
    
    # Final bounds
    width = max_x - min_x
    height = max_y - min_y
    print(f"Min: ({min_x}, {min_y})")
    print(f"Max: ({max_x}, {max_y})")
    print(f"Dimensions: ({width}, {height})")
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
First: (5, 10)
Rest count: 4
Min: (1, 4)
Max: (9, 12)
Dimensions: (8, 8)

```

### Actual
```
First: (5, 10)
Rest count: 4
Min: (5, 4)
Max: (5, 12)
Dimensions: (0, 8)
```

## Timing

- Generation: 194.63s
- Execution: 4.73s
