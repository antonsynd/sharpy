# Issue Report: execution_failed

**Timestamp:** 2026-01-24T18:33:47.644016
**Type:** execution_failed
**Feature Focus:** if_else_simple
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Simple if-else test with temperature check

def main():
    temp: int = 25
    
    if temp > 30:
        print(1)
    else:
        print(0)

main()

# EXPECTED OUTPUT:
# 0
```

## Error

```
Compilation failed:
  Semantic error at line 11, column 1: Executable statements are not allowed at module level

```

## Timing

- Generation: 16.15s
- Execution: 0.86s
