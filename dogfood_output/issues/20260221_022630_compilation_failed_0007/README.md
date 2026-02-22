# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T02:25:55.970534
**Type:** compilation_failed
**Feature Focus:** dotnet_import
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Basic .NET import and usage
from system import Math

def main():
    # Use Math.PI constant
    print(Math.PI)
    
    # Use Math.Abs method
    print(Math.Abs(-42))
    
    # Basic arithmetic with Math values
    r: float = 5.0
    area: float = Math.PI * r * r
    print(area)
    
    # Compare values
    print(Math.Max(10, 25))
    print(Math.Min(-5, 3))
    
    # Round value
    print(Math.Round(3.7))

# EXPECTED OUTPUT:
# 3.141592653589793
# 42
# 78.53981633974483
# 25
# -5
# 4
```

## Error

```
Assembly compilation failed:

error[CS0117]: 'Math' does not contain a definition for 'Pi'
  --> /tmp/tmpfv5auhtm/dogfood_test.spy:6:44
    |
  6 |     print(Math.PI)
    |                   ^
    |

error[CS0117]: 'Math' does not contain a definition for 'Pi'
  --> /tmp/tmpfv5auhtm/dogfood_test.spy:13:28
    |
 13 |     area: float = Math.PI * r * r
    |                            ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpfv5auhtm/dogfood_test.cs

```

## Timing

- Generation: 19.55s
- Execution: 4.75s
