# Successful Dogfood Run

**Timestamp:** 2026-02-26T00:19:01.155287
**Feature Focus:** generator_yield_from
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test yield_from delegation between generator methods in a class
class IntervalGenerator:
    start: int
    gap: int
    
    def __init__(self, start: int, gap: int):
        self.start = start
        self.gap = gap
    
    def low_values(self) -> int:
        i = 0
        while i < 3:
            yield self.start + i * self.gap
            i += 1
    
    def high_values(self) -> int:
        i = 0
        while i < 3:
            yield self.start + 10 + i * self.gap
            i += 1
    
    def all_values(self) -> int:
        yield from self.low_values()
        yield from self.high_values()
        yield self.start + 100

def main():
    gen = IntervalGenerator(5, 2)
    for n in gen.all_values():
        print(n)
```

## Output

```
5
7
9
15
17
19
105
```

## Timing

- Generation: 162.06s
- Execution: 4.55s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
