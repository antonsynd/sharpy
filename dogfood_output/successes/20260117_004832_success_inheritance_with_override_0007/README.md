# Successful Dogfood Run

**Timestamp:** 2026-01-17T00:48:13.164113
**Feature Focus:** inheritance_with_override
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test inheritance with method overriding

class Animal:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @virtual
    def speak(self) -> int:
        print(0)
        return 0
    
    def get_legs(self) -> int:
        return 4

class Dog(Animal):
    breed: str
    
    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed
    
    @override
    def speak(self) -> int:
        print(1)
        return 1

class Cat(Animal):
    indoor: bool
    
    def __init__(self, name: str, indoor: bool):
        super().__init__(name)
        self.indoor = indoor
    
    @override
    def speak(self) -> int:
        print(2)
        return 2

# Test the classes
dog = Dog("Rex", "Shepherd")
cat = Cat("Whiskers", True)
animal = Animal("Generic")

animal.speak()
dog.speak()
cat.speak()
print(dog.get_legs())
print(cat.get_legs())

# EXPECTED OUTPUT:
# 0
# 1
# 2
# 4
# 4
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_f9cd10d8e4db4975b72219d9b2992d8d.exe

=== Running Program ===

0
1
2
4
4
```

## Timing

- Generation: 5.65s
- Execution: 1.38s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
