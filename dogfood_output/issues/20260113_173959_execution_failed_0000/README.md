# Issue Report: execution_failed

**Timestamp:** 2026-01-13T17:39:46.230516
**Type:** execution_failed
**Feature Focus:** simple_function
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Simple function with if/elif/else control flow
# Testing function calls and conditional logic

def classify_number(n: int) -> str:
    if n < 0:
        return "negative"
    elif n == 0:
        return "zero"
    else:
        return "positive"

def double(x: int) -> int:
    result: int = x * 2
    return result

def main():
    a: int = -5
    b: int = 0
    c: int = 7
    
    print(classify_number(a))
    print(classify_number(b))
    print(classify_number(c))
    
    doubled: int = double(c)
    print(doubled)
    
    print(classify_number(doubled))

main()

# EXPECTED OUTPUT:
# negative
# zero
# positive
# 14
# positive
```

## Error

```
Compilation failed:
  Semantic error at line 12, column 1: Function 'double' is already defined

```

## Timing

- Generation: 5.59s
- Execution: 0.82s
