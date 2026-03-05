# Issue Report: output_mismatch

**Timestamp:** 2026-03-04T19:04:01.155450
**Type:** output_mismatch
**Feature Focus:** set_literal
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test set literals with class inheritance and complex filtering
enum FilterMode:
    EVEN = 0
    ODD = 1

type IntSet = set[int]

class DataFilter:
    @virtual
    def apply(self, values: set[int]) -> set[int]:
        return set[int]()

class SetFilter(DataFilter):
    threshold: int
    excluded: set[int]

    def __init__(self, threshold: int):
        self.threshold = threshold
        self.excluded = {13, 26, 39}

    @override
    def apply(self, values: set[int]) -> set[int]:
        result: set[int] = set[int]()
        for v in values:
            if v >= self.threshold and v not in self.excluded:
                result.add(v)
        return result

def collect_by_mode(items: list[int], mode: FilterMode) -> set[int]:
    result: set[int] = set[int]()
    i = 0
    while i < len(items):
        val = items[i]
        if mode == FilterMode.EVEN and val % 2 == 0:
            result.add(val)
        elif mode == FilterMode.ODD and val % 2 == 1:
            result.add(val)
        i += 1
    return result

def main():
    # Type alias for set type
    direct_literal: IntSet = {2, 4, 6, 8, 10, 12}

    # Create filter and process set literal
    filter_even = SetFilter(5)
    filtered = filter_even.apply(direct_literal)
    print(len(filtered))

    # Process list through collection function
    source: list[int] = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 13, 26]

    # Collect evens
    evens = collect_by_mode(source, FilterMode.EVEN)
    print(len(evens))

    # Collect odds
    odds = collect_by_mode(source, FilterMode.ODD)
    print(len(odds))

    # Combine sets with spread operator
    combined: set[int] = {*evens, 100, *odds}
    print(len(combined))

    # Membership tests
    print(100 in combined)
    print(13 in combined)
    print(50 in combined)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
2
5
4
10
True
False
False

```

### Actual
```
4
6
6
13
True
True
False
```

## Timing

- Generation: 590.97s
- Execution: 4.89s
