# Issue Report: execution_failed

**Timestamp:** 2026-01-27T00:36:21.631013
**Type:** execution_failed
**Feature Focus:** lambda_basic
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Lambda expressions with higher-order functions
# Tests: lambda syntax, passing lambdas to functions, lambda return types

class MathOperations:
    @staticmethod
    def apply_twice(func: callable, value: int) -> int:
        result = func(value)
        return func(result)

    @staticmethod
    def combine(func1: callable, func2: callable, x: int) -> int:
        first = func1(x)
        return func2(first)

def main():
    # Basic lambda - square a number
    square = lambda n: n * n
    print(square(7))

    # Lambda with addition
    add_five = lambda x: x + 5
    print(add_five(10))

    # Apply lambda twice using higher-order function
    ops = MathOperations()
    result1 = ops.apply_twice(lambda n: n * 3, 4)
    print(result1)

    # Combine two lambdas
    result2 = ops.combine(lambda x: x + 2, lambda y: y * 10, 5)
    print(result2)

    # Multi-parameter lambda
    multiply = lambda a, b: a * b
    print(multiply(6, 8))

    # Lambda in list operations - FIXED: actually apply the lambda
    numbers: list[int] = [1, 2, 3, 4, 5]
    double_func = lambda x: x * 2
    doubled: list[int] = [double_func(x) for x in numbers]
    print(len(doubled))

# EXPECTED OUTPUT:
# 49
# 15
# 36
# 70
# 48
# 5
```

## Error

```
Compilation failed:
  Semantic error at line 7, column 18: 'func' is not callable (type: <?>)
  Semantic error at line 8, column 16: 'func' is not callable (type: <?>)
  Semantic error at line 12, column 17: 'func1' is not callable (type: <?>)
  Semantic error at line 13, column 16: 'func2' is not callable (type: <?>)
  Semantic error at line 26, column 15: Function expects 1 arguments but got 2
  Semantic error at line 30, column 15: Function expects 2 arguments but got 3
  Semantic error: Type 'callable' not found
  Semantic error at line 5, column 5: The '@staticmethod' decorator is not supported in Sharpy. Methods without a 'self' parameter are automatically static.
  Semantic error at line 10, column 5: The '@staticmethod' decorator is not supported in Sharpy. Methods without a 'self' parameter are automatically static.

```

## Timing

- Generation: 18.41s
- Execution: 0.92s
