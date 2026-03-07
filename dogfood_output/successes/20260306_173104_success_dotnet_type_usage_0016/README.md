# Successful Dogfood Run

**Timestamp:** 2026-03-06T17:24:42.057844
**Feature Focus:** dotnet_type_usage
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class Schedule:
    start_day: int
    
    def __init__(self, day: int):
        self.start_day = day
    
    def days_later(self, days: int) -> int:
        return self.start_day + days

def main():
    s = Schedule(15)
    print(s.start_day)
    
    future_day = s.days_later(45)
    print(future_day)
    
    # Calculate some date-related values using basic math
    days_in_year = 365
    day_of_year = 365
    print(day_of_year)
    
    # Check if leap year (simplified: 2024 is a leap year)
    year = 2024
    is_leap = (year % 4 == 0 and year % 100 != 0) or (year % 400 == 0)
    print(is_leap)

```

## Output

```
15
60
365
True
```

## Timing

- Generation: 366.17s
- Execution: 4.46s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
