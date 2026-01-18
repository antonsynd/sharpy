# Issue Report: execution_failed

**Timestamp:** 2026-01-18T18:45:37.514587
**Type:** execution_failed
**Feature Focus:** generic_function
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test generic function with type parameter
def swap[T](a: T, b: T) -> T:
    temp: T = a
    a = b
    return a

x: int = 42
y: int = 100
result: int = swap(x, y)
print(result)

a: float = 3.14
b: float = 2.71
result_f: float = swap(a, b)
print(result_f)

# EXPECTED OUTPUT:
# 100
# 2.71
```

## Error

```
Compilation failed:
  Semantic error at line 9, column 20: Cannot pass argument of type 'int' to parameter of type 'T'
  Semantic error at line 9, column 23: Cannot pass argument of type 'int' to parameter of type 'T'
  Semantic error at line 9, column 1: Cannot assign type 'T' to variable of type 'int'
  Semantic error at line 14, column 24: Cannot pass argument of type 'float' to parameter of type 'T'
  Semantic error at line 14, column 27: Cannot pass argument of type 'float' to parameter of type 'T'
  Semantic error at line 14, column 1: Cannot assign type 'T' to variable of type 'float'

```

## Timing

- Generation: 4.29s
- Execution: 0.86s
