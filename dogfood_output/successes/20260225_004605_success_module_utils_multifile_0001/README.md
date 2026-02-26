# Successful Dogfood Run

**Timestamp:** 2026-02-25T00:37:38.881125
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### validators.spy

```python
# Module providing validation utilities

class ValidationResult:
    is_valid: bool
    message: str

    def __init__(self, valid: bool, msg: str):
        self.is_valid = valid
        self.message = msg

# Base validator class with virtual method
class Validator:
    @virtual
    def validate(self, value: int) -> ValidationResult:
        return ValidationResult(True, "Valid")

class NumberValidator(Validator):
    min_val: int
    max_val: int

    def __init__(self, min_v: int, max_v: int):
        self.min_val = min_v
        self.max_val = max_v

    @override
    def validate(self, value: int) -> ValidationResult:
        if value < self.min_val:
            return ValidationResult(False, "Value too small")
        elif value > self.max_val:
            return ValidationResult(False, "Value too large")
        return ValidationResult(True, "Valid")

def is_positive(x: int) -> bool:
    return x > 0

def clamp_value(x: int, low: int, high: int) -> int:
    if x < low:
        return low
    elif x > high:
        return high
    return x
```

### formatters.spy

```python
# Module providing formatting utilities
from validators import is_positive, ValidationResult

interface IFormattable:
    def format(self) -> str

def format_result(result: ValidationResult) -> str:
    status: str = "PASS" if result.is_valid else "FAIL"
    return f"[{status}] {result.message}"

def format_number(n: int, width: int) -> str:
    num_str: str = str(n)
    pad_count: int = width - len(num_str)
    if is_positive(pad_count):
        padding: str = " " * pad_count
        return padding + num_str
    return num_str

class ValueFormatter(IFormattable):
    value: int

    def __init__(self, v: int):
        self.value = v

    def format(self) -> str:
        return f"Value: {self.value}"
```

### main.spy

```python
# Main entry point - tests module utilities across two modules
from validators import NumberValidator, is_positive, clamp_value
from formatters import format_result, format_number, ValueFormatter

def main():
    # Test 1: Number validation
    validator: NumberValidator = NumberValidator(1, 100)
    result = validator.validate(50)
    print(format_result(result))

    # Test 2: Check helper functions
    x: int = 75
    y: int = -5
    print(is_positive(x))
    print(is_positive(y))

    # Test 3: Clamp and format
    clamped: int = clamp_value(150, 1, 100)
    formatted: str = format_number(clamped, 5)
    print(formatted)

    # Test 4: Interface usage
    formatter: ValueFormatter = ValueFormatter(42)
    print(formatter.format())

    # EXPECTED OUTPUT:
    # [PASS] Valid
    # True
    # False
    #   100
    # Value: 42
```

## Timing

- Generation: 491.42s
- Execution: 4.60s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
