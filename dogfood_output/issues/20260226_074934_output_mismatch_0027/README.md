# Issue Report: output_mismatch

**Timestamp:** 2026-02-26T07:46:37.601250
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point for module_utils test
# Tests cross-module imports, inheritance, and utility functions

from math_core import calculate_sum, calculate_mean
from math_stats import Statistics, analyze_dataset

def main():
    # Create dataset using Statistics class from math_stats
    data: Statistics = Statistics([10, 25, 30, 45, 50])

    # Test direct utility functions from math_core
    total: int = calculate_sum([10, 25, 30, 45, 50])
    print(total)

    # Test Statistics class methods
    print(data.average())

    # Test inherited method
    print(data.count())

    # Test analysis report
    report: dict[str, float] = analyze_dataset(data)
    print(report["min"])
    print(report["max"])
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
160
30.0
5
10.0
50.0

```

### Actual
```
160
32.0
5
10.0
50.0
```

## Timing

- Generation: 140.97s
- Execution: 4.58s
