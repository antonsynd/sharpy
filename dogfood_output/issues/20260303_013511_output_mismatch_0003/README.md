# Issue Report: output_mismatch

**Timestamp:** 2026-03-03T01:32:28.301764
**Type:** output_mismatch
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage

from types import Species, Status, Vitals
from animals import Animal, Dog, Cat
from clinic import PetClinic

def main():
    # Create animals - cross-module classes with inheritance and interfaces
    dog1: Dog = Dog("Rex", 3, "Golden")
    cat1: Cat = Cat("Whiskers", 5, True)
    dog2: Dog = Dog("Max", 15, "Bulldog")  # Old dog, not healthy

    # Test interface methods and inheritance
    print(dog1.speak())
    print(cat1.speak())

    # Test Vitals struct
    vitals: Vitals = dog1.get_vitals()
    print(vitals.temperature)
    print(vitals.heart_rate)

    # Test enum values
    print(dog1.species.value)
    print(cat1.species.name)

    # Test clinic operations with interface types
    clinic: PetClinic = PetClinic("City Vet")
    clinic.check_in(dog1)
    clinic.check_in(cat1)
    clinic.check_in(dog2)

    # Test exam workflow
    clinic.start_exam(dog1)
    healthy1: bool = clinic.complete_exam(dog1)
    clinic.start_exam(cat1)
    healthy2: bool = clinic.complete_exam(cat1)

    # Clinic statistics
    print(clinic.get_average_weight())

    # Count healthy
    print(clinic.count_healthy())

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Woof!
Meow!
101.5
80
1
Feline
17.5
2

```

### Actual
```
Woof!
Meow!
101.5
80
1
Feline
20.333333333333332
2
```

## Timing

- Generation: 110.89s
- Execution: 5.01s
