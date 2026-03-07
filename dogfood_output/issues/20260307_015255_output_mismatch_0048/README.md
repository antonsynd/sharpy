# Issue Report: output_mismatch

**Timestamp:** 2026-03-07T01:47:28.811844
**Type:** output_mismatch
**Feature Focus:** property_inheritance
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Property inheritance: RPG item system with virtual computed methods
# Tests method override, polymorphic access, and control flow in derived methods

@abstract
class Item:
    property name: str
    _base_value: int

    def __init__(self, name: str, base_value: int):
        self.name = name
        self._base_value = base_value

    @abstract
    def value(self) -> int: ...

class Weapon(Item):
    damage: int
    durability: int

    def __init__(self, name: str, base_value: int, damage: int, durability: int):
        super().__init__(name, base_value)
        self.damage = damage
        self.durability = durability

    @override
    def value(self) -> int:
        quality_factor: int = self.damage * self.durability
        bonus: int = 0
        if quality_factor > 1000:
            bonus = 50
        elif quality_factor > 500:
            bonus = 20
        return self._base_value + (quality_factor // 100) + bonus

class Potion(Item):
    healing: int
    potency: float

    def __init__(self, name: str, base_value: int, healing: int, potency: float):
        super().__init__(name, base_value)
        self.healing = healing
        self.potency = potency

    @override
    def value(self) -> int:
        scaled: float = self.healing * self.potency
        if scaled > 100.0:
            return self._base_value + 75
        return self._base_value + int(scaled // 2.0)

class Scroll(Item):
    magic_level: int

    def __init__(self, name: str, base_value: int, magic_level: int):
        super().__init__(name, base_value)
        self.magic_level = magic_level

    @override
    def value(self) -> int:
        total: int = self._base_value
        i: int = 0
        while i < self.magic_level:
            total = total + 10
            i = i + 1
        return total

def inventory_value(items: list[Item]) -> int:
    result: int = 0
    for item in items:
        result = result + item.value()
    return result

def main():
    sword = Weapon("Fire Blade", 100, 25, 90)
    axe = Weapon("Rusty Axe", 50, 15, 40)
    potion = Potion("Healing Elixir", 30, 80, 1.2)
    scroll = Scroll("Teleport", 200, 5)

    print(sword.name)
    print(sword.value())
    print(axe.value())
    print(potion.value())
    print(scroll.value())

    all_items: list[Item] = [sword, axe, potion, scroll]
    print(inventory_value(all_items))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Fire Blade
275
110
78
250
713

```

### Actual
```
Fire Blade
172
76
78
250
576
```

## Timing

- Generation: 252.77s
- Execution: 4.97s
