# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T00:12:32.209919
**Type:** compilation_failed
**Feature Focus:** inheritance_with_override
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Testing: inheritance with @override decorator
# Medium complexity: base class with virtual method, child overrides

class Animal:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @virtual
    def speak(self) -> str:
        return "..."
    
    def describe(self) -> None:
        print(self.name)
        print(self.speak())

class Dog(Animal):
    breed: str
    
    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed
    
    @override
    def speak(self) -> str:
        return "Woof!"
    
    def show_breed(self) -> None:
        print(self.breed)

class Cat(Animal):
    def __init__(self, name: str):
        super().__init__(name)
    
    @override
    def speak(self) -> str:
        return "Meow!"

dog = Dog("Buddy", "Labrador")
cat = Cat("Whiskers")

print("Dog:")
dog.describe()
dog.show_breed()

print("Cat:")
cat.describe()

# EXPECTED OUTPUT:
# Dog:
# Buddy
# Woof!
# Labrador
# Cat:
# Whiskers
# Meow!
```

## Error

```
Assembly compilation failed:
  error CS5001: Program does not contain a static 'Main' method suitable for an entry point

```

## Timing

- Generation: 7.04s
- Execution: 1.38s
