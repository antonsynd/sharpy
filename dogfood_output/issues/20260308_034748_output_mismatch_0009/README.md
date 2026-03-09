# Issue Report: output_mismatch

**Timestamp:** 2026-03-08T03:46:39.883651
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from both utility modules

from string_utils import StringFormatter, truncate
from math_utils import Calculator, clamp, average

def main():
    # Test string utilities
    formatter = StringFormatter("INFO")
    formatted: str = formatter.format("test message")
    print(formatted)
    
    truncated: str = truncate("hello world", 5)
    print(truncated)
    
    # Test math utilities
    calc = Calculator(3)
    scaled: int = calc.scale(10)
    print(scaled)
    
    clamped: int = clamp(150, 0, 100)
    print(clamped)
    
    # Test cross-module usage
    values: list[int] = [10, 20, 30]
    avg: float = average(values)
    result: str = formatter.format(f"average is {avg}")
    print(result)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
INFO: test message
hello...
30
100
INFO: AVERAGE IS 20.0

```

### Actual
```
INFO: test message
hello...
30
100
INFO: average is 20.0
```

## Timing

- Generation: 23.63s
- Execution: 5.35s
