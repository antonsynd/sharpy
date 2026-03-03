# Successful Dogfood Run

**Timestamp:** 2026-03-03T02:44:19.996131
**Feature Focus:** enum_usage
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Basic enum arithmetic using .value and .name properties
enum Scale:
    SMALL = 10
    MEDIUM = 20
    LARGE = 30

def main():
    # Double the medium scale value
    result: int = Scale.MEDIUM.value * 2
    print(result)
    
    # Access enum name (PascalCase)
    print(Scale.LARGE.name)
    
    # Access raw value
    print(Scale.SMALL.value)

```

## Output

```
40
Large
10
```

## Timing

- Generation: 31.21s
- Execution: 4.71s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
