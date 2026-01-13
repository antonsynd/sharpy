# Issue Report: compilation_failed

**Timestamp:** 2026-01-13T16:26:59.219894
**Type:** compilation_failed
**Feature Focus:** logical_operators
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Logical operators (and, or, not) with various conditions

def check_range(value: int, low: int, high: int) -> bool:
    # Test 'and' operator - value must be >= low AND <= high
    result: bool = value >= low and value <= high
    return result

def check_either(a: bool, b: bool) -> bool:
    # Test 'or' operator
    result: bool = a or b
    return result

def main():
    x: int = 15
    
    # Test 'and' operator
    in_range: bool = check_range(x, 10, 20)
    print(in_range)
    
    # Test 'or' operator
    has_true: bool = check_either(False, True)
    print(has_true)
    
    # Test 'not' operator
    flag: bool = False
    inverted: bool = not flag
    print(inverted)
    
    # Test combined logical operators
    a: bool = True
    b: bool = False
    c: bool = True
    
    # (True and False) or True = False or True = True
    combined: bool = a and b or c
    print(combined)
    
    # not (True or False) = not True = False
    negated: bool = not (a or b)
    print(negated)

main()

# EXPECTED OUTPUT:
# True
# True
# True
# True
# False
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(6,81): error CS0116: A namespace cannot directly contain members such as fields, methods or statements
  dogfood_test.cs(6,23): error CS1514: { expected
  dogfood_test.cs(6,93): error CS1022: Type or namespace definition, or end-of-file expected

```

## Compiler Output

```
Warning: 1 module-level statement(s) ignored because a 'main' function is defined

```

## Timing

- Generation: 8.00s
- Execution: 1.18s
