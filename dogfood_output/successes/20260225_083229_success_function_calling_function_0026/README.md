# Successful Dogfood Run

**Timestamp:** 2026-02-25T08:30:29.795678
**Feature Focus:** function_calling_function
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def is_even(n: int) -> bool:
    return n % 2 == 0

def transform(n: int) -> int:
    if is_even(n):
        return n // 2
    else:
        return 3 * n + 1

def apply_twice(n: int) -> int:
    return transform(transform(n))

def main():
    seed: int = 5
    first = transform(seed)
    print(first)
    
    second = transform(first)
    print(second)
    
    result = apply_twice(seed)
    print(result)
    
    for i in range(3, 6):
        chained = apply_twice(i)
        print(chained)

# EXPECTED OUTPUT:
# 16
# 8
# 8
# 5
# 1
# 8

```

## Output

```
16
8
8
5
1
8
```

## Timing

- Generation: 110.00s
- Execution: 4.51s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
