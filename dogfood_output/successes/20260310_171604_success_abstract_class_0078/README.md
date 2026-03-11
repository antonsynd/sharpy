# Successful Dogfood Run

**Timestamp:** 2026-03-10T17:12:59.065462
**Feature Focus:** abstract_class
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Abstract class with inheritance and polymorphic dispatch
# Abstract base class with field and constructor, concrete subclasses
# implement abstract method with different behaviors

@abstract
class Unit:
    power: int
    
    def __init__(self, power: int):
        self.power = power
    
    @abstract
    def attack(self) -> int: ...

class Warrior(Unit):
    crit: int
    
    def __init__(self, power: int):
        super().__init__(power)
        self.crit = 2
    
    @override
    def attack(self) -> int:
        return self.power * self.crit

class Mage(Unit):
    mana: int
    
    def __init__(self, power: int):
        super().__init__(power)
        self.mana = 3
    
    @override
    def attack(self) -> int:
        return self.power + self.mana * 5

def simulate_battle(units: list[Unit]) -> int:
    total: int = 0
    for u in units:
        total += u.attack()
    return total

def main():
    units: list[Unit] = [Warrior(5), Mage(4)]
    print(Warrior(3).attack())
    print(Mage(2).attack())
    print(simulate_battle(units))

```

## Output

```
6
17
29
```

## Timing

- Generation: 174.51s
- Execution: 4.97s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
