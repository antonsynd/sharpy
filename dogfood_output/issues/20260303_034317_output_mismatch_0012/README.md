# Issue Report: output_mismatch

**Timestamp:** 2026-03-03T03:42:10.761773
**Type:** output_mismatch
**Feature Focus:** try_except_basic
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Basic try/except block catching ZeroDivisionError  
# Tests: try block execution, exception catching, control flow transfer

def main():
    x: int = 5
    y: int = 0
    z: int = 2
    
    try:
        a: int = x * z
        b: int = a // y
        print(b)
    except ZeroDivisionError:
        print(999)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
999

```

### Actual
```
2147483647
```

## Timing

- Generation: 40.76s
- Execution: 4.58s
