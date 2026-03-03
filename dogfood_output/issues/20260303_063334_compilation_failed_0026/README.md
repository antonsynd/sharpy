# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T06:30:20.659943
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main calculator program
# Imports from multiple modules and demonstrates cross-module usage

from calc_types import Addition, Multiplication, Operation, format_result
from calc_history import CalculatorHistory, HistoryEntry
from calc_extra import Division, Subtraction

def apply_operation(op: Operation, a: float, b: float) -> float:
    return op.execute(a, b)

def main():
    history: CalculatorHistory = CalculatorHistory()
    
    add: Addition = Addition()
    mul: Multiplication = Multiplication()
    div: Division = Division()
    sub: Subtraction = Subtraction()
    
    a: float = 10.0
    b: float = 3.0
    
    result1: float = apply_operation(add, a, b)
    print(format_result(result1))
    history.record("add", a, b, result1)
    
    result2: float = apply_operation(mul, a, b)
    print(format_result(result2))
    history.record("mul", a, b, result2)
    
    result3: float = apply_operation(div, a, b)
    print(format_result(result3))
    history.record("div", a, b, result3)
    
    result4: float = apply_operation(sub, a, b)
    print(format_result(result4))
    history.record("sub", a, b, result4)
    
    history.print_summary()
    
    last: HistoryEntry = history.get_last()
    print(f"Last: {last.operation} {last.operand1} {last.operand2} -> {last.result}")

```

## Error

```
Assembly compilation failed:

error[CS0029]: Cannot implicitly convert type 'CalcTypes.CalcError' to 'System.Exception'
  --> /tmp/tmp0ljvcjw5/calc_history.spy:30:23
    |
 30 |     result3: float = apply_operation(div, a, b)
    |                       ^
    |

error[CS0029]: Cannot implicitly convert type 'CalcTypes.CalcError' to 'System.Exception'
  --> /tmp/tmp0ljvcjw5/calc_extra.spy:14:23
    |
 14 |     add: Addition = Addition()
    |                       ^
    |


```

## Timing

- Generation: 164.25s
- Execution: 5.16s
