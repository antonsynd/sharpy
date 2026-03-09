# Successful Dogfood Run

**Timestamp:** 2026-03-08T20:35:15.448796
**Feature Focus:** generator_early_return
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test generator with early return for conditional termination
def bounded_generator(limit: int) -> int:
    n = 1
    while True:
        if n > limit:
            return  # Early exit - generator stops here
        yield n * n
        n += 1

def filtered_squares(max_value: int, skip_threshold: int) -> int:
    for sq in bounded_generator(max_value):
        if sq > skip_threshold:
            yield sq

def main():
    # Generator with early return at 10, filtering values > 20
    result: list[int] = []
    for value in filtered_squares(10, 20):
        result.append(value)
    
    # Print results
    for v in result:
        print(v)
    
    # Test early return triggers correctly
    empty_result: list[int] = []
    for v in bounded_generator(0):
        empty_result.append(v)
    print(len(empty_result))
    
    # Test single iteration before return
    single_list: list[int] = []
    for v in bounded_generator(1):
        single_list.append(v)
    print(single_list[0])

```

## Output

```
25
36
49
64
81
100
0
1
```

## Timing

- Generation: 115.44s
- Execution: 5.26s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
