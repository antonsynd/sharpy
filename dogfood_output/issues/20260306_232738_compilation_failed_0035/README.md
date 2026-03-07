# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T23:22:43.132795
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
from vehicles import Vehicle
from car_types import Car, Truck
from fleet import Fleet

def main():
    fleet: Fleet = Fleet()
    sedan: Car = Car("Toyota", "Camry", 2023, 4)
    coupe: Car = Car("Honda", "Civic", 2022, 2)
    pickup: Truck = Truck("Ford", "F-150", 2023, 6.5)
    
    fleet.add_vehicle(sedan)
    fleet.add_vehicle(coupe)
    fleet.add_vehicle(pickup)
    
    print("Fleet size: " + str(fleet.count()))
    print(sedan.vehicle_type())
    print(pickup.vehicle_type())
    print(fleet.summary())
    print("---")
    fleet.print_inventory()

```

## Error

```
Assembly compilation failed:

error[CS0534]: 'CarTypes.Car' does not implement inherited abstract member 'Vehicles.Vehicle.Describe()'
  --> car_types.cs:13:18
    |
 13 |     fleet.add_vehicle(pickup)
    |                  ^
    |

error[CS0534]: 'CarTypes.Truck' does not implement inherited abstract member 'Vehicles.Vehicle.Describe()'
  --> /tmp/tmpbitysh9s/car_types.spy:14:18
    |
 14 |     
    |     ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Vehicle' is never used
  --> /tmp/tmpbitysh9s/main.spy:1:22
    |
  1 | from vehicles import Vehicle
    |                      ^^^^^^^
    |


```

## Timing

- Generation: 265.21s
- Execution: 4.42s
