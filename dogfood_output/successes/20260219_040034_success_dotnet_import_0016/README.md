# Successful Dogfood Run

**Timestamp:** 2026-02-19T03:59:29.335615
**Feature Focus:** dotnet_import
**Complexity:** simple
**Backend:** copilot

## Generated Sharpy Code

```python
from System import Math

def main():
    x = Math.Abs(-42)
    print(x)
    y = Math.Pow(2.0, 3.0)
    print(y)

# EXPECTED OUTPUT:
# 42
# 8.0
```

## Output

```
42
8.0
```

## Timing

- Generation: 34.88s
- Execution: 4.55s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
