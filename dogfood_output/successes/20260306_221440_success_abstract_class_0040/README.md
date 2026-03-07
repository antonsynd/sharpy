# Successful Dogfood Run

**Timestamp:** 2026-03-06T22:09:06.026165
**Feature Focus:** abstract_class
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Abstract class with template method pattern
# The base class defines an algorithm that uses abstract methods,
# while concrete subclasses provide the specific implementations.

@abstract
class DataProcessor:
    @abstract
    def read_data(self) -> list[int]: ...
    
    @abstract
    def transform(self, data: list[int]) -> list[int]: ...
    
    def process(self) -> int:
        result = 0
        for x in self.transform(self.read_data()):
            result = result + x
        return result

class DoublingProcessor(DataProcessor):
    _values: list[int]
    
    def __init__(self, values: list[int]):
        self._values = values
    
    @override
    def read_data(self) -> list[int]:
        return self._values
    
    @override
    def transform(self, data: list[int]) -> list[int]:
        result: list[int] = []
        i = 0
        while i < len(data):
            result.append(data[i] * 2)
            i = i + 1
        return result

def main():
    p = DoublingProcessor([1, 2, 3, 4])
    print(p.read_data()[0])
    print(p.process())
    print(p.process() * 2)

```

## Output

```
1
20
40
```

## Timing

- Generation: 322.58s
- Execution: 5.65s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
