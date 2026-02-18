# Successful Dogfood Run

**Timestamp:** 2026-02-17T22:15:41.834019
**Feature Focus:** function_with_print
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Function with internal print statement during computation
def compute_and_print(a: int, b: int) -> int:
    doubled = a * 2
    print(doubled)
    return doubled + b

def main():
    final = compute_and_print(7, 5)
    print(final)

# EXPECTED OUTPUT:
# 14
# 19
```

## Output

```
14
19
```

## Timing

- Generation: 29.39s
- Execution: 4.33s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
