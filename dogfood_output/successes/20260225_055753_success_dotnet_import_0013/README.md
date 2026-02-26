# Successful Dogfood Run

**Timestamp:** 2026-02-25T05:56:02.842339
**Feature Focus:** dotnet_import
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
from System import Math

def main():
    # Use Math.Abs for absolute value
    result1: int = Math.Abs(-42)
    print(result1)
    
    # Use Math.Max for finding maximum
    result2: int = Math.Max(10, 25)
    print(result2)
    
    # Use Math.Min for finding minimum
    result3: int = Math.Min(7, 3)
    print(result3)

# EXPECTED OUTPUT:
# 42
# 25
# 3
```

## Output

```
42
25
3
```

## Timing

- Generation: 99.90s
- Execution: 4.58s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
