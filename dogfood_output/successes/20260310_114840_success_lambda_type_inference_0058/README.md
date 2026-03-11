# Successful Dogfood Run

**Timestamp:** 2026-03-10T11:43:04.219521
**Feature Focus:** lambda_type_inference
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test lambda type inference in complex generic and higher-order contexts
type IntTransform = (int) -> int
type IntPredicate = (int) -> bool

class GenericProcessor[T]:
    _items: list[T]

    def __init__(self):
        self._items = []

    def add(self, item: T) -> None:
        self._items.append(item)

    def process(self, transformer: (T) -> T) -> list[T]:
        result: list[T] = []
        for item in self._items:
            result.append(transformer(item))
        return result

    def filter_items(self, pred: (T) -> bool) -> list[T]:
        result: list[T] = []
        for item in self._items:
            if pred(item):
                result.append(item)
        return result

class Accumulator:
    _values: list[int]

    def __init__(self):
        self._values = []

    def add(self, x: int) -> None:
        self._values.append(x)

    property get sum_squared(self) -> int:
        # Lambda inferred from context in map
        squared = map(lambda n: n * n, self._values)
        total = 0
        for s in squared:
            total += s
        return total

    property get count_positive(self) -> int:
        # Lambda inferred in filter context
        positives = filter(lambda x: x > 0, self._values)
        count = 0
        for _ in positives:
            count += 1
        return count

def transform_and_filter(value: int, transform: IntTransform, predicate: IntPredicate) -> bool:
    transformed = transform(value)
    return predicate(transformed)

def main():
    # Test 1: Generic processor with inferred lambdas on ints
    int_proc = GenericProcessor[int]()
    int_proc.add(1)
    int_proc.add(2)
    int_proc.add(3)

    # Lambdas infer T=int from GenericProcessor[int]
    doubled = int_proc.process(lambda n: n * 2)
    print(doubled[0])
    print(doubled[1])
    print(doubled[2])

    # Lambda inferred in filter
    evens = int_proc.filter_items(lambda x: x % 2 == 0)
    print(len(evens))

    # Test 2: Lambda in higher-order function with explicit types
    result = transform_and_filter(5, lambda n: n * 2, lambda m: m > 5)
    print(result)

    # Test 3: Accumulator with lambda-inferred properties
    acc = Accumulator()
    acc.add(2)
    acc.add(-3)
    acc.add(4)
    print(acc.count_positive)
    print(acc.sum_squared)

    # Test 4: Lambda with explicit signature in variable
    predicate: IntPredicate = lambda x: x > 10
    print(predicate(5))
    print(predicate(15))

    # Test 5: Lambda chaining
    values: list[int] = [1, 2, 3, 4, 5]
    transformed = map(lambda x: x + 1, filter(lambda y: y > 2, values))
    count = 0
    for _ in transformed:
        count += 1
    print(count)

```

## Output

```
2
4
6
1
True
2
29
False
True
3
```

## Timing

- Generation: 318.71s
- Execution: 5.43s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
