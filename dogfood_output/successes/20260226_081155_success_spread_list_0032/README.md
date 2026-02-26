# Successful Dogfood Run

**Timestamp:** 2026-02-26T08:09:18.327560
**Feature Focus:** spread_list
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Advanced spread_list testing with type aliases, abstract inheritance, and comprehensions
type IntList = list[int]

@abstract
class DataSource:
    @abstract
    def fetch(self) -> IntList: ...
    
    def process(self, extra: IntList) -> IntList:
        data = self.fetch()
        return [*data, *extra]

class StaticSource(DataSource):
    values: IntList
    
    def __init__(self, vals: IntList):
        self.values = vals
    
    @override
    def fetch(self) -> IntList:
        return [*self.values]

class ComputedSource(DataSource):
    base: int
    count: int
    
    def __init__(self, start: int, n: int):
        self.base = start
        self.count = n
    
    @override
    def fetch(self) -> IntList:
        result: IntList = []
        i = self.base
        while i < self.base + self.count:
            result.append(i * i)
            i += 1
        return result

def combine_sources(sources: list[DataSource], prefix: IntList) -> IntList:
    result: IntList = [*prefix]
    for src in sources:
        data = src.fetch()
        result = [*result, 999, *data]
    return result

def main():
    s1 = StaticSource([10, 20])
    s2 = ComputedSource(1, 3)
    
    combined = combine_sources([s1, s2], [0, 0])
    
    suffix: IntList = [x * 2 for x in [5, 10]]
    final: IntList = [*combined, *suffix]
    
    for val in final:
        print(val)
```

## Output

```
0
0
999
10
20
999
1
4
9
10
20
```

## Timing

- Generation: 147.02s
- Execution: 4.77s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
