# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T06:43:13.540354
**Type:** compilation_failed
**Feature Focus:** inheritance_with_override
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
class Counter:
    _val: int
    def __init__(self, v: int):
        self._val = v
    
    @virtual
    def get(self) -> int:
        return self._val

class DoublingCounter(Counter):
    @override
    def get(self) -> int:
        return self._val * 2

def main():
    c = Counter(7)
    d = DoublingCounter(7)
    print(c.get())
    print(d.get())
    # EXPECTED OUTPUT:
    # 7
    # 14
```

## Error

```
Assembly compilation failed:

error[CS1729]: 'DogfoodTest.DoublingCounter' does not contain a constructor that takes 1 arguments
  --> /tmp/tmpwp0fo3l8/dogfood_test.spy:17:21
    |
 17 |     d = DoublingCounter(7)
    |                     ^
    |

error[CS7036]: There is no argument given that corresponds to the required parameter 'v' of 'DogfoodTest.Counter.Counter(int)'
  --> /tmp/tmpwp0fo3l8/dogfood_test.spy:8:18
    |
  8 |         return self._val
    |                  ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpwp0fo3l8/dogfood_test.cs

```

## Timing

- Generation: 92.91s
- Execution: 4.05s
