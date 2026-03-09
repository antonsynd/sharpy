# Issue Report: output_mismatch

**Timestamp:** 2026-03-08T16:18:53.704414
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from modules and demonstrates usage

from math_utils import sum_list, average, is_prime, Statistics
from string_utils import TextFormatter, total_length, is_palindrome, reverse_string, count_vowels

def main():
    # Test math_utils functions
    nums: list[int] = [1, 2, 3, 4, 5]
    total: int = sum_list(nums)
    print(total)
    
    # Test average with floats
    values: list[float] = [10.0, 20.0, 30.0, 40.0]
    avg: float = average(values)
    print(avg)
    
    # Test prime checking
    print(is_prime(17))
    print(is_prime(18))
    
    # Test Statistics class
    stats: Statistics = Statistics([5.0, 10.0, 15.0, 20.0])
    print(stats.min_val())
    print(stats.max_val())
    print(stats.range())
    
    # Test TextFormatter class
    formatter: TextFormatter = TextFormatter("INFO")
    print(formatter.format("Test message"))
    
    # Test total_length
    words: list[str] = ["hello", "world", "spy"]
    print(total_length(words))
    
    # Test palindrome
    print(is_palindrome("radar"))
    print(is_palindrome("hello"))
    
    # Test reverse_string
    print(reverse_string("spy"))
    
    # Test count_vowels
    print(count_vowels("Sharpy"))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
15
25.0
True
False
5.0
20.0
15.0
INFO: Test message
12
True
False
yps
2

```

### Actual
```
15
25.0
True
False
5.0
20.0
15.0
INFO: Test message
13
True
False
yps
1
```

## Timing

- Generation: 74.95s
- Execution: 5.43s
