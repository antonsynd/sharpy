# Successful Dogfood Run

**Timestamp:** 2026-03-03T07:08:51.613759
**Feature Focus:** generator_yield_from
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: yield_from with multiple nested generators - class state management
# Tests: yield from delegation, generator state preservation across multiple levels
# Novelty: Different structure than existing tests - uses class with accumulating state

class DataProcessor:
    values: list[int]
    
    def __init__(self, values: list[int]):
        self.values = values
    
    def _evens(self) -> int:
        for val in self.values:
            if val % 2 == 0:
                yield val
    
    def _odds(self) -> int:
        for val in self.values:
            if val % 2 == 1:
                yield val
    
    def _with_offset(self, offset: int) -> int:
        for val in self.values:
            yield val + offset
    
    def all_processed(self) -> int:
        yield 0  # marker
        yield from self._evens()
        yield -1  # separator
        yield from self._odds()
        yield -2  # separator
        yield from self._with_offset(100)

def main():
    data = DataProcessor([1, 2, 3, 4, 5])
    
    for n in data.all_processed():
        print(n)

```

## Output

```
0
2
4
-1
1
3
5
-2
101
102
103
104
105
```

## Timing

- Generation: 253.66s
- Execution: 4.89s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
