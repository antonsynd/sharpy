# Successful Dogfood Run

**Timestamp:** 2026-03-08T16:05:42.707682
**Feature Focus:** star_unpacking
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class DataProcessor:
    values: list[int]
    
    def __init__(self, values: list[int]) -> None:
        self.values = values
    
    def analyze(self) -> tuple[int, int, int]:
        if len(self.values) < 3:
            result: tuple[int, int, int] = (0, 0, 0)
            return result
        
        first: int = self.values[0]
        last: int = self.values[len(self.values) - 1]
        middle_count: int = len(self.values) - 2
        
        result: tuple[int, int, int] = (first, middle_count, last)
        return result
    
    def summary(self) -> int:
        a: int
        b: int
        c: int
        a, b, c = self.analyze()
        return a + b * 10 + c

def main():
    dp: DataProcessor = DataProcessor([5, 10, 15, 20, 25])
    start: int
    middle_count: int
    end: int
    start, middle_count, end = dp.analyze()
    print(start)
    print(middle_count)
    print(end)
    print(dp.summary())
    
    dp2: DataProcessor = DataProcessor([100, 200, 300])
    print(dp2.summary())

```

## Output

```
5
3
25
60
410
```

## Timing

- Generation: 331.91s
- Execution: 5.06s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
