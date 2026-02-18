# Successful Dogfood Run

**Timestamp:** 2026-02-17T22:11:11.355416
**Feature Focus:** function_keyword_args
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Function keyword arguments with positional and named mix
def calculate(a: int, b: int, c: int) -> int:
    return a * 2 + b * 3 + c * 4

def main():
    # Mix positional and keyword args
    result: int = calculate(10, c=5, b=2)
    print(result)
    # EXPECTED OUTPUT:
    # 46
```

## Output

```
46
```

## Timing

- Generation: 30.39s
- Execution: 4.34s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
