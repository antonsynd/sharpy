# Issue Report: compilation_failed

**Timestamp:** 2026-02-11T23:43:08.521011
**Type:** compilation_failed
**Feature Focus:** raise_exception
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Simple exception raising with ValueError
def divide(a: int, b: int) -> int:
    if b == 0:
        raise ValueError("Cannot divide by zero")
    return a // b

def main():
    try:
        result = divide(10, 2)
        print(result)
    except ValueError as e:
        print("Error caught")
    
    try:
        result = divide(10, 0)
        print(result)
    except ValueError as e:
        print("Division by zero handled")

# EXPECTED OUTPUT:
# 5
# Division by zero handled
```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'result' does not exist in the current context
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmphesl7pga/dogfood_test.spy:15:13
    |
 15 |         result = divide(10, 0)
    |             ^
    |

error[CS0103]: The name 'result' does not exist in the current context
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmphesl7pga/dogfood_test.spy:16:43
    |
 16 |         print(result)
    |                      ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'e' is assigned but never used
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmphesl7pga/dogfood_test.spy:17:5
    |
 17 |     except ValueError as e:
    |     ^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Generated C#

```csharp
warning[SPY0451]: Local variable 'e' is assigned but never used
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmphesl7pga/dogfood_test.spy:17:5
    |
 17 |     except ValueError as e:
    |     ^^^^^^^^^^^^^^^^^^^^^^^
    |

Generated C# code written to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmphesl7pga/dogfood_test.cs

```

## Timing

- Generation: 4.71s
- Execution: 1.43s
