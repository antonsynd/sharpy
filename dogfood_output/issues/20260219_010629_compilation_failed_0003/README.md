# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T01:05:50.725099
**Type:** compilation_failed
**Feature Focus:** logical_operators
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test logical operators: and, or, not

def main():
    a = 1
    b = 0
    c = 0.0  # falsy
    d = -5   # truthy

    # Basic logical operators
    print(a and b)    # EXPECTED: 0
    print(b or d)     # EXPECTED: -5
    print(not c)      # EXPECTED: True

    # Combined logical operators with short-circuit
    result = a and b or d
    print(result)     # EXPECTED: -5

    # Truthiness check
    if not d:
        print("negative is falsy")
    else:
        print("negative is truthy")  # EXPECTED: negative is truthy
```

## Error

```
Assembly compilation failed:

error[CS0019]: Operator '&&' cannot be applied to operands of type 'int' and 'int'
  --> /tmp/tmpp6sartta/dogfood_test.spy:10:39
    |
 10 |     print(a and b)    # EXPECTED: 0
    |                                    ^
    |

error[CS0019]: Operator '||' cannot be applied to operands of type 'int' and 'int'
  --> /tmp/tmpp6sartta/dogfood_test.spy:11:39
    |
 11 |     print(b or d)     # EXPECTED: -5
    |                                     ^
    |

error[CS0023]: Operator '!' cannot be applied to operand of type 'double'
  --> /tmp/tmpp6sartta/dogfood_test.spy:12:39
    |
 12 |     print(not c)      # EXPECTED: True
    |                                       ^
    |

error[CS0019]: Operator '&&' cannot be applied to operands of type 'int' and 'int'
  --> /tmp/tmpp6sartta/dogfood_test.spy:15:22
    |
 15 |     result = a and b or d
    |                      ^
    |

error[CS0023]: Operator '!' cannot be applied to operand of type 'int'
  --> /tmp/tmpp6sartta/dogfood_test.spy:19:13
    |
 19 |     if not d:
    |             ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpp6sartta/dogfood_test.cs

```

## Timing

- Generation: 26.62s
- Execution: 4.04s
