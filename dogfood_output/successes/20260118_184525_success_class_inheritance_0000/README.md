# Successful Dogfood Run

**Timestamp:** 2026-01-18T18:45:15.770841
**Feature Focus:** class_inheritance
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test class inheritance with field access and method calls
class Animal:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    def get_age(self) -> int:
        return self.age

class Dog(Animal):
    breed: str

    def __init__(self, name: str, age: int, breed: str):
        super().__init__(name, age)
        self.breed = breed

    def get_years_in_dog_years(self) -> int:
        return self.age * 7

d = Dog("Buddy", 3, "Labrador")
print(d.get_age())
print(d.get_years_in_dog_years())

# EXPECTED OUTPUT:
# 3
# 21
```

## Output

```
3
21
```

## Timing

- Generation: 3.29s
- Execution: 1.34s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
