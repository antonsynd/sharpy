# Issue Report: output_mismatch

**Timestamp:** 2026-03-04T15:14:50.970203
**Type:** output_mismatch
**Feature Focus:** try_except_else
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test try/except/else with class-based data validation and processing
# The else block runs only when try succeeds, with access to modified values

class DataProcessor:
    _data: list[int]
    _total: int
    
    def __init__(self):
        self._data = []
        self._total = 0
    
    def add_value(self, value: int) -> None:
        self._data.append(value)
        self._total += value
    
    def get_average(self) -> float:
        if len(self._data) == 0:
            return 0.0
        return self._total / len(self._data)
    
    def process_batch(self, values: list[int]) -> str:
        success_count = 0
        processed: list[int] = []
        
        for val in values:
            try:
                # Simulate potential failure for negative values
                if val < 0:
                    raise ValueError("negative value")
                self.add_value(val)
                processed.append(val)
            except ValueError:
                # Skip invalid values
                pass
            else:
                # Only count if no exception was raised
                success_count += 1
        
        if success_count > 0:
            return f"processed {success_count} values, avg={self.get_average()}"
        else:
            return "no valid values"

def safe_divide(a: float, b: float) -> float:
    result: float = 0.0
    try:
        result = a / b
    except ZeroDivisionError:
        return 0.0
    else:
        # else runs only when division succeeds
        return result * 2.0  # Double the result in else block

def main():
    processor = DataProcessor()
    
    # Test 1: Process mix of valid and invalid values
    result1 = processor.process_batch([10, 20, -5, 30, -10, 40])
    print(result1)
    
    # Test 2: Process all invalid values
    result2 = processor.process_batch([-1, -2, -3])
    print(result2)
    
    # Test 3: Safe division - success path
    print(safe_divide(10.0, 2.0))
    
    # Test 4: Safe division - exception path
    print(safe_divide(5.0, 0.0))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
processed 4 values, avg=25.0
no valid values
10.0
0.0

```

### Actual
```
processed 4 values, avg=25.0
no valid values
10.0
inf
```

## Timing

- Generation: 139.87s
- Execution: 4.90s
