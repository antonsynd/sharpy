# Successful Dogfood Run

**Timestamp:** 2026-03-08T15:02:22.001776
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### base.spy

```python
# Base module defining abstract appliance class

@abstract
class Appliance:
    """Abstract base class for household appliances."""
    
    @abstract
    def power_consumption(self) -> int:
        ...
    
    @virtual
    def get_type(self) -> str:
        return "Unknown appliance"

```

### impl.spy

```python
# Implementation module - defines concrete appliance types

from base import Appliance

class WashingMachine(Appliance):
    wattage: int
    
    def __init__(self, wattage: int):
        self.wattage = wattage
    
    @override
    def power_consumption(self) -> int:
        return self.wattage
    
    @override
    def get_type(self) -> str:
        return "Washing machine"

class Refrigerator(Appliance):
    annual_kwh: int
    
    def __init__(self, annual_kwh: int):
        self.annual_kwh = annual_kwh
    
    @override
    def power_consumption(self) -> int:
        return self.annual_kwh
    
    @override
    def get_type(self) -> str:
        return "Refrigerator"

```

### main.spy

```python
# Main entry point - demonstrates cross-module inheritance

from base import Appliance
from impl import WashingMachine, Refrigerator

def main():
    # Create appliances from cross-module classes
    washer = WashingMachine(500)
    fridge = Refrigerator(365)
    
    # Test specific implementations
    print(washer.power_consumption())
    print(washer.get_type())
    print(fridge.power_consumption())
    print(fridge.get_type())
    
    # Calculate total consumption
    total: int = washer.power_consumption() + fridge.power_consumption()
    print(total)

```

## Timing

- Generation: 221.40s
- Execution: 5.33s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
