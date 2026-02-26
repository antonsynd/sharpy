# Successful Dogfood Run

**Timestamp:** 2026-02-25T04:21:56.860800
**Feature Focus:** bool_variables
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Bool variable declarations and logical operations
def main():
    is_active: bool = True
    is_valid: bool = False
    
    and_result: bool = is_active and is_valid
    or_result: bool = is_active or is_valid
    not_result: bool = not is_active
    
    print(and_result)
    print(or_result)
    print(not_result)
    print(is_active)
    print(is_valid)
    
# EXPECTED OUTPUT:
# False
# True
# False
# True
# False
```

## Output

```
False
True
False
True
False
```

## Timing

- Generation: 43.23s
- Execution: 4.22s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
