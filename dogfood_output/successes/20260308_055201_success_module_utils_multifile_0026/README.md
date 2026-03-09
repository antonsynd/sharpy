# Successful Dogfood Run

**Timestamp:** 2026-03-08T05:44:04.569762
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### numeric_ops.spy

```python
class Calculator:
    _value: float
    
    def __init__(self, value: float):
        self._value = value
    
    @virtual
    def compute(self) -> float:
        return self._value

def double_value(x: float) -> float:
    return x * 2.0

def triple_value(x: float) -> float:
    return x * 3.0

```

### advanced_calc.spy

```python
from numeric_ops import Calculator

class DoublingCalculator(Calculator):
    @override
    def compute(self) -> float:
        base: float = super().compute()
        return base * 2.0

class SquaringCalculator(Calculator):
    @override
    def compute(self) -> float:
        base: float = super().compute()
        return base * base

```

### main.spy

```python
from numeric_ops import Calculator, double_value, triple_value
from advanced_calc import DoublingCalculator, SquaringCalculator

def main():
    base = Calculator(5.0)
    doubling = DoublingCalculator(5.0)
    squaring = SquaringCalculator(5.0)
    
    print(base.compute())
    print(doubling.compute())
    print(squaring.compute())
    print(double_value(10.0))
    print(triple_value(10.0))

```

## Timing

- Generation: 449.73s
- Execution: 5.19s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
