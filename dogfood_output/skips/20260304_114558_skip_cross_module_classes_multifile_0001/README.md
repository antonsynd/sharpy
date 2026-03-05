# Skipped Dogfood Run

**Timestamp:** 2026-03-04T11:32:51.722670
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot pass argument of type 'Player' to parameter of type 'Entity'
  --> /tmp/tmpbzpv278u/main.spy:36:17
    |
 36 |     manager.add(player)
    |                 ^^^^^^
    |

error[SPY0220]: Cannot pass argument of type 'Actor' to parameter of type 'Entity'
  --> /tmp/tmpbzpv278u/main.spy:37:17
    |
 37 |     manager.add(actor)
    |                 ^^^^^
    |


**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (5 files)

## Source Files

### idservice.spy

```python
class DataService:
    _counter: int = 0

    @static
    def generate_id() -> int:
        DataService._counter += 1
        return DataService._counter

    @static
    def reset() -> None:
        DataService._counter = 0

```

### entities.spy

```python
from idservice import DataService

@abstract
class Entity:
    _id: int

    property init id: int

    @abstract
    def compute(self) -> int:
        ...

class Actor(Entity):
    _name: str
    _health: int

    property init name: str
    property init health: int

    def __init__(self, name: str, health: int = 100):
        self._id = DataService.generate_id()
        self._name = name
        self._health = health

    def compute(self) -> int:
        return self._health * 10

class Player(Actor):
    _score: int

    property init score: int = 0

    def __init__(self, name: str, health: int = 100):
        super().__init__(name, health)
        self._score = 0

    def compute(self) -> int:
        base = super().compute()
        return base + self._score

    def level_up(self) -> None:
        self._health += 20

@abstract
class Item:
    _weight: float

    property init weight: float

    @abstract
    def get_description(self) -> str:
        ...

class Weapon(Item):
    _damage: int

    property init damage: int

    def __init__(self, damage: int, weight: float):
        self._damage = damage
        self._weight = weight

    def get_description(self) -> str:
        return f"Weapon: dmg={self._damage}, wgt={self._weight}"

```

### services.spy

```python
from entities import Entity, Actor

class EntityManager:
    _entities: list[Entity]

    def __init__(self):
        self._entities = []

    def add(self, entity: Entity) -> None:
        self._entities.append(entity)

    def total_health(self) -> int:
        total = 0
        for e in self._entities:
            if isinstance(e, Actor):
                actor_ref: Actor = e
                total += actor_ref.health
        return total

    def count_actors(self) -> int:
        count = 0
        for e in self._entities:
            if isinstance(e, Actor):
                count += 1
        return count

```

### factories.spy

```python
from entities import Actor, Player, Weapon, Item, Entity

class ActorFactory:
    @static
    def create_actor(name: str) -> Actor:
        return Actor(name)

    @static
    def create_player(name: str, health: int) -> Player:
        return Player(name, health)

class ItemFactory:
    _item_count: int = 0

    @static
    def create_weapon(damage: int, weight: float) -> Weapon:
        ItemFactory._item_count += 1
        return Weapon(damage, weight)

    @static
    def get_count() -> int:
        return ItemFactory._item_count

    @static
    def reset() -> None:
        ItemFactory._item_count = 0

```

### main.spy

```python
from idservice import DataService
from entities import Actor, Player, Weapon, Item, Entity
from factories import ActorFactory, ItemFactory
from services import EntityManager

def main():
    DataService.reset()
    ItemFactory.reset()
    
    player = ActorFactory.create_player("Hero", 150)
    actor = ActorFactory.create_actor("NPC")
    sword = ItemFactory.create_weapon(25, 3.5)
    bow = ItemFactory.create_weapon(15, 2.0)
    
    print(f"Player ID: {player.id}")
    print(f"Player name: {player.name}")
    print(f"Player health: {player.health}")
    print(f"Player compute: {player.compute()}")
    
    print(f"Actor ID: {actor.id}")
    print(f"Actor name: {actor.name}")
    print(f"Actor compute: {actor.compute()}")
    
    print(f"Sword desc: {sword.get_description()}")
    print(f"Bow desc: {bow.get_description()}")
    
    print(f"Items created: {ItemFactory.get_count()}")
    
    player.level_up()
    player.score = 50
    
    print(f"After level up health: {player.health}")
    print(f"Player compute with score: {player.compute()}")
    
    manager = EntityManager()
    manager.add(player)
    manager.add(actor)
    
    print(f"Entity count: {2}")
    print(f"Actor count: {2}")

```

## Timing

- Generation: 756.84s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
