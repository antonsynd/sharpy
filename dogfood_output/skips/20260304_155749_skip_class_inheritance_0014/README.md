# Skipped Dogfood Run

**Timestamp:** 2026-03-04T15:53:17.183511
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0102]: Expected newline, got Identifier
  --> /tmp/tmpf5xkrxu2/dogfood_test.spy:6:13
    |
  6 |     @static species: str = "Animal"
    |             ^^^^^^^
    |

error[SPY0102]: Expected newline, got Identifier
  --> /tmp/tmpf5xkrxu2/dogfood_test.spy:7:13
    |
  7 |     @static instance_count: int = 0
    |             ^^^^^^^^^^^^^^
    |

error[SPY0102]: Expected newline, got Identifier
  --> /tmp/tmpf5xkrxu2/dogfood_test.spy:23:13
    |
 23 |     @static species: str = "Canine"
    |             ^^^^^^^
    |


**Feature Focus:** class_inheritance
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Class inheritance test - medium complexity
# Tests inheritance, virtual/override, super(), static fields, abstract classes

# Base class with virtual methods and static fields
class Animal:
    @static species: str = "Animal"
    @static instance_count: int = 0

    def __init__(self, name: str):
        self.name = name
        Animal.instance_count += 1

    @virtual
    def speak(self) -> str:
        return f"{self.name} makes a sound"

    @virtual
    def describe(self) -> str:
        return f"{self.name} is an animal"

# Derived class with override and super() calls
class Dog(Animal):
    @static species: str = "Canine"

    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed

    @override
    def speak(self) -> str:
        return f"{self.name} barks"

    @override
    def describe(self) -> str:
        return f"{self.name} is a {self.breed} dog"

# Another derived class
class Cat(Animal):
    def __init__(self, name: str, color: str):
        super().__init__(name)
        self.color = color

    @override
    def speak(self) -> str:
        return f"{self.name} meows"

    def get_color(self) -> str:
        return self.color

# Interface definition
interface IMovable:
    def move(self) -> str

# Class implementing interface
class Bird(Animal, IMovable):
    def __init__(self, name: str, wingspan: float):
        super().__init__(name)
        self.wingspan = wingspan

    @override
    def speak(self) -> str:
        return f"{self.name} chirps"

    def move(self) -> str:
        return f"{self.name} flies"

# Abstract class
@abstract
class Shape:
    @abstract
    def area(self) -> float

    @abstract
    def perimeter(self) -> float

# Concrete implementation of abstract class
class Rectangle(Shape):
    def __init__(self, width: float, height: float):
        self.width = width
        self.height = height

    @override
    def area(self) -> float:
        return self.width * self.height

    @override
    def perimeter(self) -> float:
        return 2.0 * (self.width + self.height)

# Properties demonstration
class Person:
    def __init__(self, first_name: str, last_name: str):
        self.first_name = first_name
        self.last_name = last_name

    # Read-only computed property
    property get full_name(self) -> str:
        return f"{self.first_name} {self.last_name}"

def main():
    # Test basic inheritance
    dog = Dog("Buddy", "Golden Retriever")
    print(dog.name)
    print(dog.breed)
    print(dog.speak())
    print(dog.describe())
    print(Animal.instance_count)
    print("---")

    # Test another derived class
    cat = Cat("Whiskers", "Orange")
    print(cat.name)
    print(cat.get_color())
    print(cat.speak())
    print("---")

    # Test polymorphism through base type - using manual iteration
    animals: list[Animal] = []
    animals.append(Animal("Generic"))
    animals.append(Dog("Rex", "German Shepherd"))
    animals.append(Cat("Mittens", "Black"))
    animals.append(Bird("Tweety", 15.0))

    i = 0
    while i < len(animals):
        animal = animals[i]
        print(animal.name)
        i += 1
    print("---")

    # Test instance count static field
    print(Animal.instance_count)

    # Test interface implementation
    bird = Bird("Eagle", 120.0)
    print(bird.wingspan)
    print(bird.move())
    print("---")

    # Test abstract class implementation
    rect = Rectangle(5.0, 3.0)
    print(rect.area())
    print(rect.perimeter())
    print("---")

    # Test properties
    person = Person("John", "Doe")
    print(person.first_name)
    print(person.last_name)
    print(person.full_name)
    print("---")

    # Test static field access
    print(Dog.species)
    print(Animal.species)

    # Test polymorphic behavior through manual iteration
    j = 0
    while j < len(animals):
        animal = animals[j]
        print(animal.speak())
        print(animal.describe())
        print("---")
        j += 1

```

## Timing

- Generation: 255.89s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
