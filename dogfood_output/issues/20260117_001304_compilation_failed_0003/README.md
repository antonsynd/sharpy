# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T00:12:49.771153
**Type:** compilation_failed
**Feature Focus:** super_init_call
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Testing super().__init__() call in inheritance

class Animal:
    age: int
    
    def __init__(self, age: int):
        self.age = age

class Dog(Animal):
    name: str
    
    def __init__(self, name: str, age: int):
        super().__init__(age)
        self.name = name

dog = Dog("Buddy", 5)
print(dog.age)
print(dog.name)

# EXPECTED OUTPUT:
# 5
# Buddy
```

## Error

```
Assembly compilation failed:
  error CS5001: Program does not contain a static 'Main' method suitable for an entry point

```

## Timing

- Generation: 4.19s
- Execution: 1.43s
