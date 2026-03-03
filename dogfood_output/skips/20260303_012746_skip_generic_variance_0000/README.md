# Skipped Dogfood Run

**Timestamp:** 2026-03-03T01:23:45.469493
**Skip Reason:** Repeated identical compiler error (likely compiler bug): Compilation errors:

error[SPY0013]: Indentation must be multiple of 4 spaces (found 1)
  --> /tmp/tmpi2f9w64a/dogfood_test.spy:50:1
    |
 50 | ) -> None:
    | ^
    |


**Feature Focus:** generic_variance
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test generic variance with covariant and contravariant type parameters
# Demonstrates out T (covariant) and in T (contravariant) in interfaces and delegates

interface IProducer[out T]:
    def produce(self) -> T: ...

interface IConsumer[in T]:
    def consume(self, item: T) -> None: ...

delegate Transformer[in T, out R](value: T) -> R

class Animal:
    property name: str
    
    def __init__(self, n: str):
        self.name = n

class Dog(Animal):
    breed: str
    
    def __init__(self, n: str, b: str):
        super().__init__(n)
        self.breed = b

class DogProducer:
    dog_name: str
    dog_breed: str
    
    def __init__(self, n: str, b: str):
        self.dog_name = n
        self.dog_breed = b
    
    def produce(self) -> Dog:
        return Dog(self.dog_name, self.dog_breed)

class AnimalPrinter:
    def consume(self, item: Animal) -> None:
        print(item.name)

def make_name_animal(a: Animal) -> str:
    return a.name

def make_name_dog(d: Dog) -> str:
    return f"{d.name} ({d.breed})"

def process_items(
    producer: IProducer[Animal],
    consumer: IConsumer[Animal],
    transform: Transformer[Animal, str]
) -> None:
    item = producer.produce()
    consumer.consume(item)
    result = transform(item)
    print(result)

def main():
    # Covariance: DogProducer (IProducer[Dog]) can be used as IProducer[Animal]
    dog_maker = DogProducer("Rex", "Labrador")
    
    # Contravariance: AnimalPrinter (IConsumer[Animal]) can be used as IConsumer[Dog]
    printer = AnimalPrinter()
    
    # Test covariance: DogProducer as IProducer[Animal]
    animal_producer: IProducer[Animal] = dog_maker
    dog = animal_producer.produce()
    print(dog.name)
    
    # Test contravariance: AnimalPrinter as IConsumer[Dog]
    dog_consumer: IConsumer[Dog] = printer
    
    # Test contravariant delegate: (Dog) -> str for (Animal) -> str parameter
    xform: Transformer[Animal, str] = make_name_dog
    
    # Process with the variance-substituted components
    process_items(animal_producer, dog_consumer, xform)

```

## Timing

- Generation: 230.52s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
