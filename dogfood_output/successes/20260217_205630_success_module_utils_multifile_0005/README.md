# Successful Dogfood Run

**Timestamp:** 2026-02-17T20:53:01.595498
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### string_utils.spy

```python
# String utility module - provides text manipulation helpers

class TextFormatter:
    """Class for formatting text with various transformations"""
    prefix: str
    
    def __init__(self, prefix: str):
        self.prefix = prefix
    
    def format_line(self, text: str) -> str:
        return self.prefix + ": " + text
    
    def repeat(self, text: str, count: int) -> str:
        result: str = ""
        i: int = 0
        while i < count:
            result = result + text
            i = i + 1
        return result

def count_vowels(text: str) -> int:
    """Count vowels in a string"""
    count: int = 0
    vowels: str = "aeiouAEIOU"
    for c in text:
        if str(c) in vowels:
            count = count + 1
    return count

def reverse_words(text: str) -> str:
    """Reverse the order of words in a string"""
    words: list[str] = text.split(" ")
    result: list[str] = []
    i: int = len(words) - 1
    while i >= 0:
        result.append(words[i])
        i = i - 1
    return " ".join(result)
```

### math_utils.spy

```python
# Math utility module - provides numeric computation helpers

class Calculator:
    """Simple calculator with history tracking"""
    last_result: int
    
    def __init__(self):
        self.last_result = 0
    
    def add(self, a: int, b: int) -> int:
        self.last_result = a + b
        return self.last_result
    
    def multiply(self, a: int, b: int) -> int:
        self.last_result = a * b
        return self.last_result
    
    def get_last(self) -> int:
        return self.last_result

def factorial(n: int) -> int:
    """Calculate factorial of n"""
    if n < 0:
        return 0
    if n <= 1:
        return 1
    return n * factorial(n - 1)

def power(base: int, exp: int) -> int:
    """Calculate base raised to exp"""
    result: int = 1
    i: int = 0
    while i < exp:
        result = result * base
        i = i + 1
    return result

def sum_list(numbers: list[int]) -> int:
    """Sum all numbers in a list"""
    total: int = 0
    for n in numbers:
        total = total + n
    return total
```

### main.spy

```python
# Main entry point - demonstrates usage of utility modules

from string_utils import TextFormatter, count_vowels, reverse_words
from math_utils import Calculator, factorial, power, sum_list

def main():
    # Test string utilities
    formatter: TextFormatter = TextFormatter("INFO")
    
    line1: str = formatter.format_line("Starting computation")
    print(line1)
    
    text: str = "Hello World"
    vowel_count: int = count_vowels(text)
    print(vowel_count)
    
    reversed_text: str = reverse_words("one two three")
    print(reversed_text)
    
    repeated: str = formatter.repeat("*", 5)
    print(repeated)
    
    # Test math utilities
    calc: Calculator = Calculator()
    sum_result: int = calc.add(10, 20)
    print(sum_result)
    
    product: int = calc.multiply(3, 4)
    print(product)
    print(calc.get_last())
    
    fact: int = factorial(5)
    print(fact)
    
    pow_result: int = power(2, 4)
    print(pow_result)
    
    numbers: list[int] = [1, 2, 3, 4, 5]
    total: int = sum_list(numbers)
    print(total)
    
    # EXPECTED OUTPUT:
    # INFO: Starting computation
    # 3
    # three two one
    # *****
    # 30
    # 12
    # 12
    # 120
    # 16
    # 15
```

## Timing

- Generation: 193.94s
- Execution: 4.90s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
