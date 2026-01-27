# Issue Report: execution_failed

**Timestamp:** 2026-01-27T00:40:06.585067
**Type:** execution_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - cross-module class usage demonstration
from models import Employee, Manager, Developer
from payroll import PayrollProcessor

def main():
    # Create employees of different types
    emp1: Employee = Employee("Alice", 101)
    mgr1: Manager = Manager("Bob", 201, 5)
    dev1: Developer = Developer("Charlie", 301, "Python")

    # Display employee information
    print(emp1.get_info())
    print(mgr1.get_info())
    print(dev1.get_info())

    # Process payroll using cross-module class interaction
    processor: PayrollProcessor = PayrollProcessor()
    processor.process_employee(emp1)
    processor.process_employee(mgr1)
    processor.process_employee(dev1)

    # Show total bonuses
    total: float = processor.get_total()
    print(total)

# EXPECTED OUTPUT:
# Employee Alice (ID: 101)
# Manager Bob (ID: 201, Team: 5)
# Developer Charlie (ID: 301, Lang: Python)
# 4500
```

## Error

```
Compilation failed:
  Semantic error at line 19, column 32: Cannot pass argument of type 'Manager' to parameter of type 'Employee'
  Semantic error at line 20, column 32: Cannot pass argument of type 'Developer' to parameter of type 'Employee'

```

## Timing

- Generation: 12.53s
- Execution: 0.90s
