# Issue Report: execution_failed

**Timestamp:** 2026-01-29T20:17:44.275321
**Type:** execution_failed
**Feature Focus:** arithmetic_operators
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test arithmetic operators with negative numbers and mixed operations
x: int = -8
y: int = 3
z: int = 5

addition: int = x + y
subtraction: int = z - x
multiplication: int = x * y
floor_division: int = x // y
modulo: int = z % y
exponentiation: int = y ** 2

def main():
    print(addition)
    print(subtraction)
    print(multiplication)
    print(floor_division)
    print(modulo)
    print(exponentiation)

# EXPECTED OUTPUT:
# -5
# 13
# -24
# -3
# 2
# 9
```

## Error

```
Compilation failed:
  Semantic error at line 11, column 1: Cannot assign type 'double' to variable of type 'int'

```

## Timing

- Generation: 5.57s
- Execution: 0.95s
