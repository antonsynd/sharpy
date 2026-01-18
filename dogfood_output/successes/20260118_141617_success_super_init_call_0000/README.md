# Successful Dogfood Run

**Timestamp:** 2026-01-18T14:16:03.380853
**Feature Focus:** super_init_call
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test super().__init__() calls with multiple inheritance levels and field initialization

class Vehicle:
    brand: str
    year: int

    def __init__(self, brand: str, year: int):
        self.brand = brand
        self.year = year

class Car(Vehicle):
    doors: int
    mileage: int

    def __init__(self, brand: str, year: int, doors: int):
        super().__init__(brand, year)
        self.doors = doors
        self.mileage = 0

    def drive(self, distance: int) -> None:
        self.mileage += distance

class ElectricCar(Car):
    battery_capacity: int
    charge_level: int

    def __init__(self, brand: str, year: int, doors: int, battery: int):
        super().__init__(brand, year, doors)
        self.battery_capacity = battery
        self.charge_level = 100

    def display_info(self) -> None:
        print(self.year)
        print(self.doors)
        print(self.mileage)
        print(self.battery_capacity)
        print(self.charge_level)

tesla = ElectricCar("Tesla", 2024, 4, 85)
tesla.drive(150)
tesla.display_info()

# EXPECTED OUTPUT:
# 2024
# 4
# 150
# 85
# 100
```

## Output

```
2024
4
150
85
100
```

## Timing

- Generation: 4.89s
- Execution: 1.55s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
