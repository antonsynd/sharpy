# Issue Report: output_mismatch

**Timestamp:** 2026-03-03T05:52:39.248461
**Type:** output_mismatch
**Feature Focus:** dotnet_type_usage
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: .NET DateTime and TimeSpan type usage with arithmetic operations
from system import DateTime, TimeSpan

def time_calculator() -> None:
    # Create DateTime instances using .NET factory method
    start = DateTime.Now
    duration = TimeSpan.FromHours(2.5)
    
    # Perform DateTime arithmetic
    end = start.Add(duration)
    difference = end.Subtract(start)
    
    # Access .NET properties
    hours = difference.TotalHours
    minutes = difference.TotalMinutes
    
    print(f"Duration hours: {hours}")
    print(f"Duration minutes: {minutes}")

def main():
    print("=== DateTime Operations ===")
    time_calculator()
    print("=== Done ===")

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
=== DateTime Operations ===
Duration hours: 2.5
Duration minutes: 150.0
=== Done ===

```

### Actual
```
=== DateTime Operations ===
Duration hours: 2.5
Duration minutes: 150
=== Done ===
```

## Timing

- Generation: 110.77s
- Execution: 5.04s
