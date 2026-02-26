# Successful Dogfood Run

**Timestamp:** 2026-02-25T04:17:53.992297
**Feature Focus:** generator_early_return
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class NumberStream:
    max_value: int
    stop_at: int
    
    def __init__(self, max_val: int, stop: int):
        self.max_value = max_val
        self.stop_at = stop
    
    def generate_with_early_return(self) -> int:
        i = 0
        while i < self.max_value:
            if i == self.stop_at:
                return  # Early termination - no yielded value for stop_at
            yield i
            i += 1

def conditional_yield(max_n: int, skip: int) -> int:
    for n in range(max_n):
        if n == skip:
            return  # Early return stops the generator immediately
        yield n * 2

def main():
    stream = NumberStream(10, 5)
    print("Stream with early return at 5:")
    for val in stream.generate_with_early_return():
        print(val)
    
    print("Conditional yield with early return at 3:")
    for val in conditional_yield(8, 3):
        print(val)
    
    print("Early return not triggered (stop_at > max):")
    stream2 = NumberStream(4, 10)
    count = 0
    for _ in stream2.generate_with_early_return():
        count += 1
    print(count)

# EXPECTED OUTPUT:
# 0
# 1
# 2
# 3
# 4
# 0
# 2
# 4
# 4
```

## Output

```
Stream with early return at 5:
0
1
2
3
4
Conditional yield with early return at 3:
0
2
4
Early return not triggered (stop_at > max):
4
```

## Timing

- Generation: 122.97s
- Execution: 4.52s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
