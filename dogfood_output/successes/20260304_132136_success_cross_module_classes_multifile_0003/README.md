# Successful Dogfood Run

**Timestamp:** 2026-03-04T13:19:44.881400
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### vehicles.spy

```python
class Vehicle:
    brand: str
    
    def __init__(self, brand: str):
        self.brand = brand
    
    @virtual
    def describe(self) -> str:
        return "Vehicle: " + self.brand
    
    @virtual
    def move(self) -> str:
        return "Moving..."

```

### cars.spy

```python
from vehicles import Vehicle

class Car(Vehicle):
    wheels: int
    
    def __init__(self, brand: str, wheels: int):
        super().__init__(brand)
        self.wheels = wheels
    
    @override
    def describe(self) -> str:
        return "Car: " + self.brand + " with " + str(self.wheels) + " wheels"
    
    @override
    def move(self) -> str:
        return "Driving on " + str(self.wheels) + " wheels"

```

### main.spy

```python
from vehicles import Vehicle
from cars import Car

def process_vehicle(v: Vehicle) -> str:
    desc: str = v.describe()
    mov: str = v.move()
    return desc + " - " + mov

def main():
    v = Vehicle("Generic")
    c = Car("Toyota", 4)
    
    print(v.describe())
    print(c.describe())
    print(process_vehicle(c))

```

## Timing

- Generation: 95.54s
- Execution: 4.80s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
