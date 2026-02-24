# Successful Dogfood Run

**Timestamp:** 2026-02-24T06:50:31.041909
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### vehicles.spy

```python
# Base vehicle module with interface and base class

interface IDrivable:
    def drive(self) -> str: ...

class Vehicle:
    speed: int
    name: str

    def __init__(self, name: str, speed: int):
        self.name = name
        self.speed = speed

    @virtual
    def describe(self) -> str:
        return f"{self.name} moving at {self.speed}"

    @virtual
    def honk(self) -> str:
        return "Beep!"
```

### vehicles_extended.spy

```python
# Extended vehicle types importing from base module

from vehicles import Vehicle, IDrivable

class Car(Vehicle, IDrivable):
    doors: int

    def __init__(self, name: str, speed: int, doors: int):
        super().__init__(name, speed)
        self.doors = doors

    @override
    def describe(self) -> str:
        return f"{self.name} car with {self.doors} doors at {self.speed} speed"

    def drive(self) -> str:
        return f"Driving {self.name}"

class Truck(Vehicle):
    capacity: float

    def __init__(self, name: str, speed: int, capacity: float):
        super().__init__(name, speed)
        self.capacity = capacity

    @override
    def describe(self) -> str:
        base: str = super().describe()
        return f"{base}, capacity: {self.capacity}"
```

### main.spy

```python
# Main entry point - tests cross-module class inheritance

from vehicles import Vehicle, IDrivable
from vehicles_extended import Car, Truck

def main():
    # Test base class
    v: Vehicle = Vehicle("Generic", 30)
    print(v.describe())

    # Test Car class (inherits Vehicle + implements IDrivable)
    c: Car = Car("Sedan", 60, 4)
    print(c.describe())

    # Test interface through polymorphism
    drivable: IDrivable = c
    print(drivable.drive())

    # Test Truck class (inherits Vehicle, adds capacity)
    t: Truck = Truck("Semi", 55, 20.5)
    print(t.describe())

    # EXPECTED OUTPUT:
    # Generic moving at 30
    # Sedan car with 4 doors at 60 speed
    # Driving Sedan
    # Semi moving at 55, capacity: 20.5
```

## Timing

- Generation: 152.75s
- Execution: 4.85s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
