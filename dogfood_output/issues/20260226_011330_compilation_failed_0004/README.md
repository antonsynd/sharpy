# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T01:07:18.299488
**Type:** compilation_failed
**Feature Focus:** augmented_assignment
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex augmented assignment with inheritance, generics, and control flow
# Tests: +=, -=, *=, //=, %=, **= operators across class hierarchy and loops

type IntValue = int

interface Adjustable:
    @abstract
    def adjust(self, delta: IntValue) -> IntValue

class Counter:
    value: IntValue
    factor: float
    
    def __init__(self, initial: IntValue, factor: float):
        self.value = initial
        self.factor = factor
    
    @virtual
    def increment(self, amount: IntValue) -> IntValue:
        scaled = int(float(amount) * self.factor)
        self.value += scaled
        return self.value
    
    def get_value(self) -> IntValue:
        return self.value

class AdvancedCounter(Counter, Adjustable):
    modifier: IntValue?
    operation_count: int
    
    def __init__(self, initial: IntValue, factor: float, mod: IntValue?):
        super().__init__(initial, factor)
        self.modifier = mod
        self.operation_count = 0
    
    @override
    def increment(self, amount: IntValue) -> IntValue:
        self.operation_count += 1
        self.operation_count *= 2
        
        result = super().increment(amount)
        
        if self.modifier is not None:
            result += self.modifier.unwrap()
            self.value %= 100
            self.value //= 3
        
        return result
    
    def adjust(self, delta: IntValue) -> IntValue:
        temp: IntValue = delta
        temp -= 5
        temp //= 2
        self.value += temp
        return self.value

def main():
    # Test basic Counter with +=
    basic = Counter(10, 1.5)
    print(basic.increment(4))
    
    # Test AdvancedCounter with modifier and aug-ops
    adv = AdvancedCounter(10, 2.0, Some(5))
    print(adv.increment(5))
    
    print(adv.get_value())
    print(adv.operation_count)
    
    # Test adjust with -= and //=
    print(adv.adjust(15))
    
    # Complex loop with **= and *=
    acc: IntValue = 0
    mult: IntValue = 1
    base: IntValue = 2
    
    for i in range(1, 5):
        base **= 2
        acc += base // i
        mult *= i
    
    print(acc)
    print(mult)
    print(adv.get_value())
```

## Error

```
Assembly compilation failed:

error[CS0266]: Cannot implicitly convert type 'double' to 'int'. An explicit conversion exists (are you missing a cast?)
  --> /tmp/tmpcchvrbjb/dogfood_test.spy:78:21
    |
 78 |         base **= 2
    |                   ^
    |

error[CS1929]: 'int' does not contain a definition for 'Unwrap' and the best extension method overload 'TaskExtensions.Unwrap(Task<Task>)' requires a receiver of type 'System.Threading.Tasks.Task<System.Threading.Tasks.Task>'
  --> /tmp/tmpcchvrbjb/dogfood_test.spy:44:35
    |
 44 |             result += self.modifier.unwrap()
    |                                   ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpcchvrbjb/dogfood_test.cs

```

## Timing

- Generation: 358.72s
- Execution: 4.39s
