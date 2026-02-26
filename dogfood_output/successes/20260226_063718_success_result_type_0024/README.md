# Successful Dogfood Run

**Timestamp:** 2026-02-26T06:36:36.108643
**Feature Focus:** result_type
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
def safe_divide(a: int, b: int) -> int !str:
    if b == 0:
        return Err("division by zero")
    return Ok(a // b)

def main():
    result1: int !str = safe_divide(20, 4)
    print(result1.unwrap())
    
    result2: int !str = safe_divide(10, 0)
    print(result2.unwrap_or(-1))
    
    # Chain operations with map
    doubled: int !str = result1.map(lambda x: x * 2)
    print(doubled.unwrap_or(0))
```

## Output

```
5
-1
10
```

## Timing

- Generation: 32.47s
- Execution: 4.58s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
