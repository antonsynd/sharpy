# Successful Dogfood Run

**Timestamp:** 2026-02-26T07:22:17.444259
**Feature Focus:** property_inheritance
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Property inheritance with validation and computed properties
# Tests: inheritance of function-style properties, validation, getter override

class Person:
    _age: int
    
    def __init__(self, age: int):
        self._age = age
    
    @virtual
    property get age(self) -> int:
        return self._age

class ValidatedPerson(Person):
    _name: str
    
    def __init__(self, name: str, age: int):
        super().__init__(age)
        self._name = name
    
    @override
    property get age(self) -> int:
        # Validation: ensure age is reasonable
        if self._age < 0:
            return 0
        elif self._age > 150:
            return 150
        return self._age
    
    property get age_category(self) -> str:
        a: int = self.age  # Uses overridden getter with validation
        if a < 13:
            return "child"
        elif a < 20:
            return "teen"
        elif a < 65:
            return "adult"
        return "senior"

class Employee(ValidatedPerson):
    _salary: float
    
    def __init__(self, name: str, age: int, salary: float):
        super().__init__(name, age)
        self._salary = salary
    
    @override
    property get age(self) -> int:
        # Chain to parent validation, add employee-specific logic
        base_age: int = super().age
        if base_age < 16:
            return 16  # Minimum working age
        return base_age
    
    property get salary(self) -> float:
        return self._salary
    
    property get is_retirement_ready(self) -> bool:
        return self.age >= 65

def main():
    p1 = Person(25)
    p2 = ValidatedPerson("Alice", 300)  # Should be capped at 150
    p3 = Employee("Bob", 12, 50000.0)     # Should be adjusted to 16
    e1 = Employee("Carol", 45, 75000.0)
    
    print(p1.age)
    print(p2.age_category)
    print(p3.age)
    print(e1.age_category)
    print(e1.is_retirement_ready)
```

## Output

```
25
senior
16
adult
False
```

## Timing

- Generation: 64.68s
- Execution: 4.42s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
