# Issue Report: output_mismatch

**Timestamp:** 2026-02-25T07:22:53.443128
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main module demonstrating module utilities import and usage

from math_utils import square, cube, average, clamp, Calculator
from text_utils import repeat, pad_left, truncate, is_palindrome, TextFormatter

def main():
    # Test math utility functions
    print(square(5))
    print(cube(3))
    
    # Test average with list literal
    scores: list[int] = [85, 92, 78, 95, 88]
    avg: float = average(scores)
    print(avg)
    
    # Test clamp function
    print(clamp(50, 0, 100))
    print(clamp(-10, 0, 100))
    print(clamp(150, 0, 100))
    
    # Test Calculator class from math_utils
    calc = Calculator()
    calc.add(10.0)
    calc.multiply(2.5)
    print(calc.result)
    calc.clear()
    print(calc.result)
    
    # Test text utility functions
    print(repeat("*", 5))
    print(pad_left("hello", 10))
    print(truncate("This is a very long text message", 15))
    
    # Test palindrome checker
    print(is_palindrome("radar"))
    print(is_palindrome("hello"))
    
    # Test TextFormatter class
    formatter = TextFormatter.with_brackets()
    print(formatter.format("item"))
    
    # Test custom formatter
    custom_formatter = TextFormatter("<<", ">>")
    print(custom_formatter.format("value"))

# EXPECTED OUTPUT:
# 25
# 27
# 87.6
# 50
# 0
# 100
# 25.0
# 0.0
# *****
#      hello
# This is a ver...
# True
# False
# [item]
# <<value>>
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
25
27
87.6
50
0
100
25.0
0.0
*****
hello
This is a ver...
True
False
[item]
<<value>>

```

### Actual
```
25
27
87.6
50
0
100
25.0
0.0
*****
     hello
This is a ve...
True
False
[item]
<<value>>
```

## Timing

- Generation: 182.44s
- Execution: 4.62s
