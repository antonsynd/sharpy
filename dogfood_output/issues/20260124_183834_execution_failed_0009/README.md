# Issue Report: execution_failed

**Timestamp:** 2026-01-24T18:37:54.597670
**Type:** execution_failed
**Feature Focus:** generic_function
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test generic functions with multiple type parameters
# Tests: generic functions, type parameters, nullable types, simple operations

def swap[T, U](first: T, second: U) -> None:
    print(first)
    print(second)

def create_pair[T](value: T, count: int) -> T:
    i: int = 0
    while i < count:
        print(value)
        i = i + 1
    return value

def maybe_default[T](nullable_val: T?, default_val: T) -> T:
    result: T = nullable_val ?? default_val
    return result

def get_int_value(val: int) -> int:
    return val

def get_float_value(val: float) -> float:
    return val

def main():
    swap(42, 3.14)
    swap(True, False)
    
    val1: int = get_int_value(100)
    val2: int = get_int_value(250)
    print(val2)
    
    fval1: float = get_float_value(7.5)
    fval2: float = get_float_value(2.1)
    print(fval1)
    
    result: int = create_pair(99, 3)
    print(result)
    
    nullable1: int? = None
    output1: int = maybe_default(nullable1, 42)
    print(output1)
    
    nullable2: int? = 77
    output2: int = maybe_default(nullable2, 42)
    print(output2)

# EXPECTED OUTPUT:
# 42
# 3.14
# True
# False
# 250
# 7.5
# 99
# 99
# 99
# 99
# 42
# 77
```

## Error

```
Compilation failed:
  Semantic error at line 26, column 10: Cannot pass argument of type 'int' to parameter of type 'T'
  Semantic error at line 26, column 14: Cannot pass argument of type 'double' to parameter of type 'U'
  Semantic error at line 27, column 10: Cannot pass argument of type 'bool' to parameter of type 'T'
  Semantic error at line 27, column 16: Cannot pass argument of type 'bool' to parameter of type 'U'
  Semantic error at line 37, column 31: Cannot pass argument of type 'int' to parameter of type 'T'
  Semantic error at line 37, column 5: Cannot assign type 'T' to variable of type 'int'
  Semantic error at line 41, column 34: Cannot pass argument of type 'int?' to parameter of type 'T?'
  Semantic error at line 41, column 45: Cannot pass argument of type 'int' to parameter of type 'T'
  Semantic error at line 41, column 5: Cannot assign type 'T' to variable of type 'int'
  Semantic error at line 45, column 34: Cannot pass argument of type 'int?' to parameter of type 'T?'
  Semantic error at line 45, column 45: Cannot pass argument of type 'int' to parameter of type 'T'
  Semantic error at line 45, column 5: Cannot assign type 'T' to variable of type 'int'

```

## Timing

- Generation: 18.32s
- Execution: 0.85s
