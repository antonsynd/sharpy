# Issue Report: compilation_failed

**Timestamp:** 2026-01-27T00:39:19.589684
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - demonstrates utility modules
from math_utils import factorial, is_prime, gcd, Calculator
from string_utils import reverse_string, count_vowels, TextFormatter

def main():
    # Test math utilities
    fact5: int = factorial(5)
    print(f"Factorial of 5: {fact5}")
    
    prime_check: bool = is_prime(17)
    print(f"Is 17 prime? {prime_check}")
    
    # Test GCD
    result_gcd: int = gcd(48, 18)
    print(f"GCD of 48 and 18: {result_gcd}")
    
    # Test Calculator class
    calc = Calculator(2)
    rounded: float = calc.round_result(3.14159)
    print(f"Rounded to 2 decimals: {rounded}")
    
    # Test string utilities
    reversed_text: str = reverse_string("sharpy")
    print(f"Reversed 'sharpy': {reversed_text}")
    
    vowel_count: int = count_vowels("Hello World")
    print(f"Vowel count in 'Hello World': {vowel_count}")
    
    # Test TextFormatter
    formatter = TextFormatter("[", "]")
    formatted: str = formatter.format("wrapped")
    print(f"Formatted text: {formatted}")

# EXPECTED OUTPUT:
# Factorial of 5: 120
# Is 17 prime? True
# GCD of 48 and 18: 6
# Rounded to 2 decimals: 3.14
# Reversed 'sharpy': yprahs
# Vowel count in 'Hello World': 3
# Formatted text: [wrapped]
```

## Error

```
Assembly compilation failed:
  math_utils.cs(80,51): error CS0246: The type or namespace name 'Float' could not be found (are you missing a using directive or an assembly reference?)
  string_utils.cs(14,61): error CS0029: Cannot implicitly convert type 'System.Collections.Generic.List<char>' to 'System.Collections.Generic.List<string>'
  string_utils.cs(33,28): error CS1061: 'string' does not contain a definition for '__Contains__' and no accessible extension method '__Contains__' accepting a first argument of type 'string' could be found (are you missing a using directive or an assembly reference?)

```

## Timing

- Generation: 13.33s
- Execution: 1.28s
