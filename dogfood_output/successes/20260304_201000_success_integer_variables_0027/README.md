# Successful Dogfood Run

**Timestamp:** 2026-03-04T20:08:35.825920
**Feature Focus:** integer_variables
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Integer variable arithmetic with mixed operations
# Demonstrates accumulation pattern with positive/negative values
def main():
    total: int = 100
    offset: int = -25
    divisor: int = 7
    
    # Accumulate with negative offset
    total += offset
    
    # Floor division and modulo
    quotient: int = total // divisor
    remainder: int = total % divisor
    
    print(total)
    print(quotient)
    print(remainder)

```

## Output

```
75
10
5
```

## Timing

- Generation: 74.43s
- Execution: 4.80s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
