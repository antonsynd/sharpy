# Successful Dogfood Run

**Timestamp:** 2026-02-17T22:21:18.465284
**Feature Focus:** if_else_simple
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    value: int = -7
    result: int = 0

    if value < 0:
        result = value * -1
    else:
        result = value * 2

    print(result)

    threshold: int = 10
    adjusted: int = 0

    if threshold >= 10:
        adjusted = threshold // 2
    else:
        adjusted = threshold * 3

    print(adjusted)
# EXPECTED OUTPUT:
# 7
# 5
```

## Output

```
7
5
```

## Timing

- Generation: 33.32s
- Execution: 4.45s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
