# Issue Report: execution_failed

**Timestamp:** 2026-01-26T22:09:48.818959
**Type:** execution_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - tests module imports with classes and functions
from utilities import Calculator, double, triple, StringFormatter

def main():
    # Test standalone function imports
    x: int = 5
    doubled: int = double(x)
    print(doubled)

    tripled: int = triple(x)
    print(tripled)

    # Test class import and instantiation
    calc = Calculator(2)
    sum_result: int = calc.add(10, 20)
    print(sum_result)

    product: int = calc.multiply(4, 7)
    formatted: str = calc.format_result(product)
    print(formatted)

    # Test another class import
    formatter = StringFormatter("Status")
    message: str = formatter.format("Module system working")
    print(message)

# EXPECTED OUTPUT:
# 10
# 15
# 30
# Result: 28 (precision: 2)
# Status: Module system working
```

## Error

```
Compilation failed:
  Semantic error at line 7, column 5: Cannot assign type 'double' to variable of type 'int'

```

## Timing

- Generation: 8.45s
- Execution: 0.89s
