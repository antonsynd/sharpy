# Successful Dogfood Run

**Timestamp:** 2026-02-25T09:38:32.849373
**Feature Focus:** try_except_basic
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Multiple exception types with validation and computation
def validate_and_compute(a: str, b: str) -> float:
    try:
        x: float = float(a)
        y: float = float(b)
        if x > 100.0:
            raise RuntimeError("Value exceeds maximum")
        return x / y
    except ValueError:
        print("Error: Invalid number format")
        return 0.0
    except RuntimeError:
        print("Error: Validation failed")
        return 0.0

def main():
    print(validate_and_compute("50", "5"))
    print(validate_and_compute("150", "2"))
    print(validate_and_compute("80", "abc"))
    result: float = validate_and_compute("75", "3")
    print(result)
# EXPECTED OUTPUT:
# 10.0
# Error: Validation failed
# 0.0
# Error: Invalid number format
# 0.0
# 25.0
```

## Output

```
10.0
Error: Validation failed
0.0
Error: Invalid number format
0.0
25.0
```

## Timing

- Generation: 317.53s
- Execution: 4.48s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
