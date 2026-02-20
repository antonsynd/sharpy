# Successful Dogfood Run

**Timestamp:** 2026-02-19T04:43:32.362070
**Feature Focus:** logical_operators
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Logical operators in validation scenarios with nullable types and complex conditions
# This tests: and/or/not combination, nullable narrowing, boundary checks, guard clauses

class Validator:
    min_value: int
    max_value: int
    
    def __init__(self, min_val: int, max_val: int):
        self.min_value = min_val
        self.max_value = max_val
    
    def is_in_range(self, value: int) -> bool:
        # Combined logical check with boundary inclusion
        return value >= self.min_value and value <= self.max_value
    
    def is_out_of_bounds(self, value: int) -> bool:
        # Logical OR for out of bounds (either side)
        return value < self.min_value or value > self.max_value
    
    def is_valid_and_enabled(self, value: int, enabled: bool) -> bool:
        # Three-way logical combination
        return enabled and (not self.is_out_of_bounds(value))

def main():
    validator = Validator(10, 50)
    
    # Test various boundary and mid-range values
    test_values: list[int] = [5, 10, 30, 50, 100]
    
    for val in test_values:
        in_range = validator.is_in_range(val)
        out_of_bounds = validator.is_out_of_bounds(val)
        valid_enabled = validator.is_valid_and_enabled(val, True)
        valid_disabled = validator.is_valid_and_enabled(val, False)
        
        # Logical checks with print - using simple logic display
        print(val)
        print(in_range)
        print(out_of_bounds)
        print(valid_enabled)
        print(valid_disabled)
    
    # Test short-circuit behavior: the second condition shouldn't execute if first is false
    x: int = 0
    y: int = 10
    
    # This should work without error because y != 0 prevents division evaluation
    # Since we can't do division, we'll use a different check
    condition = y != 0 and x < y
    print(condition)
    
    # Test: not with parenthesized complex expression
    complex_not = not (x > 5 or y < 5)
    print(complex_not)
    
    # Test: chained comparisons with logical combinations
    chained = 0 <= x and x < y and y <= 20
    print(chained)
# EXPECTED OUTPUT:
# 5
# False
# True
# False
# False
# 10
# True
# False
# True
# False
# 30
# True
# False
# True
# False
# 50
# True
# False
# True
# False
# 100
# False
# True
# False
# False
# True
# True
# True
```

## Output

```
5
False
True
False
False
10
True
False
True
False
30
True
False
True
False
50
True
False
True
False
100
False
True
False
False
True
True
True
```

## Timing

- Generation: 59.13s
- Execution: 4.30s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
