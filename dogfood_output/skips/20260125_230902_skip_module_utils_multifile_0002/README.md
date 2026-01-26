# Skipped Dogfood Run

**Timestamp:** 2026-01-25T23:08:52.880986
**Skip Reason:** Unsupported feature in math_utils.spy: Line 4: with statement (not implemented) - '"""Basic calculator with operation history"""...'
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** claude
**Test Type:** Multi-file (2 files)

## Source Files

### math_utils.spy

```python
# Utility module for mathematical operations

class Calculator:
    """Basic calculator with operation history"""
    result: float
    operation_count: int
    
    def __init__(self):
        self.result = 0.0
        self.operation_count = 0
    
    def add(self, value: float) -> float:
        self.result += value
        self.operation_count += 1
        return self.result
    
    def multiply(self, value: float) -> float:
        self.result *= value
        self.operation_count += 1
        return self.result
    
    def get_stats(self) -> str:
        return f"Operations: {self.operation_count}, Result: {self.result}"

def factorial(n: int) -> int:
    """Calculate factorial iteratively"""
    if n <= 1:
        return 1
    result: int = 1
    i: int = 2
    while i <= n:
        result *= i
        i += 1
    return result

def is_prime(n: int) -> bool:
    """Check if a number is prime"""
    if n < 2:
        return False
    if n == 2:
        return True
    i: int = 2
    while i * i <= n:
        if n % i == 0:
            return False
        i += 1
    return True
```

### main.spy

```python
# Main entry point - demonstrates math utilities
from math_utils import Calculator, factorial, is_prime

def main():
    print("=== Math Utilities Demo ===")
    
    # Test Calculator class
    calc = Calculator()
    calc.add(10.0)
    print(calc.result)
    
    calc.multiply(3.0)
    print(calc.result)
    
    print(calc.get_stats())
    
    # Test factorial function
    fact_5: int = factorial(5)
    print(fact_5)
    
    # Test prime checking
    prime_check: bool = is_prime(7)
    print(prime_check)

# EXPECTED OUTPUT:
# === Math Utilities Demo ===
# 10
# 30
# Operations: 2, Result: 30
# 120
# True
```

## Timing

- Generation: 10.09s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.10).

This output is saved for inspection to help improve prompting.
