# Skipped Dogfood Run

**Timestamp:** 2026-02-19T06:57:55.300639
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0013]: Indentation must be multiple of 4 spaces (found 1)
  --> /tmp/tmpp4fak68k/dogfood_test.spy:1:1
    |
  1 |  class Weapon:
    | ^
    |

error[SPY0018]: Unterminated literal name (backtick-delimited identifier)
  --> /tmp/tmpp4fak68k/dogfood_test.spy:48:4
    |
 48 | ```
    |    ^
    |


**Feature Focus:** class_field_access
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
 class Weapon:
    name: str
    damage: int

    def __init__(self, name: str, damage: int) -> None:
        self.name = name
        self.damage = damage

class Character:
    name: str
    weapon: Weapon

    def __init__(self, name: str, weapon: Weapon) -> None:
        self.name = name
        self.weapon = weapon

    def attack_power(self) -> int:
        return self.weapon.damage

def main():
    sword: Weapon = Weapon("Iron Sword", 15)
    hero: Character = Character("Hero", sword)

    print(hero.name)
    print(hero.weapon.name)
    print(hero.weapon.damage)
    print(hero.attack_power())

    bow: Weapon = Weapon("Longbow", 12)
    archer: Character = Character("Archer", bow)

    print(archer.name)
    print(archer.weapon.name)
    print(archer.weapon.damage)

    hero.weapon.damage = 20
    print(hero.weapon.damage)

# EXPECTED OUTPUT:
# Hero
# Iron Sword
# 15
# 15
# Archer
# Longbow
# 12
# 20
```

```

## Timing

- Generation: 349.98s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
