# Issue Report: output_mismatch

**Timestamp:** 2026-03-06T15:55:48.266888
**Type:** output_mismatch
**Feature Focus:** lambda_multiarg
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Multi-argument lambdas in complex contexts: type aliases, generics, and inheritance
# Tests that multi-arg lambdas work with higher-order functions in various scenarios

type BinaryOp = (int, int) -> int
type Predicate2 = (int, int) -> bool

class Calculator:
    _accumulator: int
    _history: list[str]
    
    def __init__(self, initial: int):
        self._accumulator = initial
        self._history = [f"init:{initial}"]
    
    def fold(self, items: list[int], op: BinaryOp) -> int:
        result = self._accumulator
        for item in items:
            result = op(result, item)
        return result
    
    def combine_with(self, other: Calculator, merger: (int, int) -> int) -> int:
        return merger(self._accumulator, other._accumulator)
    
    def accumulate(self, x: int, y: int, z: int, combiner: (int, int, int) -> int) -> None:
        self._accumulator = combiner(x, y, z)
        self._history.append(f"acc:{self._accumulator}")
    
    def get_value(self) -> int:
        return self._accumulator

@abstract
class DataProcessor:
    _transform: (int, int) -> int
    
    def __init__(self, transform: (int, int) -> int):
        self._transform = transform
    
    @abstract
    def process(self, a: int, b: int) -> int: ...

class MultiplyProcessor(DataProcessor):
    _multiplier: int
    
    def __init__(self, mult: int):
        # Chain to base constructor with lambda
        super().__init__(lambda x, y: (x * y) * mult)
        self._multiplier = mult
    
    @override
    def process(self, a: int, b: int) -> int:
        return self._transform(a, b)

class Validator:
    _checks: list[Predicate2]
    
    def __init__(self):
        self._checks = []
    
    def add_check(self, check: Predicate2) -> None:
        self._checks.append(check)
    
    def validate(self, x: int, y: int) -> bool:
        for c in self._checks:
            if not c(x, y):
                return False
        return True

def reduce_pairs(pairs: list[tuple[int, int]], reducer: (int, int, int, int) -> int) -> int:
    result = 0
    for p in pairs:
        a, b = p
        result = reducer(result, a, result, b)
    return result

def main():
    # Test 1: Calculator with binary operations via lambdas
    calc = Calculator(10)
    items: list[int] = [1, 2, 3, 4]
    
    # Lambda with two arguments in fold
    sum_result = calc.fold(items, lambda acc, val: acc + val)
    print(sum_result)
    
    # Lambda with two args - power operation
    prod_result = calc.fold([2, 3], lambda acc, val: acc ** val)
    print(prod_result)
    
    # Test 2: Three-arg lambda in accumulate
    calc.accumulate(2, 3, 4, lambda a, b, c: a * b + c)
    print(calc.get_value())
    
    # Test 3: Combine two calculators with lambda
    calc2 = Calculator(5)
    combined = calc.combine_with(calc2, lambda x, y: (x * 2) + (y * 3))
    print(combined)
    
    # Test 4: Inheritance with lambda in constructor
    processor = MultiplyProcessor(2)
    result = processor.process(3, 4)
    print(result)
    
    # Test 5: Validator with multiple predicate lambdas
    validator = Validator()
    validator.add_check(lambda x, y: x > 0 and y > 0)
    validator.add_check(lambda x, y: (x + y) < 100)
    
    print(validator.validate(10, 20))
    print(validator.validate(50, 60))
    
    # Test 6: Four-arg lambda with tuple unpacking
    pairs: list[tuple[int, int]] = [(1, 2), (3, 4)]
    reduced = reduce_pairs(pairs, lambda r1, a, r2, b: r1 + a + r2 + b)
    print(reduced)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
20
100
10
35
24
True
False
20

```

### Actual
```
20
1000000
10
35
24
True
False
13
```

## Timing

- Generation: 71.94s
- Execution: 4.72s
