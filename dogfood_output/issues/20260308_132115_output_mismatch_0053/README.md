# Issue Report: output_mismatch

**Timestamp:** 2026-03-08T13:18:35.522011
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates multi-module imports
from string_utils import TextProcessor, sanitize, truncate
from math_utils import square, average, clamp, factorial
from math_utils import PI

def main():
    # Test string utilities
    raw_text: str = "  hello world from sharpy  "
    clean_text: str = sanitize(raw_text)
    processor = TextProcessor(clean_text)
    
    print(processor.word_count())
    print(processor.to_upper())
    print(truncate(clean_text, 10))
    
    # Test math utilities
    nums: list[int] = [10, 20, 30, 40, 50]
    print(square(7))
    print(average(nums))
    print(clamp(150, 0, 100))
    print(factorial(5))
    print(PI)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
4
HELLO WORLD FROM SHARPY
hello worl...
49.0
30.0
100
120
3.14159

```

### Actual
```
4
HELLO WORLD FROM SHARPY
hello worl...
49
30.0
100
120
3.14159
```

## Timing

- Generation: 114.64s
- Execution: 5.39s
