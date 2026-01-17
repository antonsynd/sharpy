# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T09:46:25.842915
**Type:** compilation_failed
**Feature Focus:** function_with_print
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Functions with print statements showing computation steps

def compute_factorial(n: int) -> int:
    print(n)
    result: int = 1
    i: int = 1
    while i <= n:
        result *= i
        i += 1
    return result

def show_factorial_result(n: int, result: int) -> None:
    print(n)
    print(result)

def main() -> None:
    num: int = 5
    print(num)
    
    factorial_result: int = compute_factorial(num)
    
    show_factorial_result(num, factorial_result)

main()

# EXPECTED OUTPUT:
# 5
# 5
# 5
# 120
```

## Error

```
Compilation failed:
  Cannot have module-level executable statements when a 'main' function is defined. The main function is automatically invoked as the entry point.

```

## Timing

- Generation: 5.08s
- Execution: 0.89s
