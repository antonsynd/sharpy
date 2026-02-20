# Successful Dogfood Run

**Timestamp:** 2026-02-19T07:34:24.389392
**Feature Focus:** class_with_loop
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test class with loop: Factorial calculator using iteration
class FactorialCalculator:
    n: int
    
    def __init__(self, n: int):
        self.n = n
    
    def calculate(self) -> int:
        if self.n < 0:
            return 0
        result: int = 1
        i: int = 2
        while i <= self.n:
            result *= i
            i += 1
        return result

def main():
    calc = FactorialCalculator(5)
    print(calc.calculate())
    calc2 = FactorialCalculator(0)
    print(calc2.calculate())
    calc3 = FactorialCalculator(7)
    print(calc3.calculate())
    # EXPECTED OUTPUT:
    # 120
    # 1
    # 5040
```

## Output

```
120
1
5040
```

## Timing

- Generation: 360.06s
- Execution: 4.28s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
