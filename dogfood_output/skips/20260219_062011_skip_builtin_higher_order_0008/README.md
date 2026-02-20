# Skipped Dogfood Run

**Timestamp:** 2026-02-19T06:11:58.382272
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0107]: Invalid type annotation target
  --> /tmp/tmpwitue_7l/dogfood_test.spy:20:26
    |
 20 |         self.stored_value: T = val
    |                          ^
    |


**Feature Focus:** builtin_higher_order
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
type TransformFn = (int) -> int

class DataProcessor:
    @virtual
    def process(self, value: int) -> int:
        return value

class DoublingProcessor(DataProcessor):
    @override
    def process(self, value: int) -> int:
        return value * 2

class SquaringProcessor(DataProcessor):
    @override
    def process(self, value: int) -> int:
        return value * value

class ProcessorBox[T]:
    def __init__(self, val: T):
        self.stored_value: T = val

    def get_value(self) -> T:
        return self.stored_value

def make_filter_fn(threshold: int) -> (int) -> bool:
    return lambda x: x > threshold

def increment(x: int) -> int:
    return x + 1

def process_with_processor(proc: DataProcessor, val: int) -> int:
    return proc.process(val)

def is_even_func(n: int) -> bool:
    return n % 2 == 0

def main():
    numbers: list[int] = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]

    print("=== Filter with lambda ===")
    is_even: (int) -> bool = lambda n: n % 2 == 0
    evens = filter(is_even, numbers)
    for n in evens:
        print(n)

    print("=== Map with lambda ===")
    doubled = map(lambda x: x * 2, [1, 2, 3, 4, 5])
    for n in doubled:
        print(n)

    print("=== Map with named function ===")
    incremented = map(increment, [10, 20, 30])
    for n in incremented:
        print(n)

    print("=== Filter with closure ===")
    filter_fn: (int) -> bool = make_filter_fn(5)
    big_numbers = filter(filter_fn, numbers)
    for n in big_numbers:
        print(n)

    print("=== Polymorphic processors ===")
    processors: list[DataProcessor] = [DoublingProcessor(), SquaringProcessor()]
    for p in processors:
        result: int = p.process(5)
        print(result)

    print("=== Map over polymorphic results ===")
    results = map(lambda p: process_with_processor(p, 3), processors)
    for r in results:
        print(r)

    print("=== Generic box with higher-order ===")
    boxes: list[ProcessorBox[int]] = [ProcessorBox(1), ProcessorBox(2), ProcessorBox(3)]
    box_values = map(lambda b: b.get_value(), boxes)
    for v in box_values:
        print(v)

    print("=== Chained map/filter ===")
    chained = filter(lambda x: x > 5, map(lambda x: x * 3, [1, 2, 3, 4]))
    for n in chained:
        print(n)

    print("=== Complete ===")

# EXPECTED OUTPUT:
# === Filter with lambda ===
# 2
# 4
# 6
# 8
# 10
# === Map with lambda ===
# 2
# 4
# 6
# 8
# 10
# === Map with named function ===
# 11
# 21
# 31
# === Filter with closure ===
# 6
# 7
# 8
# 9
# 10
# === Polymorphic processors ===
# 10
# 25
# === Map over polymorphic results ===
# 6
# 9
# === Generic box with higher-order ===
# 1
# 2
# 3
# === Chained map/filter ===
# 9
# 12
# === Complete ===
```

## Timing

- Generation: 477.80s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
