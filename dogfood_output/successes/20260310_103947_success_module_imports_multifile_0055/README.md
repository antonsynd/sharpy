# Successful Dogfood Run

**Timestamp:** 2026-03-10T10:32:47.794732
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (2 files)

## Source Files

### utils.spy

```python
interface IPrintable:
    def get_description(self) -> str: ...
    def get_value(self) -> int: ...

class Counter:
    count: int
    
    def __init__(self, start: int = 0):
        self.count = start
    
    def increment(self) -> int:
        self.count = self.count + 1
        return self.count
    
    def decrement(self) -> int:
        self.count = self.count - 1
        return self.count

class NamedCounter(Counter, IPrintable):
    name: str
    
    def __init__(self, name: str, start: int = 0):
        super().__init__(start)
        self.name = name
    
    def get_description(self) -> str:
        return f"Counter {self.name} at {self.count}"
    
    def get_value(self) -> int:
        return self.count

def running_total(items: list[int]) -> list[int]:
    result: list[int] = []
    total: int = 0
    for item in items:
        total = total + item
        result.append(total)
    return result

def compute_factorial(n: int) -> int:
    if n < 0:
        return 0
    result: int = 1
    i: int = 2
    while i <= n:
        result = result * i
        i = i + 1
    return result

```

### main.spy

```python
from utils import Counter, NamedCounter, IPrintable, running_total, compute_factorial

def describe(item: IPrintable):
    print(item.get_description())

def main():
    c = Counter(0)
    first: int = c.increment()
    second: int = c.increment()
    print(first)
    print(second)
    print(c.count)
    
    nc = NamedCounter("Alpha", 10)
    describe(nc)
    print(nc.get_value())
    
    totals: list[int] = running_total([1, 2, 3, 4, 5])
    print(totals[4])
    
    result: int = compute_factorial(5)
    print(result)

```

## Timing

- Generation: 405.40s
- Execution: 5.14s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
