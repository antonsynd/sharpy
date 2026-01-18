# Successful Dogfood Run

**Timestamp:** 2026-01-18T18:34:42.410425
**Feature Focus:** class_instance_methods
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test instance methods with basic calculator class
class Calculator:
    current_value: int

    def __init__(self, start: int):
        self.current_value = start

    def add(self, n: int) -> int:
        self.current_value = self.current_value + n
        return self.current_value

    def subtract(self, n: int) -> int:
        self.current_value = self.current_value - n
        return self.current_value

    def get_value(self) -> int:
        return self.current_value

calc = Calculator(20)
print(calc.get_value())
print(calc.add(15))
print(calc.subtract(10))
print(calc.get_value())

# EXPECTED OUTPUT:
# 20
# 35
# 25
# 25
```

## Output

```
20
35
25
25
```

## Timing

- Generation: 4.72s
- Execution: 1.75s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
