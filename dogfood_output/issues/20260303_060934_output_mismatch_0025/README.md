# Issue Report: output_mismatch

**Timestamp:** 2026-03-03T06:05:22.761952
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
from module_utils import Calculator, square, double, PI

def main():
    calc: Calculator = Calculator()
    
    # Add some values
    calc.add(10.0)
    calc.add(20.0)
    calc.add(30.0)
    
    sum_val: float = calc.add(0.0)
    avg: float = calc.average()
    cnt: int = calc.count()
    
    print(f"Count: {cnt}")
    print(f"Sum: {sum_val}")
    print(f"Average: {avg}")
    
    # Test standalone functions
    num: float = 5.0
    sq: float = square(num)
    dbl: float = double(num)
    
    print(f"Square: {sq}")
    print(f"Double: {dbl}")
    
    # Use constant from module
    print(f"PI: {PI}")

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Count: 4
Sum: 60.0
Average: 15.0
Square: 25.0
Double: 10.0
PI: 3.14159

```

### Actual
```
Count: 4
Sum: 60.0
Average: 15.0
Square: 25.0
Double: 5.0
PI: 3.14159
```

## Timing

- Generation: 176.15s
- Execution: 4.84s
