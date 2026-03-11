# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T19:14:25.921759
**Type:** compilation_failed
**Feature Focus:** loop_in_function
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex loop patterns in function hierarchies with virtual dispatch
# Tests nested loops, loop control, inheritance with overridden loop behavior

enum ProcessingMode:
    SUM = 1
    PRODUCT = 2
    COUNT_EVEN = 3

@abstract
class DataProcessor:
    @abstract
    def process(self, data: list[int]) -> int: ...

    @virtual
    def analyze(self, data: list[int]) -> str:
        result: int = 0
        for x in data:
            if x > 0:
                result += 1
        return f"positive_count={result}"

class SumProcessor(DataProcessor):
    @override
    def process(self, data: list[int]) -> int:
        total: int = 0
        i: int = 0
        while i < len(data):
            if data[i] < 0:
                i += 1
                continue
            total += data[i]
            i += 1
        return total

class ProductProcessor(DataProcessor):
    @override
    def process(self, data: list[int]) -> int:
        result: int = 1
        found_nonzero: bool = False
        for x in data:
            if x == 0:
                return 0
            if x != 0:
                found_nonzero = True
                result *= x
        return result if found_nonzero else 0

class SmartProcessor(DataProcessor):
    mode: ProcessingMode

    def __init__(self, mode: ProcessingMode):
        self.mode = mode

    @override
    def process(self, data: list[int]) -> int:
        if self.mode == ProcessingMode.SUM:
            p = SumProcessor()
            return p.process(data)
        elif self.mode == ProcessingMode.PRODUCT:
            p = ProductProcessor()
            return p.process(data)
        else:
            count: int = 0
            for x in data:
                if x % 2 == 0 and x != 0:
                    count += 1
            return count

def find_first_match(processors: list[DataProcessor], data: list[int], threshold: int) -> int:
    for i in range(len(processors)):
        result: int = processors[i].process(data)
        if result >= threshold:
            return i
    return -1

def nested_loop_analysis(matrix: list[list[int]]) -> tuple[int, int]:
    total: int = 0
    max_val: int = 0
    for row in matrix:
        row_sum: int = 0
        for val in row:
            row_sum += val
            if val > max_val:
                max_val = val
        total += row_sum
    return (total, max_val)

def main():
    values: list[int] = [3, -1, 5, 0, 7, -2, 4]
    matrix: list[list[int]] = [[1, 2, 3], [4, 5, 6], [7, 8, 9]]

    sum_proc = SumProcessor()
    prod_proc = ProductProcessor()
    smart_sum = SmartProcessor(ProcessingMode.SUM)
    smart_count = SmartProcessor(ProcessingMode.COUNT_EVEN)

    print(sum_proc.process(values))
    print(prod_proc.process(values))
    print(smart_sum.process(values))
    print(smart_count.process(values))

    processors: list[DataProcessor] = [sum_proc, prod_proc, smart_sum]
    idx: int = find_first_match(processors, values, 15)
    print(idx)

    analysis = nested_loop_analysis(matrix)
    print(analysis[0])
    print(analysis[1])

    print(smart_count.analyze(values))

```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'p' does not exist in the current context
  --> /tmp/tmp840d13wv/dogfood_test.spy:60:17
    |
 60 |             p = ProductProcessor()
    |                 ^
    |

error[CS0103]: The name 'p' does not exist in the current context
  --> /tmp/tmp840d13wv/dogfood_test.spy:61:24
    |
 61 |             return p.process(data)
    |                        ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp840d13wv/dogfood_test.cs

```

## Timing

- Generation: 63.20s
- Execution: 5.44s
