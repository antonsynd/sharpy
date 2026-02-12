# Skipped Dogfood Run

**Timestamp:** 2026-02-11T23:42:06.737163
**Skip Reason:** Sharpy compiler error in math_utils.spy: Compilation errors:

error[SPY0403]: Entry point file requires a 'main()' function
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmp8bbajy3f/dogfood_test.spy:3:1
    |
  3 | def factorial(n: int) -> int:
    | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** claude
**Test Type:** Multi-file (3 files)

## Source Files

### math_utils.spy

```python
# Module providing mathematical utility functions

def factorial(n: int) -> int:
    if n <= 1:
        return 1
    result: int = 1
    i: int = 2
    while i <= n:
        result *= i
        i += 1
    return result

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

def gcd(a: int, b: int) -> int:
    while b != 0:
        temp: int = b
        b = a % b
        a = temp
    return a
```

### string_utils.spy

```python
# Module providing string utility functions

def reverse_string(s: str) -> str:
    # Build reversed string using a list
    chars: list[str] = []
    i: int = len(s) - 1
    while i >= 0:
        chars.append(s[i])
        i -= 1
    return "".join(chars)

def count_vowels(s: str) -> int:
    vowels: str = "aeiouAEIOU"
    count: int = 0
    i: int = 0
    while i < len(s):
        if s[i] in vowels:
            count += 1
        i += 1
    return count

class StringAnalyzer:
    text: str
    
    def __init__(self, text: str):
        self.text = text
    
    def get_length(self) -> int:
        return len(self.text)
    
    def count_words(self) -> int:
        # Simple word count by splitting on spaces
        if len(self.text) == 0:
            return 0
        words: list[str] = self.text.split()
        return len(words)
```

### main.spy

```python
# Main entry point - demonstrates utility modules
from math_utils import factorial, is_prime, gcd
from string_utils import reverse_string, count_vowels, StringAnalyzer

def main():
    # Test math utilities
    fact5: int = factorial(5)
    print(f"Factorial of 5: {fact5}")
    
    prime_check: bool = is_prime(17)
    print(f"Is 17 prime? {prime_check}")
    
    gcd_result: int = gcd(48, 18)
    print(f"GCD of 48 and 18: {gcd_result}")
    
    # Test string utilities
    reversed_text: str = reverse_string("hello")
    print(f"Reversed 'hello': {reversed_text}")
    
    vowel_count: int = count_vowels("beautiful")
    print(f"Vowels in 'beautiful': {vowel_count}")
    
    # Test StringAnalyzer class
    analyzer = StringAnalyzer("The quick brown fox")
    text_length: int = analyzer.get_length()
    word_count: int = analyzer.count_words()
    print(f"Text length: {text_length}")
    print(f"Word count: {word_count}")

# EXPECTED OUTPUT:
# Factorial of 5: 120
# Is 17 prime? True
# GCD of 48 and 18: 6
# Reversed 'hello': olleh
# Vowels in 'beautiful': 5
# Text length: 19
# Word count: 4
```

## Timing

- Generation: 12.38s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
