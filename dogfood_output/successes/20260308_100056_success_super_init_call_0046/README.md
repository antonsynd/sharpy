# Successful Dogfood Run

**Timestamp:** 2026-03-08T09:59:42.146530
**Feature Focus:** super_init_call
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test super().__init__() with property initialization and type narrowing
class Vehicle:
    wheels: int
    name: str

    def __init__(self, name: str, wheels: int):
        self.name = name
        self.wheels = wheels

    @virtual
    def describe(self) -> str:
        return f"{self.name} has {self.wheels} wheels"

class ElectricCar(Vehicle):
    battery_kwh: float

    def __init__(self, model: str, battery: float):
        # Call parent constructor with super().__init__()
        super().__init__(model, 4)
        self.battery_kwh = battery

    @override
    def describe(self) -> str:
        return f"{self.name} (EV) with {self.battery_kwh} kWh battery"

def main():
    # Create base vehicle
    v: Vehicle = Vehicle("Bicycle", 2)
    print(v.wheels)
    print(v.describe())

    # Create electric car using super().__init__()
    ev: ElectricCar = ElectricCar("Tesla", 75.0)
    print(ev.wheels)
    print(ev.battery_kwh)
    print(ev.describe())

    # Type narrowing with isinstance-like behavior via cast
    maybe_ev: ElectricCar? = ev
    if maybe_ev is not None:
        print(maybe_ev.battery_kwh)

```

## Output

```
2
Bicycle has 2 wheels
4
75.0
Tesla (EV) with 75.0 kWh battery
75.0
```

## Timing

- Generation: 62.87s
- Execution: 4.97s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
