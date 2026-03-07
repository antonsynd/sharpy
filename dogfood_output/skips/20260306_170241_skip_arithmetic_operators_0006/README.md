# Skipped Dogfood Run

**Timestamp:** 2026-03-06T16:55:09.331693
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0100]: Unexpected token: Less
  --> /tmp/tmp09430qo9/dogfood_test.spy:78:1
    |
 78 | </expected>
    | ^
    |


**Feature Focus:** arithmetic_operators
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test arithmetic operators with medium complexity
# Testing basic operators, augmented assignments, and operator precedence

def main():
    # Basic arithmetic operations
    a: int = 15
    b: int = 4
    
    # Addition
    result: int = a + b
    print(result)
    
    # Subtraction
    result = a - b
    print(result)
    
    # Multiplication
    result = a * b
    print(result)
    
    # Float division
    float_result: float = a / b
    print(float_result)
    
    # Floor division
    result = a // b
    print(result)
    
    # Modulo
    result = a % b
    print(result)
    
    # Exponentiation
    result = a ** 2
    print(result)
    
    # Negative numbers
    neg: int = -10
    result = -neg
    print(result)
    
    # Operator precedence
    result = 2 + 3 * 4
    print(result)
    
    result = (2 + 3) * 4
    print(result)
    
    # Augmented assignments
    x: int = 10
    x += 5
    print(x)
    
    x -= 3
    print(x)
    
    x *= 2
    print(x)
    
    x //= 4
    print(x)
    
    # Comparison operators
    print(a > b)
    print(a < b)
    print(a == 15)
    print(a != b)
    print(a >= 15)
    print(a <= 15)
    
    # Chained comparisons
    y: int = 7
    print(5 < y < 10)
    
    # Mixed float and int
    mixed: float = 5.0 + 3
    print(mixed)
</expected>
19
11
60
3.75
3
3
225
10
14
20
15
12
24
6
True
False
True
True
True
True
True
8.0

```

## Timing

- Generation: 447.70s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
