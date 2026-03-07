# Issue Report: output_mismatch

**Timestamp:** 2026-03-06T19:42:56.968606
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports

from text_utils import TextProcessor, truncate_text
from data_utils import DataAnalyzer, calc_sum, create_label

def main():
    # Test text_utils module
    sample: str = "hello world from sharpy"
    processor = TextProcessor(sample)
    print(processor.word_count())
    
    # Test data_utils using imported text_utils
    values: list[int] = [10, 20, 30, 40]
    analyzer = DataAnalyzer(sample, values)
    print(analyzer.summary())
    
    # Test standalone functions from both modules
    truncated: str = truncate_text("very long text here", 8)
    print(truncated)
    
    total: int = calc_sum(values)
    print(total)
    
    label: str = create_label("Result", total)
    print(label)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
3
Words: 5, Avg: 25.0
very lon...
100
Result Number: 100

```

### Actual
```
4
Words: 4, Avg: 25.0
very lon...
100
Result Number: 100
```

## Timing

- Generation: 71.58s
- Execution: 4.74s
