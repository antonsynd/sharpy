# Successful Dogfood Run

**Timestamp:** 2026-03-08T04:57:57.057426
**Feature Focus:** keyword_only_params
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Basic function with keyword-only parameters and default values
# Demonstrates that params after * must be passed by keyword

def calculate(base: int, *, multiplier: int = 2, offset: int = 0) -> int:
    return base * multiplier + offset

def main():
    # Call with just positional arg (base), use defaults for keyword-only params
    result1: int = calculate(5)
    print(result1)
    
    # Call with positional arg and one keyword-only arg
    result2: int = calculate(3, multiplier=3)
    print(result2)
    
    # Call with all keyword-only args provided
    result3: int = calculate(4, multiplier=5, offset=10)
    print(result3)
    
    # Call with keyword-only args in different order
    result4: int = calculate(2, offset=7, multiplier=3)
    print(result4)

```

## Output

```
10
9
30
13
```

## Timing

- Generation: 42.50s
- Execution: 5.10s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
