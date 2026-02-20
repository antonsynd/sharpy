# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T01:51:45.189176
**Type:** compilation_failed
**Feature Focus:** nullable_types
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Nullable types with null coalescing and conditional operators
def main():
    score: int? = None()
    display: str = str(score ?? 0)
    print(display)

    name: str? = "Sharpy"
    length: int? = name?.len()
    result: str = str(length ?? -1)
    print(result)

    name2: str? = None()
    length2: int? = name2?.len()
    print(str(length2 ?? -1))

# EXPECTED OUTPUT:
# 0
# 6
# -1
```

## Error

```
Assembly compilation failed:

error[CS1061]: 'string' does not contain a definition for 'Len' and no accessible extension method 'Len' accepting a first argument of type 'string' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpjbaemhjw/dogfood_test.spy:8:62
    |
  8 |     length: int? = name?.len()
    |                               ^
    |

error[CS1061]: 'string' does not contain a definition for 'Len' and no accessible extension method 'Len' accepting a first argument of type 'string' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpjbaemhjw/dogfood_test.spy:13:65
    |
 13 |     length2: int? = name2?.len()
    |                                 ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpjbaemhjw/dogfood_test.cs

```

## Timing

- Generation: 83.12s
- Execution: 4.06s
