# Successful Dogfood Run

**Timestamp:** 2026-03-10T08:44:42.236460
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### types.spy

```python
# Core types module - defines interfaces, enums, and structs

# Status enum for entity states
enum Status:
    ACTIVE = 1
    INACTIVE = 0
    PENDING = 2

# Interface for drawable objects
interface IDrawable:
    @abstract
    def draw(self) -> str: ...

    @abstract
    def get_position(self) -> Position: ...

# Position struct - value type with coordinates
struct Position:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def __str__(self) -> str:
        return "Position(" + str(self.x) + ", " + str(self.y) + ")"

    def distance_to(self, other: Position) -> float:
        dx: float = self.x - other.x
        dy: float = self.y - other.y
        return (dx * dx + dy * dy) ** 0.5

# Simple data class for color
class Color:
    name: str
    code: int

    def __init__(self, name: str, code: int):
        self.name = name
        self.code = code

```

### entities.spy

```python
# Entities module - defines the inheritance hierarchy
from types import IDrawable, Status, Position, Color

# Abstract base entity class
@abstract
class Entity(IDrawable):
    id: int
    status: Status
    pos: Position

    def __init__(self, id: int, pos: Position):
        self.id = id
        self.status = Status.ACTIVE
        self.pos = pos

    @virtual
    def describe(self) -> str:
        return "Entity " + str(self.id)

    @abstract
    def get_type_name(self) -> str: ...

    # IDrawable implementation
    def draw(self) -> str:
        return self.get_type_name() + " at " + str(self.pos)

    def get_position(self) -> Position:
        return self.pos

# Mid-level abstract class for living entities
@abstract
class LivingEntity(Entity):
    health: int

    def __init__(self, id: int, pos: Position, health: int):
        super().__init__(id, pos)
        self.health = health

    @override
    def describe(self) -> str:
        return super().describe() + " with health " + str(self.health)

    @abstract
    def can_move(self) -> bool: ...

# Concrete implementation - Creature
class Creature(LivingEntity):
    species: str
    color: Color

    def __init__(self, id: int, pos: Position, health: int, species: str, color: Color):
        super().__init__(id, pos, health)
        self.species = species
        self.color = color

    @override
    def get_type_name(self) -> str:
        return "Creature(" + self.species + ")"

    @override
    def can_move(self) -> bool:
        return self.status == Status.ACTIVE and self.health > 0

    def get_color_name(self) -> str:
        return self.color.name

# Static utility class for entity operations
class EntityUtils:
    @static
    def count_active(entities: list[Entity]) -> int:
        count: int = 0
        for e in entities:
            if e.status == Status.ACTIVE:
                count += 1
        return count

    @static
    def average_health(living: list[LivingEntity]) -> float:
        if len(living) == 0:
            return 0.0
        total: int = 0
        for e in living:
            total += e.health
        return total / len(living)

```

### main.spy

```python
# Main entry point - demonstrates cross-module usage
from types import Status, Position, Color
from entities import Entity, LivingEntity, Creature, EntityUtils, IDrawable

def main():
    # Create positions using the struct
    pos1: Position = Position(0.0, 0.0)
    pos2: Position = Position(3.0, 4.0)
    pos3: Position = Position(10.0, 20.0)

    # Print positions and their distance
    print(str(pos1))
    print(str(pos2))
    distance: float = pos1.distance_to(pos2)
    print(distance)

    # Create colors
    red: Color = Color("Red", 0xFF0000)
    blue: Color = Color("Blue", 0x0000FF)

    # Create creatures (cross-module inheritance)
    dragon: Creature = Creature(1, pos1, 100, "Dragon", red)
    goblin: Creature = Creature(2, pos2, 30, "Goblin", blue)

    # Test polymorphic describe (LivingEntity overrides Entity)
    print(dragon.describe())
    print(goblin.describe())

    # Test interface implementation (IDrawable)
    drawable: IDrawable = dragon
    print(drawable.draw())

    # Test static utility method
    all_entities: list[Entity] = [dragon, goblin]
    active_count: int = EntityUtils.count_active(all_entities)
    print(active_count)

    # Change status and count again
    goblin.status = Status.INACTIVE
    active_count = EntityUtils.count_active(all_entities)
    print(active_count)

    # Test moving capability
    print(goblin.can_move())

    # Test average health
    living: list[LivingEntity] = [dragon, goblin]
    avg: float = EntityUtils.average_health(living)
    print(avg)

```

## Timing

- Generation: 103.05s
- Execution: 5.51s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
