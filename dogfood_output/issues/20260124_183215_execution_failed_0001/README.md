# Issue Report: execution_failed

**Timestamp:** 2026-01-24T18:31:49.666875
**Type:** execution_failed
**Feature Focus:** function_default_params
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test default parameters with different argument combinations

def greet(name: str, greeting: str = "Hello", punctuation: str = "!") -> None:
    print(greeting)
    print(name)
    print(punctuation)

def calculate(base: int, multiplier: int = 2, offset: int = 0) -> int:
    return base * multiplier + offset

def main():
    greet("Alice")
    print(42)
    greet("Bob", "Hi")
    print(42)
    greet("Charlie", "Hey", ".")
    print(42)
    
    result1 = calculate(5)
    print(result1)
    
    result2 = calculate(5, 3)
    print(result2)
    
    result3 = calculate(5, 3, 10)
    print(result3)

main()

# EXPECTED OUTPUT:
# Hello
# Alice
# !
# 42
# Hi
# Bob
# !
# 42
# Hey
# Charlie
# .
# 42
# 10
# 15
# 25
```

## Error

```
Compilation failed:
  Semantic error at line 28, column 1: Executable statements are not allowed at module level

```

## Timing

- Generation: 15.90s
- Execution: 0.85s
