# Issue Report: output_mismatch

**Timestamp:** 2026-02-24T03:39:07.254687
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates module imports with inheritance
from base_utils import Processor, double_value
from advanced_utils import AdvancedProcessor, apply_twice

def main():
    simple = Processor(3)
    print(simple.compute())
    
    doubled = double_value(7)
    print(doubled)
    
    advanced = AdvancedProcessor(4, 5)
    print(advanced.compute())
    
    total = apply_twice(advanced)
    print(total)
    
    combined = simple.compute() + doubled
    print(combined)

# EXPECTED OUTPUT:
# 13
# 14
# 20
# 40
# 27
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
13
14
20
40
27

```

### Actual
```
13
14
20
28
27
```

## Timing

- Generation: 330.50s
- Execution: 4.61s
