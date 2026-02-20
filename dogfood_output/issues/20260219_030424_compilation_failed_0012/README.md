# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T02:55:52.265593
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from both utility modules
from math_utils import PI, factorial, power, Calculator
from string_utils import VOWELS, count_vowels, capitalize_words, TextFormatter

def main():
    # Test math utilities
    print(factorial(5))
    print(power(2.0, 10.0))
    
    calc: Calculator = Calculator()
    result: float = calc.calculate("*", 3.0, 4.0)
    print(result)
    
    # Test string utilities
    text: str = "hello world"
    print(count_vowels(text))
    
    formatter: TextFormatter = TextFormatter("INFO")
    print(formatter.format("System ready"))

# EXPECTED OUTPUT:
# 120
# 1024.0
# 12.0
# 3
# INFO: System ready
```

## Error

```
Assembly compilation failed:

error[CS1950]: The best overloaded Add method 'Set<string>.Add(string)' for the collection initializer has some invalid arguments
  --> string_utils.cs:13:9
    |
 13 |     
    |     ^
    |

error[CS1503]: Argument 1: cannot convert from 'Sharpy.Set<string>' to 'string'
  --> string_utils.cs:13:9
    |
 13 |     
    |     ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'PI' is never used
  --> /tmp/tmpeu_b653v/main.spy:2:24
    |
  2 | from math_utils import PI, factorial, power, Calculator
    |                        ^^
    |

warning[SPY0452]: Imported name 'VOWELS' is never used
  --> /tmp/tmpeu_b653v/main.spy:3:26
    |
  3 | from string_utils import VOWELS, count_vowels, capitalize_words, TextFormatter
    |                          ^^^^^^
    |

warning[SPY0452]: Imported name 'capitalize_words' is never used
  --> /tmp/tmpeu_b653v/main.spy:3:48
    |
  3 | from string_utils import VOWELS, count_vowels, capitalize_words, TextFormatter
    |                                                ^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 497.59s
- Execution: 4.29s
