# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T07:16:40.227462
**Type:** compilation_failed
**Feature Focus:** lambda_type_inference
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Combines: generics, optional types, higher-order functions, and method chaining

type IntTransform = Transform[int]

class Transform[T]:
    _fn: (T) -> int

    def __init__(self, func: (T) -> int):
        self._fn = func

    def apply(self, value: T) -> int:
        return self._fn(value)

    def compose(self, other: Transform[T]) -> Transform[T]:
        return Transform(lambda x: self._fn(x) + other._fn(x))

def double_if_present(val: int?) -> int:
    return val.map(lambda n: n * 2).unwrap_or(0)

def scale(factor: int) -> (int) -> int:
    return lambda x: x * factor

def main():
    t1: IntTransform = Transform(lambda x: x + 10)
    t2: IntTransform = Transform(lambda x: x * 3)

    composed: IntTransform = t1.compose(t2)

    print(t1.apply(5))
    print(t2.apply(5))
    print(composed.apply(5))

    maybe_val: int? = Some(7)
    print(double_if_present(maybe_val))

    tripler: (int) -> int = scale(3)
    print(tripler(4))

    base_values: list[int] = [1, 2, 3]
    results: list[int] = [t1.apply(v) for v in base_values]
    print(results[2])
# EXPECTED OUTPUT:
# 15
# 15
# 40
# 14
# 12
# 13
```

## Error

```
Assembly compilation failed:

error[CS1503]: Argument 1: cannot convert from 'System.Collections.Generic.IEnumerable<int>' to 'System.Collections.Generic.IEnumerable<object>'
  --> /tmp/tmpxmgtzgjh/dogfood_test.spy:40:60
    |
 40 |     results: list[int] = [t1.apply(v) for v in base_values]
    |                                                            ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpxmgtzgjh/dogfood_test.cs

```

## Timing

- Generation: 130.45s
- Execution: 4.35s
