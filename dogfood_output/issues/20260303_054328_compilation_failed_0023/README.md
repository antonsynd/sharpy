# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T05:42:18.800281
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main module importing from math_utils and string_utils
from math_utils import Calculator, square, cube, circle_area
from string_utils import format_number, repeat_text, TextFormatter

def main():
    # Create calculator and do some operations
    calc = Calculator()
    
    # Test calculator methods
    sum_result: float = calc.add(5.0, 3.0)
    print(square(sum_result))
    
    prod_result: float = calc.multiply(4.0, 7.0)
    print(cube(2.0))
    
    # Format and print results
    fmt = TextFormatter(">> ")
    message: str = fmt.format_result("Area", circle_area(3.0))
    print(message)
    
    # Repeat a status message
    status: str = repeat_text("OK ", 3)
    print(status)
    
    # Show history length
    history = calc.get_history()
    print(len(history))

```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'pi' does not exist in the current context
  --> /tmp/tmp1j45coi1/math_utils.spy:31:16


```

## Compiler Output

```
warning[SPY0451]: Local variable 'prod_result' is assigned but never used
  --> /tmp/tmp1j45coi1/main.spy:13:5
    |
 13 |     prod_result: float = calc.multiply(4.0, 7.0)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'format_number' is never used
  --> /tmp/tmp1j45coi1/main.spy:3:26
    |
  3 | from string_utils import format_number, repeat_text, TextFormatter
    |                          ^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 54.04s
- Execution: 4.78s
