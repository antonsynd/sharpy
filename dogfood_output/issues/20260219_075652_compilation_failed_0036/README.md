# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T07:54:09.731815
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating module utility imports
# Tests cross-module usage of utility functions and classes

from string_utils import to_upper_case, truncate, TextFormatter, join_items
from math_utils import square, clamp, Calculator, calculate_circle_area, PI

def main():
    # Test string utilities
    original: str = "hello world"
    print(to_upper_case(original))
    
    long_text: str = "this is a very long message"
    print(truncate(long_text, 10))
    
    # Test TextFormatter class from string_utils
    formatter: TextFormatter = TextFormatter("INFO")
    print(formatter.format_message("system ready"))
    
    # Test list joining
    items: list[str] = ["apple", "banana", "cherry"]
    print(join_items(items))
    
    # Test math utilities
    calc: Calculator = Calculator(10)
    calc.add(5)
    print(calc.total)
    
    # Test clamp function
    print(clamp(150, 0, 100))
    
    # Test circle area calculation
    area: float = calculate_circle_area(5.0)
    print(f"Area: {area:.2f}")

# EXPECTED OUTPUT:
# HELLO WORLD
# this is a ...
# INFO: system ready
# apple, banana, cherry
# 15
# 100
# Area: 78.54
```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'defaultSeparator' does not exist in the current context
  --> /tmp/tmpjb8xor38/string_utils.spy:25:82
    |
 25 |     calc.add(5)
    |                ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'square' is never used
  --> /tmp/tmpjb8xor38/main.spy:5:24
    |
  5 | from math_utils import square, clamp, Calculator, calculate_circle_area, PI
    |                        ^^^^^^
    |

warning[SPY0452]: Imported name 'PI' is never used
  --> /tmp/tmpjb8xor38/main.spy:5:74
    |
  5 | from math_utils import square, clamp, Calculator, calculate_circle_area, PI
    |                                                                          ^^
    |


```

## Timing

- Generation: 148.23s
- Execution: 4.19s
