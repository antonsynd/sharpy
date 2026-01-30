# Issue Report: compilation_failed

**Timestamp:** 2026-01-29T22:08:53.394830
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module utilities
from string_utils import reverse_string, count_vowels, TextAnalyzer
from math_utils import factorial, is_palindrome_number, Calculator

def main():
    # Test string utilities
    original: str = "hello"
    reversed: str = reverse_string(original)
    print(f"Reversed '{original}': {reversed}")
    
    vowels: int = count_vowels("Beautiful")
    print(f"Vowels in 'Beautiful': {vowels}")
    
    # Test text analyzer
    analyzer = TextAnalyzer("quick brown fox")
    summary: str = analyzer.get_summary()
    print(summary)
    
    # Test math utilities with cross-module dependency
    fact_5: int = factorial(5)
    print(f"5! = {fact_5}")
    
    is_pal: bool = is_palindrome_number(12321)
    print(f"Is 12321 palindrome? {is_pal}")
    
    # Test calculator class
    calc = Calculator()
    sum_result: int = calc.add_and_store(10, 15)
    product: int = calc.multiply_with_last(3)
    print(f"Calculator: (10+15)*3 = {product}")

# EXPECTED OUTPUT:
# Reversed 'hello': olleh
# Vowels in 'Beautiful': 5
# Words: 3, Vowels: 4, Length: 15
# 5! = 120
# Is 12321 palindrome? True
# Calculator: (10+15)*3 = 75
```

## Error

```
Assembly compilation failed:
  main.cs(19,31): error CS0121: The call is ambiguous between the following methods or properties: 'Sharpy.Main.StringUtils.Exports.ReverseString(string)' and 'Sharpy.Main.MathUtils.Exports.ReverseString(string)'
  string_utils.cs(69,21): error CS0019: Operator '==' cannot be applied to operands of type 'char' and 'string'
  string_utils.cs(85,30): error CS0103: The name 'CountVowels' does not exist in the current context
  string_utils.cs(86,106): error CS0103: The name 'global' does not exist in the current context
  string_utils.cs(38,32): error CS0029: Cannot implicitly convert type 'char' to 'string'
  string_utils.cs(39,28): error CS1061: 'string' does not contain a definition for '__Contains__' and no accessible extension method '__Contains__' accepting a first argument of type 'string' could be found (are you missing a using directive or an assembly reference?)

```

## Timing

- Generation: 17.74s
- Execution: 5.44s
