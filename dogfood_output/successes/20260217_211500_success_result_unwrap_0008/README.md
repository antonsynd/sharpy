# Successful Dogfood Run

**Timestamp:** 2026-02-17T21:11:53.900567
**Feature Focus:** result_unwrap
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Result unwrap in computation pipeline with error propagation
# Tests chaining Results, unwrap in conditional contexts, and unwrap_or patterns

class Calculator:
    last_result: float
    
    def __init__(self):
        self.last_result = 0.0
    
    def divide(self, a: float, b: float) -> float !str:
        if b == 0.0:
            return Err("Division by zero")
        return Ok(a / b)
    
    def compute_average(self, total: float, count: int) -> float !str:
        if count <= 0:
            return Err("Invalid count")
        if total < 0.0:
            return Err("Negative total")
        return Ok(total / float(count))

def process_data(total: float, count: int) -> float !str:
    calc: Calculator = Calculator()
    return calc.compute_average(total, count)

def main():
    # Successful computation - unwrap works
    result1: float !str = process_data(100.0, 4)
    print(result1.unwrap())
    
    # Error case - use unwrap_or to provide default
    result2: float !str = process_data(100.0, 0)
    print(result2.unwrap_or(-1.0))
    
    # Calculator operations chaining
    calc: Calculator = Calculator()
    step1: float !str = calc.divide(20.0, 4.0)
    intermediate: float = step1.unwrap_or(0.0)
    calc.last_result = intermediate
    print(calc.last_result)
    
    # Another error case
    step2: float !str = calc.divide(10.0, 0.0)
    final_result: float = step2.unwrap_or(999.9)
    print(final_result)

# EXPECTED OUTPUT:
# 25.0
# -1.0
# 5.0
# 999.9
```

## Output

```
25.0
-1.0
5.0
999.9
```

## Timing

- Generation: 176.59s
- Execution: 4.43s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
