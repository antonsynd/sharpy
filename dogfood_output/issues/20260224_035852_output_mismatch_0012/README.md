# Issue Report: output_mismatch

**Timestamp:** 2026-02-24T03:51:25.783812
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from data_utils and format_utils
from data_utils import filter_positive, DataStats, Doubler, Tripler
from data_utils import apply_transform, Incrementor
from format_utils import Formatter, create_summary, label_value

def main():
    # Test data processing utilities
    raw_data: list[int] = [5, -3, 8, -1, 10, 0, -7, 15]
    positive: list[int] = filter_positive(raw_data)
    stats: DataStats = DataStats(positive)
    print(len(positive))
    
    # Test class inheritance from module
    d: Doubler = Doubler()
    t: Tripler = Tripler()
    print(d.transform(5))
    print(t.transform(5))
    
    # Test base class usage (polymorphism)
    result: int = apply_transform(t, 42)
    print(result)
    
    # Test Incrementor class
    inc: Incrementor = Incrementor(10)
    inc_result: int = inc.process(42)
    print(inc_result)
    
    # Test formatter module
    formatter: Formatter = Formatter(">> ")
    labeled: str = label_value("Result", inc_result)
    print(labeled)
    
    # Test summary creation (cross-module type usage)
    summary: str = create_summary(stats)
    print(summary)

# EXPECTED OUTPUT:
# 4
# 10
# 15
# 57
# 52
# Result=52
# Sum: 38, Avg: 9.5
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
4
10
15
57
52
Result=52
Sum: 38, Avg: 9.5

```

### Actual
```
4
10
15
126
52
Result=52
Sum: 38, Avg: 9.5
```

## Timing

- Generation: 309.16s
- Execution: 4.70s
