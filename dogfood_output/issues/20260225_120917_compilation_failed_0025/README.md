# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T12:07:30.634985
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates module utilities across multiple modules

from math_utils import Rectangle, Circle, clamp, cube
from text_utils import reverse, truncate, TextFormatter, pluralize

def main():
    # Test math utilities
    rect: Rectangle = Rectangle(5.0, 3.0)
    circle: Circle = Circle(2.0)
    
    # Test text utilities with formatter
    formatter: TextFormatter = TextFormatter("INFO")
    message: str = formatter.format_line(f"Rectangle area: {rect.area()}")
    print(message)
    
    # Test clamp and cube
    value: float = 10.0
    clamped: float = clamp(value, 0.0, 50.0)
    cubed: float = cube(clamped)
    print(f"Cube of clamped value: {cubed}")
    
    # Test text transformations
    original: str = "Hello World"
    reversed_text: str = reverse(original)
    truncated: str = truncate(reversed_text, 5)
    print(f"Reversed and truncated: {truncated}")
    
    # Test pluralize function (imported by math_utils, now used here)
    items: int = 3
    plural_form: str = pluralize(items, "shape", "shapes")
    print(f"Created {plural_form}")

# EXPECTED OUTPUT:
# INFO Rectangle area: 15.0
# Cube of clamped value: 1000.0
# Reversed and truncated: dlroW...
# Created 3 shapes
```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'pi' does not exist in the current context
  --> /tmp/tmp0r7_mo1r/math_utils.spy:44:20

error[CS0103]: The name 'pi' does not exist in the current context
  --> /tmp/tmp0r7_mo1r/math_utils.spy:47:27


```

## Compiler Output

```
warning[SPY0452]: Imported name 'pluralize' is never used
  --> /tmp/tmp0r7_mo1r/math_utils.spy:3:26
    |
  3 | from math_utils import Rectangle, Circle, clamp, cube
    |                          ^^^^^^^^^
    |

warning[SPY0451]: Local variable 'circle' is assigned but never used
  --> /tmp/tmp0r7_mo1r/main.spy:9:5
    |
  9 |     circle: Circle = Circle(2.0)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 92.47s
- Execution: 4.36s
