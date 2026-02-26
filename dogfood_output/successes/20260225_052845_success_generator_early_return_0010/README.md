# Successful Dogfood Run

**Timestamp:** 2026-02-25T05:26:48.749927
**Feature Focus:** generator_early_return
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def accumulate_until(threshold: int, values: list[int]) -> int:
    running: int = 0
    for value in values:
        running += value
        if running > threshold:
            return  # Early return - stop yielding before exceeding threshold
        yield running

def main():
    data: list[int] = [5, 10, 15, 8, 20]
    
    yielded_count: int = 0
    for partial in accumulate_until(35, data):
        yielded_count += 1
        print(partial)
    
    print(f"Yielded: {yielded_count}")
    print(f"Stopped before adding value causing excess")

# EXPECTED OUTPUT:
# 5
# 15
# 30
# Yielded: 3
```

## Output

```
5
15
30
Yielded: 3
Stopped before adding value causing excess
```

## Timing

- Generation: 106.48s
- Execution: 4.71s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
