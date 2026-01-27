# Issue Report: compilation_failed

**Timestamp:** 2026-01-26T23:52:14.020406
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - demonstrates utilities from both modules
from string_utils import TextProcessor, reverse_string, count_vowels
from math_utils import factorial, is_even, Calculator

def main():
    # Test string utilities
    processor: TextProcessor = TextProcessor("LOG")
    message: str = processor.format_with_prefix("System started")
    print(message)
    
    word: str = "Sharpy"
    reversed_word: str = reverse_string(word)
    print(reversed_word)
    
    vowel_count: int = count_vowels("Hello World")
    print(vowel_count)
    
    # Test math utilities
    fact_5: int = factorial(5)
    print(fact_5)
    
    # Test calculator with accumulator pattern
    calc: Calculator = Calculator()
    calc.add(10)
    calc.multiply(3)
    final_result: int = calc.get_result()
    print(final_result)

# EXPECTED OUTPUT:
# LOG: System started
# yprahS
# 3
# 120
# 30
```

## Error

```
Assembly compilation failed:
  string_utils.cs(17,26): error CS0266: Cannot implicitly convert type 'uint' to 'int'. An explicit conversion exists (are you missing a cast?)
  string_utils.cs(21,23): error CS0411: The type arguments for method 'Enumerable.Append<TSource>(IEnumerable<TSource>, TSource)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
  string_utils.cs(42,28): error CS1061: 'string' does not contain a definition for '__Contains__' and no accessible extension method '__Contains__' accepting a first argument of type 'string' could be found (are you missing a using directive or an assembly reference?)

```

## Timing

- Generation: 12.23s
- Execution: 1.39s
