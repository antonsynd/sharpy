# Issue Report: internal_compiler_error

**Timestamp:** 2026-02-17T18:36:40.799248
**Type:** internal_compiler_error
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports and uses string_utils and math_utils modules

from string_utils import reverse_string, is_palindrome, StringBuilder
from math_utils import factorial, is_prime, sum_of_digits

def main():
    # Test string utilities
    word: str = "radar"
    print(is_palindrome(word))
    
    builder = StringBuilder()
    builder.append("Hello")
    builder.append(" ")
    builder.append("World")
    print(builder.to_string())
    
    # Test math utilities
    n: int = 5
    print(factorial(n))
    
    # Test prime check with digit sum
    candidate: int = 29
    is_prime_result: bool = is_prime(candidate)
    digit_sum: int = sum_of_digits(candidate)
    
    print(is_prime_result)
    print(digit_sum)

# EXPECTED OUTPUT:
# True
# Hello World
# 120
# True
# 11
```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpb4g05pci/main.spy:12:5
    |
 12 |     builder.append("Hello")
    |     ^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpb4g05pci/main.spy:13:5
    |
 13 |     builder.append(" ")
    |     ^^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpb4g05pci/main.spy:14:5
    |
 14 |     builder.append("World")
    |     ^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 154.62s
