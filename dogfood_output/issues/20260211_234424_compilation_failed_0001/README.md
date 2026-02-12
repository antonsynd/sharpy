# Issue Report: compilation_failed

**Timestamp:** 2026-02-11T23:44:15.753049
**Type:** compilation_failed
**Feature Focus:** optional_unwrap
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test optional unwrap and unwrap_or methods
def main():
    x: int? = Some(42)
    y: int? = None()
    
    # Test unwrap on Some
    print(x.unwrap())
    
    # Test unwrap_or on None
    print(y.unwrap_or(99))
    
    # Test unwrap_or on Some (should return wrapped value)
    print(x.unwrap_or(0))

# EXPECTED OUTPUT:
# 42
# 99
# 42
```

## Error

```
Assembly compilation failed:

error[CS1061]: 'int?' does not contain a definition for 'Unwrap' and no accessible extension method 'Unwrap' accepting a first argument of type 'int?' could be found (are you missing a using directive or an assembly reference?)
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpo3t6nefp/dogfood_test.spy:7:41
    |
  7 |     print(x.unwrap())
    |                      ^
    |

error[CS1061]: 'int?' does not contain a definition for 'UnwrapOr' and no accessible extension method 'UnwrapOr' accepting a first argument of type 'int?' could be found (are you missing a using directive or an assembly reference?)
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpo3t6nefp/dogfood_test.spy:10:41
    |
 10 |     print(y.unwrap_or(99))
    |                           ^
    |

error[CS1061]: 'int?' does not contain a definition for 'UnwrapOr' and no accessible extension method 'UnwrapOr' accepting a first argument of type 'int?' could be found (are you missing a using directive or an assembly reference?)
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpo3t6nefp/dogfood_test.spy:13:41
    |
 13 |     print(x.unwrap_or(0))
    |                          ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpo3t6nefp/dogfood_test.cs

```

## Timing

- Generation: 4.62s
- Execution: 1.27s
