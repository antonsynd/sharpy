# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T07:29:23.030807
**Type:** output_mismatch
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from both modules and coordinates them

from models import Employee, Manager, Developer, get_employee_summary
from reporting import generate_report, find_by_role

def main():
    # Create employee objects using imported classes
    alice: Manager = Manager("Alice", 100000.0, 5)
    bob: Developer = Developer("Bob", 80000.0, "Python")
    carol: Developer = Developer("Carol", 75000.0, "C#")
    
    # Create list of employees
    employees: list[Employee] = [alice, bob, carol]
    
    # Use imported function to print summary
    print(get_employee_summary(alice))
    
    # Generate full report using imported function
    generate_report(employees)
    
    # Find developers using imported function
    devs: list[Employee] = find_by_role(employees, "Developer")
    print(f"Developer count: {len(devs)}")

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Alice (Manager)
Total Salary: 255000.0
Total Bonus: 34500.0
Manager Count: 1
Developer count: 2

```

### Actual
```
Alice (Manager)
Total Salary: 255000.0
Total Bonus: 25900.0
Manager Count: 1
Developer count: 2
```

## Timing

- Generation: 155.27s
- Execution: 5.46s
