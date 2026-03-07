# Issue Report: output_mismatch

**Timestamp:** 2026-03-07T01:52:55.287498
**Type:** output_mismatch
**Feature Focus:** bool_variables
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex bool variables test combining type aliases, nullable narrowing, generics, and inheritance
type Predicate = (int) -> bool
type MaybeBool = bool?

enum LogicState:
    TRUE = 1
    FALSE = 0
    UNKNOWN = -1

class BooleanAnalyzer:
    _value: bool

    def __init__(self, initial: bool):
        self._value = initial

    property get value(self) -> bool:
        return self._value

    @virtual
    def evaluate(self, x: int) -> bool:
        return self._value and x > 0

class AdvancedAnalyzer(BooleanAnalyzer):
    threshold: int

    def __init__(self, initial: bool, threshold: int):
        super().__init__(initial)
        self.threshold = threshold

    @override
    def evaluate(self, x: int) -> bool:
        return self.value and x >= self.threshold

class BoolContainer[T]:
    data: T
    _is_active: bool

    def __init__(self, data: T, active: bool):
        self.data = data
        self._is_active = active

    property get is_active(self) -> bool:
        return self._is_active

    def toggle(self) -> None:
        self._is_active = not self._is_active

def check_maybe(p: MaybeBool) -> str:
    if p is not None:
        if p:
            return "true"
        else:
            return "false"
    return "unknown"

def logic_from_enum(state: LogicState) -> MaybeBool:
    if state == LogicState.TRUE:
        return Some(True)
    elif state == LogicState.FALSE:
        return Some(False)
    return None()

def apply_predicate(pred: Predicate, val: int) -> bool:
    return pred(val)

def main():
    # Basic bool variables with type alias
    flag1: bool = True
    flag2: Predicate = lambda x: x % 2 == 0

    # Nullable bool variables with narrowing
    maybe_flag: MaybeBool = None()
    print(check_maybe(maybe_flag))
    maybe_flag = Some(True)
    print(check_maybe(maybe_flag))

    # Bool in generic container
    container: BoolContainer[int] = BoolContainer(42, False)
    print(container.is_active)
    container.toggle()
    print(container.is_active)

    # Bool properties in inheritance hierarchy
    analyzer: BooleanAnalyzer = AdvancedAnalyzer(True, 10)
    print(analyzer.evaluate(5))
    print(analyzer.evaluate(15))

    # Bool from enum via if-elif (replaced match expression)
    result = logic_from_enum(LogicState.TRUE)
    if result is not None:
        print(result)
    else:
        print(False)

    # Complex boolean expressions
    a: bool = True
    b: bool = False
    c: bool = True
    result: bool = (a or b) and (not b or c) and a
    print(result)

    # Boolean in comprehension (replaced with loop + append)
    nums: list[int] = [1, 2, 3, 4, 5]
    evens: list[bool] = []
    for n in nums:
        evens.append(n % 2 == 0)
    for is_even in evens:
        print(is_even)

    # Lambda returning bool
    is_positive: Predicate = lambda x: x > 0
    print(apply_predicate(is_positive, 5))
    print(apply_predicate(is_positive, -3))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
unknown
true
False
True
False
True
True
True
False
True
False
True
False
True
False
True
False
True
False

```

### Actual
```
unknown
true
False
True
False
True
True
True
False
True
False
True
False
True
False
```

## Timing

- Generation: 180.77s
- Execution: 5.09s
