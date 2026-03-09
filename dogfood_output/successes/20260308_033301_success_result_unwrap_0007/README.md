# Successful Dogfood Run

**Timestamp:** 2026-03-08T03:32:01.306250
**Feature Focus:** result_unwrap
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Result chaining with validation pipeline
# Combines Result creation, map, map_err, and unwrap_or for data processing

def parse_int(s: str) -> int !str:
    if s == "42":
        return Ok(42)
    elif s == "100":
        return Ok(100)
    elif s == "0":
        return Ok(0)
    else:
        return Err(f"Invalid number: {s}")

def validate_positive(n: int) -> int !str:
    if n > 0:
        return Ok(n)
    else:
        return Err("Number must be positive")

def double_value(n: int) -> int:
    return n * 2

def process_input(input: str) -> int:
    # Chain: parse -> validate -> transform, with error fallbacks
    parsed = parse_int(input)
    
    # Transform error message
    with_error_code = parsed.map_err(lambda e: f"PARSE_ERROR: {e}")
    
    # Unwrap with default if parse failed
    raw_value = with_error_code.unwrap_or(-1)
    print(raw_value)
    
    # Create new result from unwrapped value
    validated: int !str = validate_positive(raw_value)
    
    # Transform success value
    doubled = validated.map(lambda x: double_value(x))
    
    # Get final result with fallback
    return doubled.unwrap_or(-999)

def main():
    # Test case 1: Valid positive
    result1 = process_input("42")
    print(result1)
    
    # Test case 2: Another valid value
    result2 = process_input("100")
    print(result2)
    
    # Test case 3: Invalid input (parse fails)
    result3 = process_input("abc")
    print(result3)
    
    # Test case 4: Zero (fails validation)
    result4 = process_input("0")
    print(result4)

```

## Output

```
42
84
100
200
-1
-999
0
-999
```

## Timing

- Generation: 48.95s
- Execution: 5.33s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
