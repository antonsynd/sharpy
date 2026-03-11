# Successful Dogfood Run

**Timestamp:** 2026-03-10T08:23:59.487196
**Feature Focus:** match_type_binding
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Type binding with list filtering and custom class
class Value:
    _data: object
    
    def __init__(self, data: object):
        self._data = data
    
    property get data(self) -> object:
        return self._data

def extract_numbers(items: list[Value]) -> list[int]:
    result: list[int] = []
    for item in items:
        data = item.data
        if isinstance(data, int):
            result.append(data)
        elif isinstance(data, float):
            result.append(int(data))
        else:
            pass
    return result

def sum_if_positive(items: list[object]) -> int:
    total: int = 0
    for item in items:
        if isinstance(item, int):
            n: int = item
            if n > 0:
                total += n
        else:
            pass
    return total

def main():
    values: list[Value] = [Value(42), Value("skip"), Value(3.5), Value("also skip"), Value(-7)]
    numbers: list[int] = extract_numbers(values)
    print(numbers[0])
    print(numbers[1])
    print(len(numbers))
    
    mixed: list[object] = [5, -2, 10, "ignored", 3.14, 0, 15]
    total: int = sum_if_positive(mixed)
    print(total)
    
    # Test direct type binding
    raw: object = 100
    if isinstance(raw, int):
        bound: int = raw
        print(bound * 2)
    else:
        print(0)

```

## Output

```
42
3
3
30
200
```

## Timing

- Generation: 323.88s
- Execution: 5.35s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
