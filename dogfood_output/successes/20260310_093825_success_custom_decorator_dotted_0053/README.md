# Successful Dogfood Run

**Timestamp:** 2026-03-10T09:36:41.705879
**Feature Focus:** custom_decorator_dotted
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Dotted decorator on function with message argument
# Tests: @system.obsolete applied to a function
@system.obsolete("Use calculate_area() instead")
def get_area_legacy(width: int, height: int) -> int:
    return width * height

def calculate_area(width: int, height: int) -> int:
    return width * height

def main():
    result: int = get_area_legacy(5, 3)
    print(result)

```

## Output

```
15
```

## Timing

- Generation: 92.99s
- Execution: 4.91s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
