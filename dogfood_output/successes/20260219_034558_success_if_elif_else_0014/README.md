# Successful Dogfood Run

**Timestamp:** 2026-02-19T03:42:42.565301
**Feature Focus:** if_elif_else
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
enum Department:
    SALES = 0
    ENGINEERING = 1
    HR = 2

class Employee:
    years: int
    dept: Department
    
    def __init__(self, years: int, dept: Department):
        self.years = years
        self.dept = dept
    
    @virtual
    def bonus(self) -> float:
        b: float = 500.0
        if self.years < 2:
            b = 500.0
        elif self.years < 5:
            b = 1500.0
        else:
            b = 3000.0
        
        if self.dept == Department.SALES:
            b = b * 1.5
        elif self.dept == Department.ENGINEERING:
            b = b * 1.3
        else:
            b = b * 1.1
        return b

class Manager(Employee):
    team: int
    
    def __init__(self, years: int, dept: Department, team: int):
        super().__init__(years, dept)
        self.team = team
    
    @override
    def bonus(self) -> float:
        b: float = super().bonus()
        tb: float = 0.0
        if self.team < 3:
            tb = 1000.0
        elif self.team < 8:
            tb = 2500.0
        else:
            tb = 4000.0
        
        if self.years >= 5 and self.team >= 5:
            return b + tb + 2000.0
        elif self.years >= 3 or self.team >= 10:
            return b + tb + 1000.0
        else:
            return b + tb

def main():
    e1: Employee = Employee(1, Department.HR)
    e2: Employee = Employee(6, Department.SALES)
    m1: Manager = Manager(7, Department.ENGINEERING, 6)
    m2: Manager = Manager(12, Department.SALES, 20)
    m3: Manager = Manager(2, Department.ENGINEERING, 4)
    
    print(e1.bonus())
    print(e2.bonus())
    print(m1.bonus())
    print(m2.bonus())
    print(m3.bonus())

# EXPECTED OUTPUT:
# 550.0
# 4500.0
# 8400.0
# 10500.0
# 4450.0
```

## Output

```
550.0
4500.0
8400.0
10500.0
4450.0
```

## Timing

- Generation: 186.07s
- Execution: 4.39s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
