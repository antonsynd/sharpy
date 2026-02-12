# Skipped Dogfood Run

**Timestamp:** 2026-02-11T23:43:30.463217
**Skip Reason:** Sharpy compiler error in math_utils.spy: Compilation errors:

error[SPY0403]: Entry point file requires a 'main()' function
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmp1wew0j_k/dogfood_test.spy:3:1
    |
  3 | def is_prime(n: int) -> bool:
    | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** claude
**Test Type:** Multi-file (3 files)

## Source Files

### math_utils.spy

```python
# Module providing mathematical utility functions and classes

def is_prime(n: int) -> bool:
    if n < 2:
        return False
    if n == 2:
        return True
    if n % 2 == 0:
        return False
    
    i: int = 3
    while i * i <= n:
        if n % i == 0:
            return False
        i += 2
    return True

def factorial(n: int) -> int:
    if n <= 1:
        return 1
    result: int = 1
    i: int = 2
    while i <= n:
        result *= i
        i += 1
    return result

class Statistics:
    numbers: list[int]
    
    def __init__(self, nums: list[int]):
        self.numbers = nums
    
    def get_sum(self) -> int:
        total: int = 0
        for num in self.numbers:
            total += num
        return total
    
    def get_average(self) -> float:
        total: int = self.get_sum()
        count: int = len(self.numbers)
        return float(total) / float(count)
```

### string_utils.spy

```python
# Module providing string utility functions

def count_vowels(text: str) -> int:
    vowels: str = "aeiouAEIOU"
    count: int = 0
    i: int = 0
    while i < len(text):
        char: str = text[i:i+1]
        if char in vowels:
            count += 1
        i += 1
    return count

def reverse_string(text: str) -> str:
    result: str = ""
    i: int = len(text) - 1
    while i >= 0:
        result += text[i:i+1]
        i -= 1
    return result
```

### main.spy

```python
# Main entry point - demonstrates utility modules
from math_utils import is_prime, factorial, Statistics
from string_utils import count_vowels, reverse_string

def main():
    # Test prime checking
    num: int = 17
    print(is_prime(num))
    
    # Test factorial
    fact_result: int = factorial(5)
    print(fact_result)
    
    # Test statistics
    data: list[int] = [10, 20, 30, 40]
    stats = Statistics(data)
    print(stats.get_sum())
    print(stats.get_average())
    
    # Test string utilities
    text: str = "Hello"
    vowel_count: int = count_vowels(text)
    print(vowel_count)
    
    reversed_text: str = reverse_string(text)
    print(reversed_text)

# EXPECTED OUTPUT:
# True
# 120
# 100
# 25.0
# 2
# olleH
```

## Timing

- Generation: 10.61s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
