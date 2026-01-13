# Issue Report: execution_failed

**Timestamp:** 2026-01-13T17:39:59.829926
**Type:** execution_failed
**Feature Focus:** simple_function
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Simple function test: calculate double of a number
def double(n: int) -> int:
    result: int = n * 2
    return result

x: int = 7
doubled: int = double(x)
print(doubled)

y: int = double(15)
print(y)

# EXPECTED OUTPUT:
# 14
# 30
```

## Error

```
Compilation failed:
  Semantic error at line 2, column 1: Function 'double' is already defined

```

## Timing

- Generation: 4.94s
- Execution: 0.83s
