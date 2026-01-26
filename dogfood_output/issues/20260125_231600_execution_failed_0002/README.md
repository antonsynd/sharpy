# Issue Report: execution_failed

**Timestamp:** 2026-01-25T23:15:12.982584
**Type:** execution_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - Fleet management system

from car import Car, Motorcycle
from fleet import FleetManager

def main():
    manager = FleetManager("City Transport")
    print(manager.get_fleet_summary())

    tesla = Car("Tesla", "Model 3", 2023, 4, True)
    manager.register_vehicle(tesla)
    print(tesla.get_description())
    print(tesla.get_vehicle_type())
    print(tesla.perform_maintenance())

    honda = Motorcycle("Honda", "CBR1000RR", 2022, False, 1000)
    manager.register_vehicle(honda)
    print(honda.get_description())
    print(honda.get_vehicle_type())

    print(manager.get_fleet_summary())

# EXPECTED OUTPUT:
# Fleet 'City Transport' has 0 vehicles
# 2023 Tesla Model 3
# Electric Car
# Check battery and electric motor
# 2022 Honda CBR1000RR
# Motorcycle
# Fleet 'City Transport' has 2 vehicles
```

## Error

```
Compilation failed:
  Semantic error at line 3, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpgn5txvrw/vehicle.spy': Parser error at line 22, column 41: Expected Colon, got Dedent (in car.spy)
  Semantic error at line 3, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpgn5txvrw/vehicle.spy': Parser error at line 22, column 41: Expected Colon, got Dedent (in car.spy)
  Semantic error at line 3, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpgn5txvrw/vehicle.spy': Parser error at line 22, column 41: Expected Colon, got Dedent (in fleet.spy)
  Semantic error at line 3, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpgn5txvrw/vehicle.spy': Parser error at line 22, column 41: Expected Colon, got Dedent (in fleet.spy)

```

## Timing

- Generation: 13.27s
- Execution: 0.87s
