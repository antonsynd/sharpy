# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T07:09:59.874267
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
from utils import Calculator, format_result, MODULE_VERSION

def main():
    # Print version info
    print(f"Module version: {MODULE_VERSION}")
    
    # Create calculator
    calc: Calculator = Calculator("Basic")
    
    # Test display name method
    print(calc.get_display_name())
    
    # Add operation
    sum_result: float = calc.add(10.5, 25.3)
    print(f"Sum: {format_result(sum_result, 2)}")
    
    # Multiply operation
    product: float = calc.multiply(4.0, 5.5)
    print(f"Product: {format_result(product, 2)}")
    
    # Power operation
    power_val: float = calc.power(2.0, 8.0)
    print(f"Power: {format_result(power_val, 0)}")
    
    # Access static field
    print(f"PI value: {format_result(Calculator.PI, 4)}")
    
    # Loop with calculations
    print("Squares:")
    i: int = 1
    while i <= 5:
        sq: float = calc.multiply(float(i), float(i))
        print(format_result(sq, 1))
        i = i + 1
    
    print("Done")

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Utils.Calculator' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Utils.Calculator' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpi5gljqjp/utils.spy:15:63
    |
 15 |     print(f"Sum: {format_result(sum_result, 2)}")
    |                                                  ^
    |

error[CS1061]: 'Utils.Calculator' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Utils.Calculator' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpi5gljqjp/utils.spy:12:18
    |
 12 |     
    |     ^
    |


```

## Timing

- Generation: 340.45s
- Execution: 5.05s
