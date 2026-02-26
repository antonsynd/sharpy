# Successful Dogfood Run

**Timestamp:** 2026-02-25T01:30:45.037039
**Feature Focus:** class_with_loop
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Class with loop - Digit analysis using while loops
# Tests: Class with methods containing while loops, arithmetic operations

class DigitAnalyzer:
    value: int
    
    def __init__(self, n: int):
        self.value = n
    
    def sum_digits(self) -> int:
        total: int = 0
        remaining: int = self.value
        if remaining < 0:
            remaining = -remaining
        while remaining > 0:
            digit: int = remaining % 10
            total += digit
            remaining //= 10
        return total
    
    def product_digits(self) -> int:
        product: int = 1
        remaining: int = self.value
        if remaining < 0:
            remaining = -remaining
        if remaining == 0:
            return 0
        while remaining > 0:
            digit: int = remaining % 10
            product *= digit
            remaining //= 10
        return product
    
    def count_digits(self) -> int:
        count: int = 0
        remaining: int = self.value
        if remaining < 0:
            remaining = -remaining
        if remaining == 0:
            return 1
        while remaining > 0:
            count += 1
            remaining //= 10
        return count

def main():
    analyzer = DigitAnalyzer(12345)
    print(analyzer.sum_digits())
    print(analyzer.product_digits())
    print(analyzer.count_digits())
    
    analyzer2 = DigitAnalyzer(-987)
    print(analyzer2.sum_digits())
    
    analyzer3 = DigitAnalyzer(0)
    print(analyzer3.count_digits())
    print(analyzer3.product_digits())
    
    # EXPECTED OUTPUT:
    # 15
    # 120
    # 5
    # 24
    # 1
    # 0
```

## Output

```
15
120
5
24
1
0
```

## Timing

- Generation: 77.61s
- Execution: 4.42s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
