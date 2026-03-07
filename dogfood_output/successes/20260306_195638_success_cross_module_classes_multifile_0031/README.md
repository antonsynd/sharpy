# Successful Dogfood Run

**Timestamp:** 2026-03-06T19:55:16.459916
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (2 files)

## Source Files

### organisms.spy

```python
interface ILiving:
    def is_alive(self) -> bool: ...

@abstract
class Organism(ILiving):
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @virtual
    def introduce(self) -> str:
        return "I am " + self.name
    
    @abstract
    def get_type(self) -> str: ...
    
    def is_alive(self) -> bool:
        return True

def classify(entity: Organism) -> str:
    return entity.get_type() + ": " + entity.name

```

### main.spy

```python
from organisms import Organism, ILiving, classify

class Animal(Organism):
    def __init__(self, name: str):
        super().__init__(name)
    
    @override
    def get_type(self) -> str:
        return "Animal"

class Plant(Organism):
    def __init__(self, name: str):
        super().__init__(name)
    
    @override
    def get_type(self) -> str:
        return "Plant"

def show_info(entity: Organism) -> None:
    print(entity.introduce())
    print(classify(entity))
    if entity.is_alive():
        print("Alive")

def main():
    dog = Animal("Rex")
    rose = Plant("Rose")
    
    show_info(dog)
    show_info(rose)

```

## Timing

- Generation: 69.49s
- Execution: 4.57s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
