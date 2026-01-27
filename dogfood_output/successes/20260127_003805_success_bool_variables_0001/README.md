# Successful Dogfood Run

**Timestamp:** 2026-01-27T00:37:53.837718
**Feature Focus:** bool_variables
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test boolean variables with logical operations and comparisons

is_active: bool = True
is_locked: bool = False
has_access: bool = True

def main():
    print(is_active)
    print(is_locked)
    print(is_active and has_access)
    print(is_active or is_locked)
    print(not is_locked)

# EXPECTED OUTPUT:
# True
# False
# True
# True
# True
```

## Output

```
True
False
True
True
True
```

## Timing

- Generation: 3.94s
- Execution: 1.35s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
