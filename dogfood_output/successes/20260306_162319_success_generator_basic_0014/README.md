# Successful Dogfood Run

**Timestamp:** 2026-03-06T16:15:31.331813
**Feature Focus:** generator_basic
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
enum ProcessingMode:
    RAW = 0
    FILTERED = 1
    TRANSFORMED = 2

class DataStream:
    _data: list[int]
    _mode: ProcessingMode
    
    def __init__(self, items: list[int], mode: ProcessingMode):
        self._data = items
        self._mode = mode
    
    @virtual
    def contents(self) -> int:
        for item in self._data:
            yield item
    
    @virtual
    def process(self) -> int:
        for item in self.contents():
            yield item

class BoundedStream(DataStream):
    _lower: int
    _upper: int
    
    def __init__(self, items: list[int], lower: int, upper: int):
        super().__init__(items, ProcessingMode.FILTERED)
        self._lower = lower
        self._upper = upper
    
    @override
    def process(self) -> int:
        for x in self.contents():
            if self._lower <= x and x <= self._upper:
                yield x

def classify_number(n: int) -> str:
    match n:
        case 0:
            return "zero"
        case _ if n < 0:
            return "negative"
        case _ if n % 2 == 0:
            return "even"
        case _:
            return "odd"

def main():
    values: list[int] = [0, 3, 5, -5, 8, 7, 15]
    stream: BoundedStream = BoundedStream(values, -10, 30)
    
    count: int = 0
    for val in stream.process():
        category: str = classify_number(val)
        print(f"{val} is {category}")
        count += 1
    
    print(f"count={count}")
    print(f"mode={stream._mode.name}")

```

## Output

```
0 is zero
3 is odd
5 is odd
-5 is negative
8 is even
7 is odd
15 is odd
count=7
mode=Filtered
```

## Timing

- Generation: 447.30s
- Execution: 4.75s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
