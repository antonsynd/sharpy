# Issue Report: compilation_failed

**Timestamp:** 2026-01-24T18:37:41.302495
**Type:** compilation_failed
**Feature Focus:** type_narrowing
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test type narrowing with nullable types
def process_value(x: int?) -> int:
    if x is not None:
        # Type narrowed to int here
        return x * 2
    else:
        return 0

def main():
    value1: int? = 5
    value2: int? = None
    
    result1 = process_value(value1)
    result2 = process_value(value2)
    
    print(result1)
    print(result2)

# EXPECTED OUTPUT:
# 10
# 0
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(16,24): error CS0266: Cannot implicitly convert type 'int?' to 'int'. An explicit conversion exists (are you missing a cast?)

```

## Timing

- Generation: 5.63s
- Execution: 1.29s
