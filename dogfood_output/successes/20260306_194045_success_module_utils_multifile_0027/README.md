# Successful Dogfood Run

**Timestamp:** 2026-03-06T19:35:27.241431
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (2 files)

## Source Files

### utils.spy

```python
# Utility module providing helper classes and functions
# Provides: Text utilities, numeric helpers, and a factory pattern

class TextFormatter:
    _prefix: str

    def __init__(self, prefix: str):
        self._prefix = prefix

    def format(self, message: str) -> str:
        return f"[{self._prefix}] {message}"

    def get_prefix(self) -> str:
        return self._prefix

class Counter:
    _count: int

    def __init__(self, start: int = 0):
        self._count = start

    def increment(self) -> int:
        self._count += 1
        return self._count

    def reset(self) -> None:
        self._count = 0

    def get_value(self) -> int:
        return self._count

def calculate_average(values: list[int]) -> float:
    if len(values) == 0:
        return 0.0
    total: int = 0
    for v in values:
        total += v
    return total / len(values)

def clamp_value(value: int, min_val: int, max_val: int) -> int:
    if value < min_val:
        return min_val
    if value > max_val:
        return max_val
    return value

```

### main.spy

```python
# Main entry point - demonstrates usage of utils module
from utils import TextFormatter, Counter, calculate_average, clamp_value

def main():
    # Test TextFormatter class
    formatter = TextFormatter("INFO")
    formatted = formatter.format("System started")
    print(formatted)

    # Test Counter class with increment
    counter = Counter(10)
    print(counter.get_value())
    counter.increment()
    counter.increment()
    print(counter.get_value())
    counter.reset()
    print(counter.get_value())

    # Test calculate_average function
    scores: list[int] = [85, 90, 78, 92, 88]
    avg = calculate_average(scores)
    print(avg)

    # Test clamp_value function
    result1 = clamp_value(150, 0, 100)
    result2 = clamp_value(-10, 0, 100)
    result3 = clamp_value(50, 0, 100)
    print(result1)
    print(result2)
    print(result3)

```

## Timing

- Generation: 297.96s
- Execution: 4.60s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
