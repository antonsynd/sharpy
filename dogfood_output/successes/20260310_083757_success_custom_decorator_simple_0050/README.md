# Successful Dogfood Run

**Timestamp:** 2026-03-10T08:35:12.856933
**Feature Focus:** custom_decorator_simple
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Simple custom decorator application
# Demonstrates @system.serializable on class and @obsolete on method

@system.serializable
class ThresholdConfig:
    limit: float
    
    def __init__(self, limit: float):
        self.limit = limit
    
    @obsolete("Use check_bounds instead")
    def old_check(self, val: float) -> bool:
        return val < self.limit
    
    def check_bounds(self, val: float) -> bool:
        return val < self.limit

def main():
    config = ThresholdConfig(100.0)
    
    # Print the threshold to verify object creation
    print(config.limit)
    
    # Test with multiple values
    values: list[float] = [50.0, 150.0, 99.0]
    passed: int = 0
    
    for v in values:
        if config.check_bounds(v):
            print("pass")
            passed = passed + 1
        else:
            print("fail")
    
    print(passed)

```

## Output

```
100.0
pass
fail
pass
2
```

## Timing

- Generation: 152.65s
- Execution: 5.26s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
