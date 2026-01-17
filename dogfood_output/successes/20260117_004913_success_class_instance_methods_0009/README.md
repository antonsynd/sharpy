# Successful Dogfood Run

**Timestamp:** 2026-01-17T00:48:57.481104
**Feature Focus:** class_instance_methods
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test class instance methods with simple arithmetic
class Calculator:
    result: int

    def __init__(self, initial: int):
        self.result = initial

    def add(self, x: int) -> int:
        self.result += x
        return self.result

    def get_result(self) -> int:
        return self.result

calc = Calculator(5)
print(calc.get_result())
print(calc.add(7))
print(calc.get_result())

# EXPECTED OUTPUT:
# 5
# 12
# 12
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_99885b65a494488181ca979c0a93f89d.exe

=== Running Program ===

5
12
12
```

## Timing

- Generation: 4.43s
- Execution: 1.35s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
