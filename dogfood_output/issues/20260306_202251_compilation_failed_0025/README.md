# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T20:21:24.913220
**Type:** compilation_failed
**Feature Focus:** optional_unwrap
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: optional_unwrap with generics, inheritance, and type narrowing
# Demonstrates unwrap(), unwrap_or(), map() methods and None handling

# Type alias for optional integer
type MaybeInt = int?

@abstract
class Processor[T]:
    @abstract
    def transform(self, value: T) -> MaybeInt:
        ...
    
    def safe_transform(self, value: T) -> int:
        result: MaybeInt = self.transform(value)
        return result.unwrap_or(-999)

class LengthProcessor(Processor[str]):
    min_length: int
    
    def __init__(self, minimum: int):
        self.min_length = minimum
    
    @override
    def transform(self, value: str) -> MaybeInt:
        if len(value) >= self.min_length:
            return Some(len(value))
        return None()

class ThresholdProcessor(Processor[float]):
    threshold: float
    
    def __init__(self, thresh: float):
        self.threshold = thresh
    
    @override
    def transform(self, value: float) -> MaybeInt:
        if value >= self.threshold:
            return Some(int(value))
        return None()

class StatsCollector:
    data: list[MaybeInt]
    
    def __init__(self):
        self.data = []
    
    def add(self, value: MaybeInt) -> None:
        self.data.append(value)
    
    def sum_valid(self) -> int:
        total: int = 0
        for v in self.data:
            total += v.unwrap_or(0)
        return total

def main():
    str_proc: LengthProcessor = LengthProcessor(5)
    float_proc: ThresholdProcessor = ThresholdProcessor(10.0)
    stats: StatsCollector = StatsCollector()
    
    # Test unwrap_or with valid and invalid values
    r1: MaybeInt = str_proc.transform("hello world")
    r2: MaybeInt = str_proc.transform("hi")
    print(r1.unwrap_or(0))
    print(r2.unwrap_or(0))
    
    # Test unwrap after narrowing check
    r3: MaybeInt = float_proc.transform(15.5)
    r4: MaybeInt = float_proc.transform(3.5)
    if r3 is not None:
        print(r3.unwrap())
    
    # Collect values and calculate sum
    stats.add(r3)
    stats.add(r4)
    stats.add(Some(100))
    print(stats.sum_valid())
    
    # Test map chaining
    doubled: MaybeInt = r1.map(lambda x: x * 2)
    print(doubled.unwrap_or(-1))
    
    # Final verification with type narrowing
    last: MaybeInt = Some(7)
    if last is not None:
        print(last + 3)

```

## Error

```
Assembly compilation failed:

error[CS1929]: 'int' does not contain a definition for 'Unwrap' and the best extension method overload 'TaskExtensions.Unwrap(Task<Task>)' requires a receiver of type 'System.Threading.Tasks.Task<System.Threading.Tasks.Task>'
  --> /tmp/tmp7boedkwp/dogfood_test.spy:71:43
    |
 71 |         print(r3.unwrap())
    |                           ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp7boedkwp/dogfood_test.cs

```

## Timing

- Generation: 73.49s
- Execution: 4.58s
