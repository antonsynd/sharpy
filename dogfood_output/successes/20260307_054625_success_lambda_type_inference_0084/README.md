# Successful Dogfood Run

**Timestamp:** 2026-03-07T05:44:04.743481
**Feature Focus:** lambda_type_inference
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Lambda type inference with arithmetic expression
def apply(f: (int) -> int, x: int) -> int:
    return f(x)

def main():
    r = apply(lambda n: n * n + 1, 4)
    print(r)

```

## Output

```
17
```

## Timing

- Generation: 130.10s
- Execution: 4.63s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
