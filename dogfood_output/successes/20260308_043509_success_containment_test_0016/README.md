# Successful Dogfood Run

**Timestamp:** 2026-03-08T04:33:43.330143
**Feature Focus:** containment_test
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Custom container with __contains__ for numeric range checking
class RangeContainer:
    min_val: int
    max_val: int
    
    def __init__(self, min_v: int, max_v: int):
        self.min_val = min_v
        self.max_val = max_v
    
    def __contains__(self, value: int) -> bool:
        return self.min_val <= value <= self.max_val

def main():
    range_a = RangeContainer(10, 50)
    range_b = RangeContainer(100, 200)
    
    test_values: list[int] = [5, 25, 75, 150, 250]
    
    for val in test_values:
        if val in range_a:
            print(f"{val} in range_a")
        elif val in range_b:
            print(f"{val} in range_b")
        else:
            print(f"{val} not found")

```

## Output

```
5 not found
25 in range_a
75 not found
150 in range_b
250 not found
```

## Timing

- Generation: 74.45s
- Execution: 5.15s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
