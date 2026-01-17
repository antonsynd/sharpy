# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T00:47:53.062132
**Type:** compilation_failed
**Feature Focus:** class_with_loop
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Class that uses a loop to calculate factorial
class FactorialCalculator:
    limit: int
    
    def __init__(self, n: int):
        self.limit = n
        print("Calculator initialized")
    
    def calculate(self) -> int:
        result: int = 1
        i: int = 1
        while i <= self.limit:
            result *= i
            i += 1
        return result
    
    def print_steps(self) -> None:
        product: int = 1
        step: int = 1
        while step <= self.limit:
            product *= step
            print(product)
            step += 1

calc = FactorialCalculator(5)
print("Computing steps:")
calc.print_steps()
final_result: int = calc.calculate()
print("Final result:")
print(final_result)

# EXPECTED OUTPUT:
# Calculator initialized
# Computing steps:
# 1
# 2
# 6
# 24
# 120
# Final result:
# 120
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(47,41): error CS0103: The name 'calc' does not exist in the current context

```

## Timing

- Generation: 6.92s
- Execution: 1.28s
