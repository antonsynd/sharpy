# Successful Dogfood Run

**Timestamp:** 2026-02-19T01:53:25.659445
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### vehicle_base.spy

```python
# Base module for vehicle system
enum VehicleStatus:
    OPERATIONAL = 1
    MAINTENANCE = 2
    RETIRED = 3

@abstract
class Vehicle:
    model: str
    year: int
    status: VehicleStatus

    def __init__(self, model: str, year: int):
        self.model = model
        self.year = year
        self.status = VehicleStatus.OPERATIONAL

    @abstract
    def get_type(self) -> str:
        pass

    @abstract
    def describe(self) -> str:
        pass

    @abstract
    def perform_maintenance(self) -> str:
        pass

    def get_status_name(self) -> str:
        if self.status == VehicleStatus.OPERATIONAL:
            return "Operational"
        elif self.status == VehicleStatus.MAINTENANCE:
            return "Maintenance"
        else:
            return "Retired"
```

### vehicle_types.spy

```python
# Concrete vehicle types
from vehicle_base import Vehicle, VehicleStatus

class MaintenanceRecord:
    vehicle_model: str
    cost: float

    def __init__(self, model: str, cost: float):
        self.vehicle_model = model
        self.cost = cost

    def __str__(self) -> str:
        return f"Maintenance for {self.vehicle_model}: ${self.cost:.2f}"

class Car(Vehicle):
    doors: int

    def __init__(self, model: str, year: int, doors: int):
        super().__init__(model, year)
        self.doors = doors

    @override
    def get_type(self) -> str:
        return "Car"

    @override
    def describe(self) -> str:
        base: str = f"{self.model} ({self.year})"
        return f"{base} - {self.doors} doors"

    @override
    def perform_maintenance(self) -> str:
        self.status = VehicleStatus.MAINTENANCE
        return f"Car maintenance completed for {self.model}"

class Motorcycle(Vehicle):
    engine_cc: int

    def __init__(self, model: str, year: int, cc: int):
        super().__init__(model, year)
        self.engine_cc = cc

    @override
    def get_type(self) -> str:
        return "Motorcycle"

    @override
    def describe(self) -> str:
        base: str = f"{self.model} ({self.year})"
        return f"{base} - {self.engine_cc}cc"

    @override
    def perform_maintenance(self) -> str:
        self.status = VehicleStatus.MAINTENANCE
        return f"Motorcycle service done for {self.model}"
```

### main.spy

```python
# Main entry point
from vehicle_base import Vehicle, VehicleStatus
from vehicle_types import Car, Motorcycle, MaintenanceRecord

def process_vehicle(v: Vehicle) -> str:
    type_name: str = v.get_type()
    description: str = v.describe()
    return f"Processing {type_name}: {description}"

def service_fleet(vehicles: list[Vehicle]):
    for v in vehicles:
        result: str = v.perform_maintenance()
        print(result)

def main():
    car: Car = Car("Tesla Model 3", 2023, 4)
    bike: Motorcycle = Motorcycle("Harley Davidson", 2022, 1200)
    
    print(process_vehicle(car))
    print(process_vehicle(bike))
    print(car.get_status_name())
    print(bike.get_status_name())
    
    fleet: list[Vehicle] = [car, bike]
    service_fleet(fleet)
    
    print(car.get_status_name())
    
    record: MaintenanceRecord = MaintenanceRecord("Tesla Model 3", 450.75)
    print(record)

# EXPECTED OUTPUT:
# Processing Car: Tesla Model 3 (2023) - 4 doors
# Processing Motorcycle: Harley Davidson (2022) - 1200cc
# Operational
# Operational
# Car maintenance completed for Tesla Model 3
# Motorcycle service done for Harley Davidson
# Maintenance
# Maintenance for Tesla Model 3: $450.75
```

## Timing

- Generation: 387.34s
- Execution: 4.62s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
