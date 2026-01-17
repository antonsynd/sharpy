# Successful Dogfood Run

**Timestamp:** 2026-01-17T00:44:41.362566
**Feature Focus:** class_inheritance
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Complex class inheritance test with abstract classes, virtual/override methods, and interfaces

interface IMovable:
    def move(self, distance: int) -> None:
        ...
    
    def get_position(self) -> int:
        ...

@abstract
class Vehicle(IMovable):
    position: int
    name: str
    
    def __init__(self, name: str):
        self.name = name
        self.position = 0
    
    @virtual
    def move(self, distance: int) -> None:
        self.position += distance
    
    def get_position(self) -> int:
        return self.position
    
    @abstract
    def describe(self) -> None:
        ...

class Car(Vehicle):
    wheels: int
    
    def __init__(self, name: str, wheels: int):
        super().__init__(name)
        self.wheels = wheels
    
    @override
    def describe(self) -> None:
        print("Car description:")
        print(self.name)
        print(self.wheels)

class Motorcycle(Vehicle):
    has_sidecar: bool
    
    def __init__(self, name: str, has_sidecar: bool):
        super().__init__(name)
        self.has_sidecar = has_sidecar
    
    @override
    def move(self, distance: int) -> None:
        multiplier: int = 1
        if self.has_sidecar:
            multiplier = 1
        else:
            multiplier = 2
        self.position += distance * multiplier
    
    @override
    def describe(self) -> None:
        print("Motorcycle description:")
        print(self.name)

def test_vehicle(v: Vehicle) -> None:
    v.describe()
    v.move(10)
    print(v.get_position())

def main() -> None:
    print("Testing inheritance")
    
    car: Car = Car("Sedan", 4)
    test_vehicle(car)
    
    bike: Motorcycle = Motorcycle("Speedy", False)
    test_vehicle(bike)
    
    bike_with_sidecar: Motorcycle = Motorcycle("Cruiser", True)
    test_vehicle(bike_with_sidecar)
    
    print("All tests complete")

main()

# EXPECTED OUTPUT:
# Testing inheritance
# Car description:
# Sedan
# 4
# 10
# Motorcycle description:
# Speedy
# 20
# Motorcycle description:
# Cruiser
# 10
# All tests complete
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_7be4f963d7e748c4bd3e82013334e61b.exe

=== Running Program ===

Testing inheritance
Car description:
Sedan
4
10
Motorcycle description:
Speedy
20
Motorcycle description:
Cruiser
10
All tests complete
```

## Timing

- Generation: 14.84s
- Execution: 1.73s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
