# Successful Dogfood Run

**Timestamp:** 2026-03-03T04:05:40.638773
**Feature Focus:** arithmetic_operators
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test arithmetic operators in various contexts: classes, lambdas, and higher-order functions
class Calculator:
    base: int
    
    def __init__(self, initial: int):
        self.base = initial
    
    def add(self, x: int, y: int) -> int:
        return self.base + x + y
    
    def multiply_by(self, factor: int) -> None:
        self.base *= factor
    
    def compute_average(self, a: int, b: int) -> float:
        # Combined arithmetic: addition and division
        total: int = a + b
        return total / 2.0

def apply_operation(val: int, op: (int) -> int) -> int:
    # Higher-order function using arithmetic
    return op(val)

def main():
    # Basic arithmetic
    a: int = 15
    b: int = 4
    
    result1 = a + b
    result2 = a - b
    result3 = a * b
    result4 = a / b  # Float division
    result5 = a // b  # Floor division
    result6 = a % b   # Modulo
    result7 = a ** 2  # Exponentiation
    
    print(result1)
    print(result2)
    print(result3)
    print(result4)
    print(result5)
    print(result6)
    print(result7)
    
    # Arithmetic in class methods
    calc = Calculator(10)
    print(calc.add(5, 3))
    
    # Arithmetic modifying internal state
    calc.multiply_by(2)  # base becomes 20
    print(calc.base)
    
    # Arithmetic in arguments
    print(calc.add(7, 8))  # 20 + 7 + 8 = 35
    
    # Float average
    print(calc.compute_average(10, 20))  # 30 / 2.0 = 15.0
    
    # Arithmetic in lambdas
    double_value: (int) -> int = lambda x: x * 2
    triple_value: (int) -> int = lambda n: n * 3
    
    print(double_value(7))
    print(triple_value(4))
    
    # Arithmetic with higher-order function
    print(apply_operation(5, lambda n: n + 10))
    print(apply_operation(5, lambda n: n * n))

    # Operator precedence
    precedence = 2 + 3 * 4 - 5 // 2 + 6 % 4
    print(precedence)

```

## Output

```
19
11
60
3.75
3
3
225
18
20
35
15.0
14
12
15
25
14
```

## Timing

- Generation: 74.38s
- Execution: 4.91s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
