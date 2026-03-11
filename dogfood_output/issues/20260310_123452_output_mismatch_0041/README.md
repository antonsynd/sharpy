# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T12:28:01.365851
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main module importing and using utils module
from utils import NumberStats, validate_non_empty, double_value, triple_value

def main():
    # Test 1: Function reference used as parameter
    validator: (str) -> bool = validate_non_empty
    print(validator("hello"))
    print(validator(""))
    
    # Test 2: Class with internal operations
    stats = NumberStats()
    stats.add(10)
    stats.add(20)
    stats.add(30)
    print(stats.get_count())
    print(stats.sum())
    
    # Test 3: Method chaining with function type parameter
    doubled = stats.apply_transform(double_value)
    print(doubled.sum())
    
    # Test 4: Lambda with inferred types
    tripled = stats.apply_transform(lambda x: x * 3)
    print(tripled.sum())
    
    # Test 5: Using imported direct function
    print(triple_value(7))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
True
False
3
60
120
360
21

```

### Actual
```
True
False
3
60
120
180
21
```

## Timing

- Generation: 282.92s
- Execution: 5.16s
