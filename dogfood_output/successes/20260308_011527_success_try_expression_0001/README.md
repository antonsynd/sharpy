# Successful Dogfood Run

**Timestamp:** 2026-03-08T01:08:16.273320
**Feature Focus:** try_expression
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def safe_divide(a: int, b: int) -> int !Exception:
    return try a // b

def format_result(r: int !Exception) -> str:
    value: int = r.unwrap_or(-1)
    if value == -1:
        return "error"
    return f"success: {value}"

def main():
    # Test try expression with successful division
    result1: int !Exception = safe_divide(24, 6)
    print(format_result(result1))
    
    # Test try expression with division by zero
    result2: int !Exception = safe_divide(10, 0)
    print(format_result(result2))
    
    # Test unwrap on success
    result3: int !Exception = safe_divide(35, 5)
    print(result3.unwrap())
    
    # Test unwrap_or with error
    result4: int !Exception = safe_divide(100, 0)
    print(result4.unwrap_or(-999))
    
    # Test unwrap on success with map
    result5: int !Exception = safe_divide(42, 7)
    mapped: str !Exception = result5.map(lambda x: x * 2).map(lambda x: f"value: {x}")
    print(mapped.unwrap())
    
    # Basic try expression in main
    result6: int !Exception = try 100 // 25
    print(result6.unwrap())
    
    # Try expression with division by zero
    result7: int !Exception = try 50 // 0
    print(result7.unwrap_or(0))

```

## Output

```
success: 4
error
7
-999
value: 12
4
0
```

## Timing

- Generation: 408.90s
- Execution: 5.54s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
