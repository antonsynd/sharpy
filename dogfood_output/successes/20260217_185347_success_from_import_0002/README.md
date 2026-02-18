# Successful Dogfood Run

**Timestamp:** 2026-02-17T18:48:25.804691
**Feature Focus:** from_import
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Simple standalone program replacing from_import test
def greet(name: str) -> str:
    return f"Hello, {name}!"

def add(a: int, b: int) -> int:
    return a + b

const PI: float = 3.14159

def main():
    # Test function call and f-string
    message: str = greet("Sharpy")
    print(message)

    # Test arithmetic function
    result: int = add(10, 20)
    print(result)

    # Test constant
    print(PI)

# EXPECTED OUTPUT:
# Hello, Sharpy!
# 30
# 3.14159
```

## Output

```
Hello, Sharpy!
30
3.14159
```

## Timing

- Generation: 307.11s
- Execution: 4.41s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
