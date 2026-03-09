# Successful Dogfood Run

**Timestamp:** 2026-03-08T03:41:21.330542
**Feature Focus:** generator_basic
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Generator with conditional yielding - only yields even numbers
def evens_up_to(n: int) -> int:
    i = 0
    while i <= n:
        if i % 2 == 0:
            yield i
        i += 1

def main():
    for x in evens_up_to(10):
        print(x)

```

## Output

```
0
2
4
6
8
10
```

## Timing

- Generation: 53.29s
- Execution: 5.09s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
