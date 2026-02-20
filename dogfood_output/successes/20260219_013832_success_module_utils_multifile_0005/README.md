# Successful Dogfood Run

**Timestamp:** 2026-02-19T01:36:29.106038
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (2 files)

## Source Files

### utils.spy

```python
# Utility module providing mathematical and string utilities

def square(x: int) -> int:
    return x * x

def cube(x: int) -> int:
    return x * x * x

def is_even(n: int) -> bool:
    return n % 2 == 0

class MathUtils:
    multiplier: int
    
    def __init__(self, multiplier: int):
        self.multiplier = multiplier
    
    def scale(self, value: int) -> int:
        return value * self.multiplier
    
    def apply(self, func: (int) -> int, value: int) -> int:
        return func(value) * self.multiplier

def format_result(name: str, value: int) -> str:
    return f"{name}: {value}"
```

### main.spy

```python
# Main entry point - tests utility module functions and classes

from utils import square, is_even, MathUtils, format_result

def main():
    # Test basic utility functions
    num: int = 5
    squared: int = square(num)
    print(squared)
    
    # Test predicate function
    check: bool = is_even(squared)
    print(check)
    
    # Test utility class with higher-order function
    utils: MathUtils = MathUtils(3)
    scaled: int = utils.scale(10)
    print(scaled)
    
    # Apply a function through the class
    result: int = utils.apply(square, 4)
    print(result)
    
    # Format and display
    message: str = format_result("Final", result)
    print(message)

# EXPECTED OUTPUT:
# 25
# False
# 30
# 48
# Final: 48
```

## Timing

- Generation: 110.95s
- Execution: 4.36s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
