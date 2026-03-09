# Successful Dogfood Run

**Timestamp:** 2026-03-08T04:46:01.109405
**Feature Focus:** result_type
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Result type with arithmetic operations and error mapping
def divide(a: float, b: float) -> float !str:
    if b == 0.0:
        return Err("division by zero")
    return Ok(a / b)

def double_value(x: float) -> float:
    return x * 2.0

def prefix_error(e: str) -> str:
    return f"error: {e}"

def main():
    # Ok result with unwrap
    r1: float !str = divide(20.0, 4.0)
    print(r1.unwrap())
    
    # Err result with unwrap_or
    r2: float !str = divide(15.0, 0.0)
    print(r2.unwrap_or(-1.0))
    
    # map on Ok result
    r3: float !str = Ok(5.0)
    mapped = r3.map(double_value)
    print(mapped.unwrap())
    
    # map_err on Err result
    r4: float !str = divide(8.0, 0.0)
    remapped = r4.map_err(prefix_error)
    print(remapped.unwrap_or(0.0))

```

## Output

```
5.0
-1.0
10.0
0.0
```

## Timing

- Generation: 113.50s
- Execution: 5.37s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
