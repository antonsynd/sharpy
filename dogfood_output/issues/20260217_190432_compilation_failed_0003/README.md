# Issue Report: compilation_failed

**Timestamp:** 2026-02-17T18:58:22.017838
**Type:** compilation_failed
**Feature Focus:** enum_definition
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Enum values in arithmetic expressions
# Tests: enum definition, enum arithmetic operations

enum Priority:
    LOW = 5
    MEDIUM = 10
    HIGH = 15

def main():
    # Enum values in arithmetic
    total: int = Priority.LOW + Priority.HIGH
    average: int = (Priority.LOW + Priority.MEDIUM + Priority.HIGH) // 3
    
    print(total)
    print(average)
    print(Priority.MEDIUM * 2)
    
# EXPECTED OUTPUT:
# 20
# 10
# 20
```

## Error

```
Assembly compilation failed:

error[CS0019]: Operator '+' cannot be applied to operands of type 'DogfoodTest.Priority' and 'DogfoodTest.Priority'
  --> /tmp/tmp39e0zvof/dogfood_test.spy:11:21
    |
 11 |     total: int = Priority.LOW + Priority.HIGH
    |                     ^
    |

error[CS0019]: Operator '+' cannot be applied to operands of type 'DogfoodTest.Priority' and 'DogfoodTest.Priority'
  --> /tmp/tmp39e0zvof/dogfood_test.spy:12:65
    |
 12 |     average: int = (Priority.LOW + Priority.MEDIUM + Priority.HIGH) // 3
    |                                                                 ^
    |

error[CS0019]: Operator '*' cannot be applied to operands of type 'DogfoodTest.Priority' and 'int'
  --> /tmp/tmp39e0zvof/dogfood_test.spy:16:39
    |
 16 |     print(Priority.MEDIUM * 2)
    |                               ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp39e0zvof/dogfood_test.cs

```

## Timing

- Generation: 353.57s
- Execution: 4.09s
