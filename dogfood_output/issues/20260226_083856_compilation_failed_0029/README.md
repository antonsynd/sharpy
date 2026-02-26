# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T08:33:50.199254
**Type:** compilation_failed
**Feature Focus:** dunder_reversed
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class HistoryStack:
    values: list[int]
    
    def __init__(self):
        self.values = []
    
    def record(self, value: int) -> None:
        self.values.append(value)
    
    def __reversed__(self) -> int:
        i = len(self.values) - 1
        while i >= 0:
            if self.values[i] > 0:
                yield self.values[i]
            i -= 1

def main():
    hs = HistoryStack()
    hs.record(10)
    hs.record(-5)
    hs.record(20)
    hs.record(0)
    hs.record(30)
    
    total = 0
    count = 0
    for val in reversed(hs):
        total = total + val
        count = count + 1
        print(val)
    
    print(count)
    print(total)
```

## Error

```
Assembly compilation failed:

error[CS1503]: Argument 1: cannot convert from 'DogfoodTest.HistoryStack' to 'System.Collections.Generic.IEnumerable<int>'
  --> /tmp/tmpft9lvsh1/dogfood_test.spy:27:75
    |
 27 |     for val in reversed(hs):
    |                             ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpft9lvsh1/dogfood_test.cs

```

## Timing

- Generation: 293.81s
- Execution: 4.34s
