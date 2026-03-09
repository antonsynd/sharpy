# Successful Dogfood Run

**Timestamp:** 2026-03-08T05:05:40.843809
**Feature Focus:** lambda_type_inference
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex lambda type inference test
# Tests inference in higher-order functions, generics, nested contexts, and chained operations
# Simplified version avoiding parser edge cases

type IntPredicate = (int) -> bool
type IntTransformer = (int) -> int

def apply_twice(value: int, fn: IntTransformer) -> int:
    return fn(fn(value))

def apply_if_positive(value: int, fn: IntTransformer) -> int:
    if value > 0:
        return fn(value)
    return value

def filter_list(items: list[int], pred: IntPredicate) -> list[int]:
    result: list[int] = []
    for item in items:
        if pred(item):
            result.append(item)
    return result

def transform_list(items: list[int], fn: IntTransformer) -> list[int]:
    result: list[int] = []
    for item in items:
        result.append(fn(item))
    return result

def sum_of_evens(items: list[int]) -> int:
    total: int = 0
    for item in items:
        if item % 2 == 0:
            total = total + item
    return total

def count_matches(items: list[int], pred: IntPredicate) -> int:
    count: int = 0
    for item in items:
        if pred(item):
            count = count + 1
    return count

class Processor:
    multiplier: int
    
    def __init__(self, multiplier: int):
        self.multiplier = multiplier
    
    def process(self, items: list[int]) -> list[int]:
        result: list[int] = []
        for item in items:
            result.append(item * self.multiplier)
        return result

def main():
    numbers: list[int] = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
    
    # Lambda inference in filter - simple lambda
    evens = filter_list(numbers, lambda n: n % 2 == 0)
    print(len(evens))
    
    # Lambda inference in transform - arithmetic lambda
    doubled = transform_list(numbers, lambda x: x * 2)
    print(doubled[0])
    print(doubled[5])
    
    # Lambda inference with apply_twice - nested application
    add_ten = lambda x: x + 10
    result = apply_twice(5, add_ten)
    print(result)
    
    # Lambda inference with apply_if_positive - conditional application
    negative_result = apply_if_positive(-5, lambda x: x * 2)
    positive_result = apply_if_positive(5, lambda x: x * 3)
    print(negative_result)
    print(positive_result)
    
    # Count matches with lambda
    count_greater = count_matches(numbers, lambda x: x > 5)
    print(count_greater)
    
    # Count matches with different lambda
    count_small = count_matches(numbers, lambda n: n < 4)
    print(count_small)
    
    # Processor with class-based transformation
    proc = Processor(3)
    processed = proc.process([1, 2, 3, 4])
    print(processed[0])
    print(processed[3])
    
    # Sum of evens test
    total = sum_of_evens([1, 2, 3, 4, 5])
    print(total)
    
    # Complex lambda in filter with closure-like behavior
    threshold: int = 7
    filtered = filter_list(numbers, lambda n: n > threshold)
    print(len(filtered))

```

## Output

```
5
2
12
25
-5
15
5
3
3
12
6
3
```

## Timing

- Generation: 267.45s
- Execution: 5.38s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
