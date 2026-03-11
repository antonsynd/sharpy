# Successful Dogfood Run

**Timestamp:** 2026-03-10T02:36:56.094302
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### calculator.spy

```python
def add(a: int, b: int) -> int:
    return a + b

def subtract(a: int, b: int) -> int:
    return a - b

def multiply(a: int, b: int) -> int:
    return a * b

def divide(a: float, b: float) -> float:
    return a / b

```

### formatter.spy

```python
def format_number(n: float) -> str:
    return f"Result: {n:.2f}"

def format_int(n: int) -> str:
    return f"Value: {n}"

```

### main.spy

```python
from calculator import add, subtract, multiply, divide
from formatter import format_number, format_int

def main():
    x: int = 10
    y: int = 3
    
    sum_result: int = add(x, y)
    diff_result: int = subtract(x, y)
    prod_result: int = multiply(x, y)
    div_result: float = divide(float(x), float(y))
    
    print(format_int(sum_result))
    print(format_int(diff_result))
    print(format_int(prod_result))
    print(format_number(div_result))

```

## Timing

- Generation: 244.19s
- Execution: 4.80s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
