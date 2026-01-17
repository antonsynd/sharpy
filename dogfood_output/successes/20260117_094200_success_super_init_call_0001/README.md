# Successful Dogfood Run

**Timestamp:** 2026-01-17T09:41:38.924501
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

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_579bb947becb4086829e779630f0ce0c.exe

=== Running Program ===

5
Buddy
```

## Timing

- Generation: 6.86s
- Execution: 1.32s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
