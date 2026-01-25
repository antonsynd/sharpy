# Issue Report: compilation_failed

**Timestamp:** 2026-01-24T19:32:20.914057
**Type:** compilation_failed
**Feature Focus:** type_narrowing
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Type narrowing with nullable types
# Tests: nullable type checking, type narrowing with 'is not None', using narrowed types

def find_positive(x: int?) -> int:
    if x is not None:
        if x > 0:
            return x
        else:
            return 0
    else:
        return 0

def main():
    val1: int? = 42
    val2: int? = None
    val3: int? = -5
    
    print(find_positive(val1))
    print(find_positive(val2))
    print(find_positive(val3))

# EXPECTED OUTPUT:
# 42
# 0
# 0
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(18,28): error CS0266: Cannot implicitly convert type 'int?' to 'int'. An explicit conversion exists (are you missing a cast?)

```

## Timing

- Generation: 5.46s
- Execution: 1.37s
