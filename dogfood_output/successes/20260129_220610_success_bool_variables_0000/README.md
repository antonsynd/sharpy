# Successful Dogfood Run

**Timestamp:** 2026-01-29T22:05:54.096462
**Feature Focus:** bool_variables
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test boolean variables with simple logic

def main():
    is_enabled: bool = True
    is_active: bool = False
    
    print(is_enabled)
    print(is_active)
    print(is_enabled and is_active)
    print(is_enabled or is_active)
    print(not is_active)

# EXPECTED OUTPUT:
# True
# False
# False
# True
# True
```

## Output

```
True
False
False
True
True
```

## Timing

- Generation: 4.86s
- Execution: 1.56s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
