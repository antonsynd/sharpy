# Skipped Dogfood Run

**Timestamp:** 2026-02-26T06:32:25.470882
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0200]: Undefined identifier 'multiply'
  --> /tmp/tmpeao89qeu/dogfood_test.spy:56:12
    |
 56 |     return multiply
    |            ^^^^^^^^
    |


**Feature Focus:** simple_function
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex higher-order function composition with generics and pipelines
# Uses direct function type syntax instead of type aliases

class Pipeline[T]:
    _value: T

    def __init__(self, value: T):
        self._value = value

    def map(self, fn: (T) -> T) -> Pipeline[T]:
        return Pipeline(fn(self._value))

    def get(self) -> T:
        return self._value

class NumericOps:
    @static
    def double(n: int) -> int:
        return n * 2

    @static
    def increment(n: int) -> int:
        return n + 1

    @static
    def is_even(n: int) -> bool:
        return n % 2 == 0

class FilterMap[T]:
    _filter_fn: (T) -> bool
    _map_fn: (T) -> T

    def __init__(self, filter_fn: (T) -> bool, map_fn: (T) -> T):
        self._filter_fn = filter_fn
        self._map_fn = map_fn

    def apply(self, item: T) -> T?:
        if self._filter_fn(item):
            return Some(self._map_fn(item))
        return None()

def compose_func(f: (int) -> int, g: (int) -> int, x: int) -> int:
    return g(f(x))

def apply_if(fn: (int) -> int, pred: (int) -> bool, x: int) -> int:
    if pred(x):
        return fn(x)
    return x

def apply_twice(fn: (int) -> int, x: int) -> int:
    return fn(fn(x))

def make_multiplier(factor: int) -> (int) -> int:
    def multiply(n: int) -> int:
        return n * factor
    return multiply

def main():
    # Test function composition
    double_then_increment: (int) -> int = make_multiplier(2)

    # Actually use composed function - double then increment 5: (5*2)+1=11
    result: int = compose_func(NumericOps.double, NumericOps.increment, 5)
    print(result)

    # Test apply_if with predicate - 4 is even, so double it: 8
    result1: int = apply_if(NumericOps.double, NumericOps.is_even, 4)
    print(result1)

    # Test apply_if with predicate - 3 is odd, so return as-is: 3
    result2: int = apply_if(NumericOps.double, NumericOps.is_even, 3)
    print(result2)

    # Test apply_twice with lambda - (10+3)+3=16
    print(apply_twice(lambda n: n + 3, 10))

    # Test closure-like pattern
    times_three: (int) -> int = make_multiplier(3)
    times_five: (int) -> int = make_multiplier(5)
    print(times_three(7))
    print(times_five(2))

    # Test Pipeline with method references
    pipe: Pipeline[int] = Pipeline(2)
    piped_result: int = pipe.map(NumericOps.double).map(NumericOps.increment).get()
    print(piped_result)

    # Test FilterMap with function types
    fm: FilterMap[int] = FilterMap(NumericOps.is_even, NumericOps.double)
    opt1: int? = fm.apply(6)
    opt2: int? = fm.apply(7)
    print(opt1 ?? -1)
    print(opt2 ?? -1)
    print(double_then_increment(5))
```

## Timing

- Generation: 235.50s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
