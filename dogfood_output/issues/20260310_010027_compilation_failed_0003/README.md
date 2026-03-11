# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T00:58:45.953048
**Type:** compilation_failed
**Feature Focus:** virtual_override
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex virtual/override hierarchy with abstract classes and multiple inheritance levels

@abstract
class Animal:
    _name: str
    
    def __init__(self, name: str):
        self._name = name
    
    @abstract
    def speak(self) -> str:
        ...
    
    @abstract
    def get_species(self) -> str:
        ...
    
    @virtual
    def describe(self) -> str:
        return f"A {self.get_species()} named {self._name}"
    
    @virtual
    def calculate_score(self, base: int) -> int:
        return base + len(self._name)

class Mammal(Animal):
    @override
    def get_species(self) -> str:
        return "Unknown Mammal"
    
    @virtual
    def is_domestic(self) -> bool:
        return False

class Dog(Mammal):
    _breed: str
    
    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self._breed = breed
    
    @override
    def speak(self) -> str:
        return "Woof!"
    
    @override
    def get_species(self) -> str:
        return f"Dog ({self._breed})"
    
    @override
    def is_domestic(self) -> bool:
        return True
    
    @override
    def calculate_score(self, base: int) -> int:
        score = super().calculate_score(base)
        if self._breed == "Golden Retriever":
            score += 10
        return score

class Cat(Mammal):
    _lives: int
    
    def __init__(self, name: str, lives: int):
        super().__init__(name)
        self._lives = lives
    
    @override
    def speak(self) -> str:
        return "Meow!"
    
    @override
    def get_species(self) -> str:
        return "Cat"
    
    @override
    def describe(self) -> str:
        base = super().describe()
        return f"{base} with {self._lives} lives"

def make_animals_speak(animals: list[Animal]) -> None:
    for animal in animals:
        msg = animal.speak()
        desc = animal.describe()
        print(f"{msg} - {desc}")

def main():
    dog = Dog("Buddy", "Golden Retriever")
    cat = Cat("Whiskers", 9)
    
    animals: list[Animal] = [dog, cat]
    make_animals_speak(animals)
    
    print(dog.is_domestic())
    print(cat.is_domestic())
    
    dog_score = dog.calculate_score(50)
    cat_score = cat.calculate_score(50)
    print(dog_score)
    print(cat_score)

```

## Error

```
Assembly compilation failed:

error[CS0534]: 'DogfoodTest.Mammal' does not implement inherited abstract member 'DogfoodTest.Animal.Speak()'
  --> /tmp/tmpvzoeoho1/dogfood_test.spy:12:18
    |
 12 |         ...
    |            ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpvzoeoho1/dogfood_test.cs

```

## Timing

- Generation: 86.35s
- Execution: 4.88s
