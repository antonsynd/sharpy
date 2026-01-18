# Successful Dogfood Run

**Timestamp:** 2026-01-18T18:46:03.778611
**Feature Focus:** super_init_call
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test super().__init__() with simple field initialization

class Animal:
    name: str
    
    def __init__(self, name: str):
        self.name = name

class Dog(Animal):
    age: int
    
    def __init__(self, name: str, age: int):
        super().__init__(name)
        self.age = age
    
    def get_age(self) -> int:
        return self.age

d = Dog("Buddy", 3)
print(d.get_age())

# EXPECTED OUTPUT:
# 3
```

## Output

```
3
```

## Timing

- Generation: 2.57s
- Execution: 1.30s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
