# Successful Dogfood Run

**Timestamp:** 2026-02-21T02:39:39.570778
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### base.spy

```python
# Base module - defines simple base class with virtual methods
class Entity:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @virtual
    def get_info(self) -> str:
        return "Entity: " + self.name
    
    @virtual
    def get_value(self) -> int:
        return 0
    
    def is_active(self) -> bool:
        return True
```

### derived.spy

```python
# Derived module - extends base classes
from base import Entity

class Counter(Entity):
    _count: int
    
    def __init__(self, name: str, start: int):
        super().__init__(name)
        self._count = start
    
    @override
    def get_info(self) -> str:
        return "Counter " + self.name + "=" + str(self._count)
    
    @override
    def get_value(self) -> int:
        return self._count
    
    def increment(self) -> None:
        self._count = self._count + 1
    
    def decrement(self) -> None:
        self._count = self._count - 1

class NamedCounter(Counter):
    label: str
    
    def __init__(self, name: str, start: int, label: str):
        super().__init__(name, start)
        self.label = label
    
    @override
    def get_info(self) -> str:
        return self.label + " " + super().get_info()

def create_counters() -> list[Counter]:
    c1: Counter = Counter("first", 0)
    c2: Counter = Counter("second", 5)
    return [c1, c2]
```

### main.spy

```python
# Main entry point - tests cross-module class hierarchy
from base import Entity
from derived import Counter, NamedCounter, create_counters

def print_entity_info(e: Entity) -> None:
    info: str = e.get_info()
    val: int = e.get_value()
    active: bool = e.is_active()
    print("Info: " + info)
    print("Value: " + str(val))
    print("Active: " + str(active))

def main():
    # Create counters
    c: Counter = Counter("main_counter", 10)
    nc: NamedCounter = NamedCounter("named", 25, "TEST")
    
    # Test inheritance chains
    print(c.get_info())
    print(nc.get_info())
    
    # Test polymorphism through base type
    print_entity_info(c)
    print_entity_info(nc)
    
    # Test Counter methods
    c.increment()
    c.increment()
    print("After increments: " + str(c.get_value()))
    
    c.decrement()
    print("After decrement: " + str(c.get_value()))
    
    # Test batch creation from module
    counters: list[Counter] = create_counters()
    for counter in counters:
        info: str = counter.get_info()
        print(info)
    
    # Test base class reference
    e: Entity = nc
    print(e.get_info())

# EXPECTED OUTPUT:
# Counter main_counter=10
# TEST Counter named=25
# Info: Counter main_counter=10
# Value: 10
# Active: True
# Info: TEST Counter named=25
# Value: 25
# Active: True
# After increments: 12
# After decrement: 11
# Counter first=0
# Counter second=5
# TEST Counter named=25
```

## Timing

- Generation: 294.94s
- Execution: 4.91s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
