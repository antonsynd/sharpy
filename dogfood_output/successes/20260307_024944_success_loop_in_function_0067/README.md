# Successful Dogfood Run

**Timestamp:** 2026-03-07T02:49:13.447346
**Feature Focus:** loop_in_function
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Sum digits using while loop in a function
# Demonstrates modulo and floor division within a loop
def sum_digits(n: int) -> int:
    total: int = 0
    while n > 0:
        digit: int = n % 10
        total += digit
        n = n // 10
    return total

def main():
    result: int = sum_digits(12345)
    print(result)

```

## Output

```
15
```

## Timing

- Generation: 21.00s
- Execution: 4.69s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
