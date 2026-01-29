# Issue Report: execution_failed

**Timestamp:** 2026-01-29T00:06:27.368009
**Type:** execution_failed
**Feature Focus:** super_init_call
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: super().__init__() with employee hierarchy
# Complexity: medium (inheritance chain, multiple fields, constructor chaining)

class Person:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    def describe(self) -> None:
        print(f"Name: {self.name}")
        print(f"Age: {self.age}")


class Employee(Person):
    employee_id: int
    salary: float

    def __init__(self, name: str, age: int, emp_id: int, salary: float):
        super().__init__(name, age)
        self.employee_id = emp_id
        self.salary = salary

    @override
    def describe(self) -> None:
        super().describe()
        print(f"ID: {self.employee_id}")
        print(f"Salary: {self.salary}")


def main():
    emp = Employee("Alice", 30, 12345, 75000.50)
    emp.describe()
    
    if emp.salary > 70000.0:
        print("High earner")
    else:
        print("Standard earner")

# EXPECTED OUTPUT:
# Name: Alice
# Age: 30
# ID: 12345
# Salary: 75000.5
# High earner
```

## Error

```
Compilation failed:
  Semantic error at line 27, column 5: Cannot override 'describe' because the base class method in 'Person' is not marked @virtual or @abstract. Add @virtual to the method in the base class.

```

## Timing

- Generation: 7.00s
- Execution: 1.18s
