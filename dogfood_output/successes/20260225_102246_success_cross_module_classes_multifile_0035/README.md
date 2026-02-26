# Successful Dogfood Run

**Timestamp:** 2026-02-25T10:18:34.207741
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### equipment.spy

```python
# Base equipment module providing base classes for cross-module inheritance testing

class Equipment:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @virtual
    def get_status(self) -> str:
        return f"{self.name}: standby"

class Machine:
    machine_id: int
    
    def __init__(self, id: int):
        self.machine_id = id
    
    @virtual
    def operate(self) -> str:
        return f"Base operation {self.machine_id}"
```

### sensors.spy

```python
# Sensors module - extends Equipment classes from equipment module

from equipment import Equipment, Machine

class TemperatureSensor(Equipment):
    reading: float
    
    def __init__(self, name: str, reading: float):
        super().__init__(name)
        self.reading = reading
    
    @override
    def get_status(self) -> str:
        return f"{self.name}: {self.reading}C"

class IndustrialMachine(Machine):
    power_level: int
    
    def __init__(self, id: int, power: int):
        super().__init__(id)
        self.power_level = power
    
    @override
    def operate(self) -> str:
        return f"Industrial {self.machine_id} at {self.power_level}%"
```

### main.spy

```python
# Main entry point - tests cross-module class inheritance and polymorphism

from equipment import Equipment, Machine
from sensors import TemperatureSensor, IndustrialMachine

def main():
    # Instantiate classes from different modules
    temp_sensor = TemperatureSensor("Sensor-A", 25.5)
    ind_machine = IndustrialMachine(100, 75)
    
    # Test 1: Polymorphic dispatch via Equipment base type
    equip: Equipment = temp_sensor
    print(equip.get_status())
    
    # Test 2: Polymorphic dispatch via Machine base type
    machine: Machine = ind_machine
    print(machine.operate())
    
    # Test 3: Direct call to overridden method
    print(temp_sensor.get_status())
    
    # Test 4: Direct call to overridden method
    print(ind_machine.operate())
    
    # Test 5: Create base class instance to show difference
    base_equip = Equipment("Base-Unit")
    print(base_equip.get_status())

# EXPECTED OUTPUT:
# Sensor-A: 25.5C
# Industrial 100 at 75%
# Sensor-A: 25.5C
# Industrial 100 at 75%
# Base-Unit: standby
```

## Timing

- Generation: 237.27s
- Execution: 4.46s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
