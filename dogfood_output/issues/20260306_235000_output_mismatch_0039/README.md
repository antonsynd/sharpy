# Issue Report: output_mismatch

**Timestamp:** 2026-03-06T23:47:27.980203
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports and tests polymorphic analysis pipeline
from analyzer import SumAnalyzer, AverageAnalyzer, MinMaxAnalyzer, run_analysis

def main():
    data: list[int] = [10, 20, 30, 40, 50]
    
    sum_analyzer = SumAnalyzer()
    avg_analyzer = AverageAnalyzer()
    range_analyzer = MinMaxAnalyzer()
    
    print(run_analysis(sum_analyzer, data))
    print(run_analysis(avg_analyzer, data))
    print(run_analysis(range_analyzer, data))
    
    # Verify polymorphic dispatch by using Analyzer type
    analyzers: list[Analyzer] = [sum_analyzer, avg_analyzer, range_analyzer]
    total: float = 0.0
    for a in analyzers:
        total += a.analyze([5, 5])
    print(total)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Result: 150.0
Result: 30.0
Result: 40.0
20.0

```

### Actual
```
Result: 150.0
Result: 30.0
Result: 40.0
15.0
```

## Timing

- Generation: 117.82s
- Execution: 4.73s
