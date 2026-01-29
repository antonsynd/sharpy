# Successful Dogfood Run

**Timestamp:** 2026-01-29T00:05:28.281026
**Feature Focus:** inheritance_with_override
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: inheritance with virtual/override methods - Employee hierarchy with salary calculation

@abstract
class Employee:
    name: str
    base_salary: float

    def __init__(self, name: str, salary: float):
        self.name = name
        self.base_salary = salary

    @virtual
    def calculate_total_salary(self) -> float:
        return self.base_salary

    @virtual
    def get_employee_type(self) -> str:
        return "Employee"

class Manager(Employee):
    bonus_percentage: float

    def __init__(self, name: str, salary: float, bonus: float):
        super().__init__(name, salary)
        self.bonus_percentage = bonus

    @override
    def calculate_total_salary(self) -> float:
        return self.base_salary + (self.base_salary * self.bonus_percentage)

    @override
    def get_employee_type(self) -> str:
        return "Manager"

class Developer(Employee):
    overtime_hours: int
    hourly_rate: float

    def __init__(self, name: str, salary: float, hours: int, rate: float):
        super().__init__(name, salary)
        self.overtime_hours = hours
        self.hourly_rate = rate

    @override
    def calculate_total_salary(self) -> float:
        overtime_pay: float = self.overtime_hours * self.hourly_rate
        return self.base_salary + overtime_pay

    @override
    def get_employee_type(self) -> str:
        return "Developer"

def main():
    mgr = Manager("Alice", 5000.0, 0.2)
    dev = Developer("Bob", 4000.0, 10, 50.0)

    print(mgr.get_employee_type())
    print(mgr.calculate_total_salary())
    print(dev.get_employee_type())
    print(dev.calculate_total_salary())

# EXPECTED OUTPUT:
# Manager
# 6000
# Developer
# 4500
```

## Output

```
Manager
6000
Developer
4500
```

## Timing

- Generation: 10.07s
- Execution: 1.53s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
