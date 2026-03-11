# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T20:17:45.432081
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports and coordinates across modules

from vehicle import Vehicle, ElectricVehicle, GasVehicle, IMovable, IFuelEfficient
from inventory import VehicleStatus, Priority, FleetStats, VehicleRecord
from utils import format_currency, calculate_discount, create_id, sum_priorities, Logger

def main():
    # Create vehicles from different modules
    ev: ElectricVehicle = ElectricVehicle("Model S", 120, 5, 100.0)
    gv: GasVehicle = GasVehicle("Civic", 110, 5, 50.0, 35.0)

    # Test inherited properties and methods
    print(ev.get_name())
    print(gv.get_name())

    # Test overridden methods
    print(ev.describe())
    print(gv.describe())

    # Test abstract method implementations
    print(ev.fuel_type())
    print(gv.fuel_type())

    # Test interface methods
    print(ev.calculate_efficiency())
    print(gv.calculate_efficiency())

    # Use structs from inventory module
    stats: FleetStats = FleetStats(10, 7, 2500.0)
    rate: float = stats.utilization_rate()
    print(rate)

    # Use enums and calculate sum of priorities
    priorities: list[Priority] = [Priority.HIGH, Priority.MEDIUM, Priority.LOW]
    total_priority: int = sum_priorities(priorities)
    print(total_priority)

    # Test utils formatter functions
    price: float = calculate_discount(100.0, 0.15)
    formatted: str = format_currency(price)
    print(formatted)

    # Create ID using utility
    vid: str = create_id("VEH", 42)
    print(vid)

    # Test polymorphic list
    ev2: ElectricVehicle = ElectricVehicle("Model 3", 130, 5, 75.0)
    vehicle_list: list[Vehicle] = [ev, gv, ev2]
    v: Vehicle = vehicle_list[0]
    print(v.fuel_type())
    v = vehicle_list[1]
    print(v.fuel_type())

    # Test function type alias
    filter_fn: (Vehicle) -> bool = lambda x: x.fuel_type() == "Electricity"
    print(filter_fn(vehicle_list[0]))

    # Test VehicleRecord with cross-module types
    vr: VehicleRecord = VehicleRecord("VEH-0001", VehicleStatus.AVAILABLE, Priority.HIGH)
    print(vr.vehicle_id)
    print(vr.status.name)
    print(vr.priority.value)

    # Log some messages
    Logger.log("Test message 1")
    Logger.log("Test message 2")
    logs: list[str] = Logger.get_all()
    print(logs[0])
    print(logs[1])

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Inventory.VehicleStatus' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Inventory.VehicleStatus' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4zg3euqi/main.spy:62:49
    |
 62 |     print(vr.status.name)
    |                          ^
    |

error[CS1061]: 'Inventory.Priority' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'Inventory.Priority' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4zg3euqi/main.spy:63:51
    |
 63 |     print(vr.priority.value)
    |                             ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IMovable' is never used
  --> /tmp/tmp4zg3euqi/main.spy:3:59
    |
  3 | from vehicle import Vehicle, ElectricVehicle, GasVehicle, IMovable, IFuelEfficient
    |                                                           ^^^^^^^^
    |

warning[SPY0452]: Imported name 'IFuelEfficient' is never used
  --> /tmp/tmp4zg3euqi/main.spy:3:69
    |
  3 | from vehicle import Vehicle, ElectricVehicle, GasVehicle, IMovable, IFuelEfficient
    |                                                                     ^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 247.51s
- Execution: 5.00s
