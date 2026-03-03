# Successful Dogfood Run

**Timestamp:** 2026-03-03T08:38:15.145677
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (2 files)

## Source Files

### utils.spy

```python
# Utility module with concrete implementations

def swap_int(a: int, b: int) -> tuple[int, int]:
    return (b, a)

def swap_str(a: str, b: str) -> tuple[str, str]:
    return (b, a)

def clamp(value: int, min_val: int, max_val: int) -> int:
    if value < min_val:
        return min_val
    elif value > max_val:
        return max_val
    return value

def transform_list(items: list[str], transformer: (str) -> str) -> list[str]:
    result: list[str] = []
    for item in items:
        result.append(transformer(item))
    return result

interface IPicker[T]:
    def pick(self, items: list[T]) -> T: ...

class LastPicker(IPicker[int]):
    def pick(self, items: list[int]) -> int:
        last_idx: int = len(items) - 1
        return items[last_idx]

class UpperCaser:
    def transform(self, s: str) -> str:
        return s.upper()

```

### main.spy

```python
# Main entry point
from utils import swap_int, clamp, swap_str, transform_list, IPicker, LastPicker, UpperCaser

def main():
    # Test swap_int function
    swapped: tuple[int, int] = swap_int(10, 20)
    print(swapped[0])
    print(swapped[1])
    
    # Test swap_str function
    swapped_str: tuple[str, str] = swap_str("hello", "world")
    print(swapped_str[0])
    print(swapped_str[1])
    
    # Test clamp utility
    value: int = 150
    clamped: int = clamp(value, 0, 100)
    print(clamped)
    
    # Test LastPicker class implementing IPicker interface
    numbers: list[int] = [1, 2, 3, 4, 5]
    picker: LastPicker = LastPicker()
    last: int = picker.pick(numbers)
    print(last)
    
    # Test UpperCaser
    caser: UpperCaser = UpperCaser()
    print(caser.transform("hello"))
    
    # Test transform_list with a lambda
    words: list[str] = ["apple", "banana", "cherry"]
    upper_words: list[str] = transform_list(words, lambda s: s.upper())
    print(upper_words[0])
    print(upper_words[1])
    print(upper_words[2])

```

## Timing

- Generation: 501.96s
- Execution: 4.98s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
