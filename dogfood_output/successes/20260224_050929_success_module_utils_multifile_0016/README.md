# Successful Dogfood Run

**Timestamp:** 2026-02-24T05:07:30.091549
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### math_utils.spy

```python
# Math utilities module with functions and constants
# Tests module-level functions, constants, and type aliases

PI: float = 3.14159

def square(x: float) -> float:
    return x * x

def cube(x: float) -> float:
    return x * x * x

def sum_range(n: int) -> int:
    total: int = 0
    i: int = 0
    for i in range(n + 1):
        total += i
    return total

class Calculator:
    _base: int
    def __init__(self, base: int):
        self._base = base
    def multiply(self, x: int) -> int:
        return self._base * x
    def add(self, x: int) -> int:
        return self._base + x
```

### text_utils.spy

```python
# Text processing module
# Tests importing from another module

from math_utils import square, Calculator

def repeat_text(text: str, times: int) -> str:
    result: str = ""
    i: int = 0
    for i in range(times):
        result += text
    return result

def format_number(n: float) -> str:
    return f"Number: {n:.2f}"

class TextFormatter:
    _calc: Calculator
    def __init__(self, multiplier: int):
        self._calc = Calculator(multiplier)
    def process(self, text: str) -> str:
        length: int = len(text)
        multiplied: int = self._calc.multiply(length)
        return f"{text} (length {length} * {multiplied})"
```

### main.spy

```python
# Main entry point - imports and uses both utility modules
from math_utils import square, cube, sum_range, PI
from text_utils import repeat_text, format_number, TextFormatter

def main():
    # Test math utils
    x: float = 4.0
    squared: float = square(x)
    cubed: float = cube(x)
    print(squared)
    print(cubed)
    
    # Test string operations
    repeated: str = repeat_text("ha", 3)
    print(repeated)
    
    # Test sum range
    total: int = sum_range(5)
    print(total)
    
    # Test formatter class
    formatter: TextFormatter = TextFormatter(2)
    result: str = formatter.process("hello")
    print(result)
    
    # Test formatted number
    formatted: str = format_number(PI)
    print(formatted)
    
    # EXPECTED OUTPUT:
    # 16.0
    # 64.0
    # hahaha
    # 15
    # hello (length 5 * 10)
    # Number: 3.14
```

## Timing

- Generation: 103.97s
- Execution: 4.58s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
