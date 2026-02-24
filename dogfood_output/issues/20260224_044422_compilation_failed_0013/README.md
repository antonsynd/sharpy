# Issue Report: compilation_failed

**Timestamp:** 2026-02-24T04:29:10.014227
**Type:** compilation_failed
**Feature Focus:** virtual_override
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# RPG Battle System - Tests virtual/override method dispatch
# Tests: abstract classes, virtual methods, override chaining, properties

type Effect = tuple[name: str, power: int]

@abstract
class Entity:
    _name: str
    _health: int
    _max_health: int

    def __init__(self, name: str, hp: int):
        self._name = name
        self._max_health = hp
        self._health = hp

    property get name(self) -> str:
        return self._name

    @virtual
    def is_alive(self) -> bool:
        return self._health > 0

    @virtual
    def get_power(self) -> int:
        return 10

    @abstract
    def attack(self) -> Effect:
        ...

    @virtual
    def take_damage(self, amount: int) -> None:
        self._health = self._health - amount
        if self._health < 0:
            self._health = 0

    @virtual
    def status(self) -> str:
        if self.is_alive():
            return f"{self._name}: {self._health}/{self._max_health}"
        return f"{self._name}: DEFEATED"

class Warrior(Entity):
    _rage: int

    def __init__(self, name: str, hp: int):
        super().__init__(name, hp)
        self._rage = 0

    @override
    def get_power(self) -> int:
        return 15 + self._rage // 10

    @override
    def attack(self) -> Effect:
        damage: int = self.get_power()
        self._rage = self._rage + 5
        return (name="Sword Strike", power=damage)

    @override
    def take_damage(self, amount: int) -> None:
        reduced: int = amount * 3 // 4
        super().take_damage(reduced)
        self._rage = self._rage + 2

class Mage(Entity):
    _mana: int

    def __init__(self, name: str, hp: int):
        super().__init__(name, hp)
        self._mana = 100

    @override
    def get_power(self) -> int:
        power: int = 20
        if self._mana >= 30:
            power = power + self._mana // 10
        return power

    @override
    def attack(self) -> Effect:
        cost: int = 20
        if self._mana >= cost:
            self._mana = self._mana - cost
            return (name="Fireball", power=self.get_power())
        return (name="Staff Bash", power=8)

    @override
    def status(self) -> str:
        base: str = super().status()
        if self.is_alive():
            return f"{base} (mana: {self._mana})"
        return base

class BossWarrior(Warrior):
    _phase: int

    def __init__(self, name: str, hp: int):
        super().__init__(name, hp)
        self._phase = 1

    @override
    def is_alive(self) -> bool:
        if self._phase < 3:
            return True
        return self._health > 0

    @override
    def get_power(self) -> int:
        base: int = super().get_power()
        return base + self._phase * 5

    @override
    def take_damage(self, amount: int) -> None:
        old_health: int = self._health
        super().take_damage(amount)
        if old_health > 0 and self._health == 0:
            if self._phase < 3:
                self._phase = self._phase + 1
                self._health = self._max_health // 2
                print(f"{self._name} enters phase {self._phase}!")

    @override
    def status(self) -> str:
        base: str = Entity.status(self)
        return f"[BOSS P{self._phase}] {base}"

def main():
    print("Spawning combatants...")
    hero: Warrior = Warrior("Aragorn", 80)
    wizard: Mage = Mage("Gandalf", 50)
    boss: BossWarrior = BossWarrior("Balrog", 100)
    print(f"Warrior: {hero.status()} power={hero.get_power()}")
    print(f"Mage: {wizard.status()} power={wizard.get_power()}")
    print(f"Boss: {boss.status()} power={boss.get_power()}")
    print("")
    effect: Effect = hero.attack()
    print(f"{hero.name} attacks: {effect.name} ({effect.power} dmg)")
    print("")
    print("Testing boss phases...")
    print(f"Before damage: {boss.status()}")
    boss.take_damage(60)
    print(f"After 60 dmg: {boss.status()}")
    boss.take_damage(100)
    print(f"After phase kill: {boss.status()}")

# EXPECTED OUTPUT:
# Warrior: Aragorn: 80/80 power=15
# Mage: Gandalf: 50/50 (mana: 100) power=30
# Boss: [BOSS P1] Balrog: 100/100 power=20
# 
# 
# After 60 dmg: Balrog enters phase 2!
# [BOSS P2] Balrog: 50/100
# [BOSS P3] Balrog: 25/100
```

## Error

```
Assembly compilation failed:

error[CS1501]: No overload for method 'Status' takes 1 arguments
  --> /tmp/tmpe5gfcjfo/dogfood_test.spy:126:35
     |
 126 |         base: str = Entity.status(self)
     |                                   ^
     |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpe5gfcjfo/dogfood_test.cs

```

## Timing

- Generation: 893.72s
- Execution: 4.45s
