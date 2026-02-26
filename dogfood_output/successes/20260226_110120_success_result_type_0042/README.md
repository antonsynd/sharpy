# Successful Dogfood Run

**Timestamp:** 2026-02-26T10:54:25.927843
**Feature Focus:** result_type
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Result types with enum errors and map transformations
enum ErrorCode:
    INVALID = 400
    NOT_FOUND = 404

def validate_id(id: int) -> int !ErrorCode:
    if id <= 0:
        return Err(ErrorCode.INVALID)
    elif id > 100:
        return Err(ErrorCode.NOT_FOUND)
    return Ok(id * 2)

def process(id: int) -> str !ErrorCode:
    result = validate_id(id)
    return result.map(lambda n: f"value-{n}")

def main():
    # Test success with map transformation
    r1 = process(10)
    print(r1.unwrap_or("fail"))
    
    # Test INVALID error path
    r2 = process(0)
    print(r2.unwrap_or("fail"))
    
    # Test NOT_FOUND error path  
    r3 = process(150)
    print(r3.unwrap_or("fail"))
    
    # Test map_err transforms error codes
    e: int !ErrorCode = Err(ErrorCode.INVALID)
    transformed = e.map_err(lambda c: ErrorCode.NOT_FOUND)
    print(transformed.unwrap_or(-1))
```

## Output

```
value-20
fail
fail
-1
```

## Timing

- Generation: 404.08s
- Execution: 4.69s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
