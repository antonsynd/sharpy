# Issue Report: output_mismatch

**Timestamp:** 2026-02-19T00:46:58.304252
**Type:** output_mismatch
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point testing module imports
from types_module import Container, process_score
from constants import MAX_LIMIT, DEFAULT_NAME, PI_VALUE, is_within_limit, format_id

def main():
    # Test basic imports
    user_id: int = 42
    print(format_id(user_id))
    
    # Test constant imports
    print(DEFAULT_NAME)
    print(MAX_LIMIT)
    
    # Test container class from other module
    scores: Container = Container()
    scores.add(85.5)
    scores.add(92.0)
    scores.add(78.5)
    print(scores.count())
    
    # Test helper function from types_module
    first_score: float = scores.get_all()[0]
    print(process_score(first_score))
    
    # Test condition with imported constant
    test_value: int = 75
    if is_within_limit(test_value):
        print("Within limit")
    else:
        print("Over limit")
    
    # Test float constant import
    print(PI_VALUE)

# EXPECTED OUTPUT:
# ID-0042
# Unknown
# 100
# 3
# Score: 85.5
# Within limit
# 3.14159
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
ID-0042
Unknown
100
3
Score: 85.5
Within limit
3.14159

```

### Actual
```
ID-424d
Unknown
100
3
Score: 85.5
Within limit
3.14159
```

## Timing

- Generation: 129.37s
- Execution: 4.46s
