# Successful Dogfood Run

**Timestamp:** 2026-02-25T06:26:55.579892
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### vehicles.spy

```python
# Vehicle types and interfaces

interface IDrivable:
    def start_engine(self) -> str: ...
    def stop_engine(self) -> str: ...

interface ILocatable:
    def get_location(self) -> str: ...

class Vehicle:
    id: int
    brand: str

    def __init__(self, id: int, brand: str):
        self.id = id
        self.brand = brand

    @virtual
    def get_location(self) -> str:
        return "Depot"

class ElectricCar(Vehicle, IDrivable, ILocatable):
    battery_level: int

    def __init__(self, id: int, brand: str, battery: int):
        super().__init__(id, brand)
        self.battery_level = battery

    @override
    def start_engine(self) -> str:
        return "Electric motor silent start"

    @override
    def stop_engine(self) -> str:
        return "Electric motor stopped"

    @override
    def get_location(self) -> str:
        return "Charging Station"

class GasCar(Vehicle, IDrivable, ILocatable):
    fuel_level: int

    def __init__(self, id: int, brand: str, fuel: int):
        super().__init__(id, brand)
        self.fuel_level = fuel

    @override
    def start_engine(self) -> str:
        return "Gas engine roars"

    @override
    def stop_engine(self) -> str:
        return "Gas engine stopped"
```

### fleet.spy

```python
# Fleet management utilities
from vehicles import Vehicle, ElectricCar, GasCar

def create_electric_fleet() -> list[ElectricCar]:
    result: list[ElectricCar] = []
    result.append(ElectricCar(101, "Tesla", 95))
    result.append(ElectricCar(102, "Nissan", 80))
    return result

def count_vehicles(vehicles: list[Vehicle]) -> int:
    return len(vehicles)
```

### main.spy

```python
# Vehicle fleet system demonstration
from vehicles import Vehicle, ElectricCar, GasCar
from fleet import create_electric_fleet, count_vehicles

def main():
    # Create individual vehicles
    ev: ElectricCar = ElectricCar(1, "Tesla", 100)
    gas: GasCar = GasCar(2, "Ford", 50)

    # Test accessing fields
    print(ev.brand)
    print(gas.brand)

    # Test starting engines
    ev_sound: str = ev.start_engine()
    gas_sound: str = gas.start_engine()
    print(ev_sound)
    print(gas_sound)

    # Count vehicles in fleet
    fleet: list[ElectricCar] = create_electric_fleet()
    count: int = len(fleet)
    print(count)

    # Test fleet access
    first_brand: str = fleet[0].brand
    second_id: int = fleet[1].id
    print(first_brand)
    print(second_id)

    # Test locations through overridden method
    ev_location: str = ev.get_location()
    print(ev_location)

    # Test stopping engines
    stop_ev: str = ev.stop_engine()
    print(stop_ev)

# EXPECTED OUTPUT:
# Tesla
# Ford
# Electric motor silent start
# Gas engine roars
# 2
# Tesla
# 102
# Charging Station
# Electric motor stopped
```

## Timing

- Generation: 938.30s
- Execution: 4.41s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
