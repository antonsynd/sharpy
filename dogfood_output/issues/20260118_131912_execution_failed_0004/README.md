# Issue Report: execution_failed

**Timestamp:** 2026-01-18T13:18:59.911109
**Type:** execution_failed
**Feature Focus:** virtual_override
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test virtual and override methods with a calculator hierarchy

@abstract
class Calculator:
    base_value: int

    def __init__(self, value: int):
        self.base_value = value

    @virtual
    def calculate(self, operand: int) -> int:
        return self.base_value + operand

    @virtual
    def get_operation_name(self) -> str:
        return "addition"

class Multiplier(Calculator):
    factor: int

    def __init__(self, value: int, mult_factor: int):
        super().__init__(value)
        self.factor = mult_factor

    @override
    def calculate(self, operand: int) -> int:
        return self.base_value * operand * self.factor

    @override
    def get_operation_name(self) -> str:
        return "multiplication"

class Subtractor(Calculator):
    def __init__(self, value: int):
        super().__init__(value)

    @override
    def calculate(self, operand: int) -> int:
        return self.base_value - operand

calc1: Calculator = Calculator(10)
print(calc1.calculate(5))
print(calc1.get_operation_name())

calc2: Calculator = Multiplier(3, 2)
print(calc2.calculate(4))
print(calc2.get_operation_name())

calc3: Calculator = Subtractor(20)
print(calc3.calculate(7))

# EXPECTED OUTPUT:
# 15
# addition
# 24
# multiplication
# 13
```

## Error

```
Compilation failed:
  Semantic error at line 41, column 21: Cannot instantiate abstract class 'Calculator'

```

## Timing

- Generation: 4.83s
- Execution: 0.84s
